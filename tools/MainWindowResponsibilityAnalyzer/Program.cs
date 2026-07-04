using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

MSBuildLocator.RegisterDefaults();

string? focusArg = null;
List<string> positionalArgs = [];

for (int i = 0; i < args.Length; i++)
{
    if (string.Equals(args[i], "--focus", StringComparison.Ordinal))
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("--focus requires a value (e.g. --focus MainWindow.DataFolderBackups.cs).");
            return 2;
        }

        focusArg = args[++i];
        continue;
    }

    positionalArgs.Add(args[i]);
}

string solutionPath = positionalArgs.Count > 0
    ? Path.GetFullPath(positionalArgs[0])
    : Path.GetFullPath("./ImprovedModMenu.sln");

string? focusFileName = null;

if (focusArg is not null)
{
    string name = focusArg;

    if (!name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        name += ".cs";

    if (!name.StartsWith("MainWindow", StringComparison.OrdinalIgnoreCase))
        name = "MainWindow." + name;

    focusFileName = name;
}

if (!File.Exists(solutionPath))
{
    Console.Error.WriteLine($"Solution file not found: {solutionPath}");
    return 2;
}

string repoRoot = Path.GetDirectoryName(solutionPath)!;
string reportsDir = Path.Combine(repoRoot, "reports", "mainwindow-responsibility");
Directory.CreateDirectory(reportsDir);

using MSBuildWorkspace workspace = MSBuildWorkspace.Create();
workspace.WorkspaceFailed += (_, e) =>
    Console.Error.WriteLine($"[workspace] {e.Diagnostic.Message}");

Console.WriteLine($"Opening solution: {solutionPath}");
Solution solution = await workspace.OpenSolutionAsync(solutionPath);

Project? project = solution.Projects.FirstOrDefault(p =>
    string.Equals(p.Name, "VintageStoryModManager", StringComparison.OrdinalIgnoreCase));

if (project is null)
{
    Console.Error.WriteLine("Could not find project 'VintageStoryModManager' in the solution.");
    return 2;
}

Compilation? compilation = await project.GetCompilationAsync();

if (compilation is null)
{
    Console.Error.WriteLine("Failed to obtain a compilation for the project.");
    return 2;
}

string mainWindowDir = Path.GetFullPath(
    Path.Combine(repoRoot, "VintageStoryModManager", "Views", "MainWindow"));

List<PartialPart> partialParts = [];

foreach (SyntaxTree tree in compilation.SyntaxTrees)
{
    if (string.IsNullOrEmpty(tree.FilePath))
        continue;

    string fullPath = Path.GetFullPath(tree.FilePath);

    if (!fullPath.StartsWith(mainWindowDir, StringComparison.OrdinalIgnoreCase))
        continue;

    string fileName = Path.GetFileName(fullPath);

    if (!fileName.StartsWith("MainWindow", StringComparison.Ordinal) ||
        !fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        continue;

    ClassDeclarationSyntax? classDecl = tree.GetRoot()
        .DescendantNodes()
        .OfType<ClassDeclarationSyntax>()
        .FirstOrDefault(c => c.Identifier.ValueText == "MainWindow");

    if (classDecl is null)
        continue;

    partialParts.Add(new PartialPart(tree, classDecl, fullPath, fileName));
}

if (partialParts.Count == 0)
{
    Console.Error.WriteLine($"No MainWindow partial files found under: {mainWindowDir}");
    return 2;
}

partialParts = [.. partialParts.OrderBy(p => p.FileName, StringComparer.OrdinalIgnoreCase)];

PartialPart? focusPart = null;

if (focusFileName is not null)
{
    focusPart = partialParts.FirstOrDefault(p =>
        string.Equals(p.FileName, focusFileName, StringComparison.OrdinalIgnoreCase));

    if (focusPart is null)
    {
        Console.Error.WriteLine($"--focus target not found: {focusFileName}");
        Console.Error.WriteLine("Available MainWindow partial files:");

        foreach (PartialPart part in partialParts)
        {
            Console.Error.WriteLine($"  {part.FileName}");
        }

        return 2;
    }
}

Dictionary<SyntaxTree, SemanticModel> models = partialParts
    .Select(p => p.Tree)
    .Distinct()
    .ToDictionary(t => t, t => compilation.GetSemanticModel(t));

SemanticModel firstModel = models[partialParts[0].Tree];
INamedTypeSymbol? mainWindowSymbol = firstModel.GetDeclaredSymbol(partialParts[0].ClassDecl);

if (mainWindowSymbol is null)
{
    Console.Error.WriteLine("Could not resolve the MainWindow type symbol.");
    return 2;
}

Console.WriteLine($"Found {partialParts.Count} MainWindow partial file(s).");

// ---- Pass 1: declare every field, keyed by symbol, regardless of usage ----

Dictionary<IFieldSymbol, FieldUsage> fieldUsages = new(SymbolEqualityComparer.Default);

foreach (PartialPart part in partialParts)
{
    SemanticModel model = models[part.Tree];

    foreach (MemberDeclarationSyntax member in part.ClassDecl.Members)
    {
        if (member is not FieldDeclarationSyntax fieldDecl)
            continue;

        foreach (VariableDeclaratorSyntax variable in fieldDecl.Declaration.Variables)
        {
            if (model.GetDeclaredSymbol(variable) is IFieldSymbol fieldSymbol)
            {
                fieldUsages[fieldSymbol] = new FieldUsage(part.FileName);
            }
        }
    }
}

// ---- Pass 2: declare every method row, keyed by symbol ----

List<MethodRow> methodRows = [];
Dictionary<IMethodSymbol, MethodRow> rowBySymbol = new(SymbolEqualityComparer.Default);

foreach (PartialPart part in partialParts)
{
    SemanticModel model = models[part.Tree];

    foreach (MemberDeclarationSyntax member in part.ClassDecl.Members)
    {
        if (member is not MethodDeclarationSyntax methodDecl)
            continue;

        if (model.GetDeclaredSymbol(methodDecl) is not IMethodSymbol methodSymbol)
            continue;

        MethodRow row = new(
            PartialFile: part.FileName,
            MethodName: methodSymbol.Name,
            IsPublic: methodSymbol.DeclaredAccessibility == Accessibility.Public,
            IsAsync: methodDecl.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            IsEventHandlerLike = LooksLikeEventHandler(methodSymbol),
        };

        methodRows.Add(row);
        rowBySymbol[methodSymbol] = row;
    }
}

Dictionary<MethodRow, int> indexByRow = methodRows
    .Select((row, index) => (row, index))
    .ToDictionary(t => t.row, t => t.index);

// ---- Pass 3: walk each method body for field/method/type/await usage ----

foreach (PartialPart part in partialParts)
{
    SemanticModel model = models[part.Tree];

    foreach (MemberDeclarationSyntax member in part.ClassDecl.Members)
    {
        if (member is not MethodDeclarationSyntax methodDecl)
            continue;

        if (model.GetDeclaredSymbol(methodDecl) is not IMethodSymbol methodSymbol)
            continue;

        if (!rowBySymbol.TryGetValue(methodSymbol, out MethodRow? row))
            continue;

        SyntaxNode? body = (SyntaxNode?)methodDecl.Body ?? methodDecl.ExpressionBody;

        if (body is null)
            continue;

        AnalyzeMethodBody(body, model, mainWindowSymbol, row, fieldUsages, rowBySymbol);
    }
}

// ---- XAML event-handler scan ----

string xamlPath = Path.Combine(repoRoot, "VintageStoryModManager", "Views", "MainWindow.xaml");
List<XamlHandlerRow> xamlHandlers = [];

if (File.Exists(xamlPath))
{
    HashSet<string> knownMethodNames = methodRows
        .Select(m => m.MethodName)
        .ToHashSet(StringComparer.Ordinal);

    xamlHandlers = ParseXamlEventHandlers(
        xamlPath,
        File.ReadAllText(xamlPath),
        knownMethodNames,
        methodRows);
}

Dictionary<string, List<XamlHandlerRow>> handlersByMethodName = xamlHandlers
    .GroupBy(h => h.HandlerName, StringComparer.Ordinal)
    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

foreach (MethodRow row in methodRows)
{
    if (!handlersByMethodName.TryGetValue(row.MethodName, out List<XamlHandlerRow>? handlers))
        continue;

    foreach (XamlHandlerRow handler in handlers)
    {
        row.XamlEvents.Add($"{handler.ElementName}.{handler.EventName}");
    }

    if (row.XamlEvents.Count > 0)
    {
        row.IsEventHandlerLike = true;
    }
}

// ---- Extraction candidates via union-find over shared fields + internal calls ----

List<ExtractionCandidate> candidates = BuildExtractionCandidates(methodRows, fieldUsages, indexByRow);

// ---- Write reports ----

WritePartialSummary(reportsDir, partialParts, methodRows, fieldUsages);
WriteMethodDependenciesCsv(reportsDir, methodRows);
WriteFieldUsageCsv(reportsDir, fieldUsages);
WriteCrossPartialCallsCsv(reportsDir, methodRows);
WriteExtractionCandidatesReport(reportsDir, candidates);
WriteXamlEventHandlersCsv(reportsDir, xamlHandlers);

if (focusPart is not null)
{
    WriteFocusedReport(reportsDir, focusPart, methodRows, fieldUsages);
    Console.WriteLine($"Focused report written for: {focusPart.FileName}");
}

Console.WriteLine();
Console.WriteLine($"Partial files analyzed: {partialParts.Count}");
Console.WriteLine($"Methods analyzed:       {methodRows.Count}");
Console.WriteLine($"Fields analyzed:        {fieldUsages.Count}");
Console.WriteLine($"XAML handlers found:    {xamlHandlers.Count}");
Console.WriteLine($"Extraction candidates:  {candidates.Count}");
Console.WriteLine();
Console.WriteLine($"Reports written to: {reportsDir}");

return 0;

// ===========================================================================
// Local functions
// ===========================================================================

static bool LooksLikeEventHandler(IMethodSymbol methodSymbol)
{
    if (methodSymbol.Parameters.Length != 2)
        return false;

    string firstType = methodSymbol.Parameters[0].Type.ToDisplayString();
    string secondType = methodSymbol.Parameters[1].Type.Name;

    bool firstIsObject = firstType is "object" or "object?";
    bool secondIsEventArgs = secondType.EndsWith("EventArgs", StringComparison.Ordinal);

    return firstIsObject && secondIsEventArgs;
}

static void AnalyzeMethodBody(
    SyntaxNode body,
    SemanticModel model,
    INamedTypeSymbol mainWindowSymbol,
    MethodRow row,
    Dictionary<IFieldSymbol, FieldUsage> fieldUsages,
    Dictionary<IMethodSymbol, MethodRow> rowBySymbol)
{
    string selfId = $"{row.PartialFile}::{row.MethodName}";

    foreach (SyntaxNode node in body.DescendantNodes())
    {
        switch (node)
        {
            case SimpleNameSyntax simpleName:
                AnalyzeSimpleName(simpleName, model, mainWindowSymbol, row, fieldUsages, selfId);
                break;

            case InvocationExpressionSyntax invocation:
                AnalyzeInvocation(invocation, model, mainWindowSymbol, row, rowBySymbol);
                break;

            case AwaitExpressionSyntax awaitExpression:
                row.AwaitedCalls.Add(DescribeAwaitedExpression(awaitExpression.Expression));
                break;
        }
    }
}

static void AnalyzeSimpleName(
    SimpleNameSyntax node,
    SemanticModel model,
    INamedTypeSymbol mainWindowSymbol,
    MethodRow row,
    Dictionary<IFieldSymbol, FieldUsage> fieldUsages,
    string selfId)
{
    ISymbol? symbol = model.GetSymbolInfo(node).Symbol;

    if (symbol is null)
        return;

    if (symbol is IFieldSymbol fieldSymbol &&
        SymbolEqualityComparer.Default.Equals(fieldSymbol.ContainingType, mainWindowSymbol))
    {
        if (!fieldUsages.TryGetValue(fieldSymbol, out FieldUsage? usage))
        {
            // A MainWindow field the analyzer never declared as a tracked source field —
            // e.g. an x:Name-generated control field from MainWindow.g.cs, which lives
            // outside the Views/MainWindow partials this tool scans.
            row.UntrackedMainWindowMembersUsed.Add(fieldSymbol.Name);
            return;
        }

        ExpressionSyntax referenceExpression =
            node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == node
                ? memberAccess
                : node;

        (bool isRead, bool isWrite) = ClassifyFieldAccess(referenceExpression);

        if (isRead)
        {
            row.FieldsRead.Add(fieldSymbol.Name);
            usage.ReadBy.Add(selfId);
            usage.ReaderRows.Add(row);
            usage.ReadCount++;
        }

        if (isWrite)
        {
            row.FieldsWritten.Add(fieldSymbol.Name);
            usage.WrittenBy.Add(selfId);
            usage.WriterRows.Add(row);
            usage.WriteCount++;
        }

        return;
    }

    if (symbol is IPropertySymbol propertySymbol &&
        SymbolEqualityComparer.Default.Equals(propertySymbol.ContainingType, mainWindowSymbol))
    {
        row.UntrackedMainWindowMembersUsed.Add(propertySymbol.Name);
        return;
    }

    if (symbol is IEventSymbol eventSymbol &&
        SymbolEqualityComparer.Default.Equals(eventSymbol.ContainingType, mainWindowSymbol))
    {
        row.UntrackedMainWindowMembersUsed.Add(eventSymbol.Name);
        return;
    }

    if (symbol is INamedTypeSymbol namedType &&
        !string.Equals(namedType.Name, "MainWindow", StringComparison.Ordinal) &&
        !IsSystemNamespace(namedType.ContainingNamespace))
    {
        row.ExternalTypes.Add(namedType.ToDisplayString());
    }
}

static (bool IsRead, bool IsWrite) ClassifyFieldAccess(ExpressionSyntax referenceExpression)
{
    SyntaxNode? parent = referenceExpression.Parent;

    if (parent is AssignmentExpressionSyntax assignment && assignment.Left == referenceExpression)
    {
        bool isCompound = !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression);
        return (isCompound, true);
    }

    if (parent is PostfixUnaryExpressionSyntax postfix && postfix.Operand == referenceExpression)
        return (true, true);

    if (parent is PrefixUnaryExpressionSyntax prefix &&
        prefix.Operand == referenceExpression &&
        (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression)))
        return (true, true);

    if (parent is ArgumentSyntax argument && argument.Expression == referenceExpression)
    {
        if (argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
            return (false, true);

        if (argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
            return (true, true);
    }

    return (true, false);
}

static bool IsSystemNamespace(INamespaceSymbol? namespaceSymbol)
{
    if (namespaceSymbol is null || namespaceSymbol.IsGlobalNamespace)
        return false;

    string display = namespaceSymbol.ToDisplayString();

    return display == "System" || display.StartsWith("System.", StringComparison.Ordinal);
}

static void AnalyzeInvocation(
    InvocationExpressionSyntax invocation,
    SemanticModel model,
    INamedTypeSymbol mainWindowSymbol,
    MethodRow row,
    Dictionary<IMethodSymbol, MethodRow> rowBySymbol)
{
    if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol calledMethod)
        return;

    IMethodSymbol original = calledMethod.OriginalDefinition;

    if (!SymbolEqualityComparer.Default.Equals(original.ContainingType, mainWindowSymbol))
        return;

    row.MethodsCalled.Add(original.Name);

    if (rowBySymbol.TryGetValue(original, out MethodRow? calleeRow) && calleeRow != row)
    {
        row.CalledRows.Add(calleeRow);
    }
}

static string DescribeAwaitedExpression(ExpressionSyntax expression)
{
    return expression is InvocationExpressionSyntax invocation
        ? invocation.Expression.ToString()
        : expression.ToString();
}

static List<XamlHandlerRow> ParseXamlEventHandlers(
    string xamlFilePath,
    string xamlText,
    HashSet<string> knownMethodNames,
    List<MethodRow> methodRows)
{
    Dictionary<string, string> handlerToFile = methodRows
        .GroupBy(m => m.MethodName, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.First().PartialFile, StringComparer.Ordinal);

    Regex tagRegex = new(@"<([A-Za-z_][\w\.:]*)((?:\s+[^>]*?)?)/?>", RegexOptions.Singleline);
    Regex attrRegex = new(@"([A-Za-z_][\w\.:]*)\s*=\s*""([^""]*)""");

    List<XamlHandlerRow> results = [];
    string xamlFileName = Path.GetFileName(xamlFilePath);

    foreach (Match tagMatch in tagRegex.Matches(xamlText))
    {
        string tagName = tagMatch.Groups[1].Value;
        MatchCollection attributeMatches = attrRegex.Matches(tagMatch.Groups[2].Value);

        string? elementName = attributeMatches
            .Cast<Match>()
            .FirstOrDefault(m => m.Groups[1].Value is "x:Name" or "Name")
            ?.Groups[2].Value;

        foreach (Match attributeMatch in attributeMatches)
        {
            string attributeName = attributeMatch.Groups[1].Value;
            string attributeValue = attributeMatch.Groups[2].Value;

            if (attributeName is "x:Name" or "Name")
                continue;

            if (!IsValidIdentifier(attributeValue) || !knownMethodNames.Contains(attributeValue))
                continue;

            results.Add(
                new XamlHandlerRow(
                    XamlFile: xamlFileName,
                    ElementName: elementName ?? tagName,
                    EventName: attributeName,
                    HandlerName: attributeValue,
                    HandlerPartialFile: handlerToFile.GetValueOrDefault(attributeValue, "(unknown)")));
        }
    }

    return [.. results
        .OrderBy(r => r.ElementName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.EventName, StringComparer.OrdinalIgnoreCase)];
}

static bool IsValidIdentifier(string value)
{
    if (string.IsNullOrEmpty(value))
        return false;

    if (!char.IsLetter(value[0]) && value[0] != '_')
        return false;

    return value.All(c => char.IsLetterOrDigit(c) || c == '_');
}

static List<ExtractionCandidate> BuildExtractionCandidates(
    List<MethodRow> methodRows,
    Dictionary<IFieldSymbol, FieldUsage> fieldUsages,
    Dictionary<MethodRow, int> indexByRow)
{
    int count = methodRows.Count;
    int[] parent = [.. Enumerable.Range(0, count)];

    int Find(int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }

        return x;
    }

    void Union(int a, int b)
    {
        int rootA = Find(a);
        int rootB = Find(b);

        if (rootA != rootB)
        {
            parent[rootA] = rootB;
        }
    }

    // ponytail: a field/method touched from many files is shared infrastructure
    // (_viewModel, ReportStatus, ...), not evidence that its callers belong together —
    // without this cap, one hub welds the entire class into a single supercluster.
    // Raise the cap if real clusters start getting split apart by it.
    const int MaxFilesForClusterSignal = 3;

    foreach (FieldUsage usage in fieldUsages.Values)
    {
        List<MethodRow> touchingRows = [.. usage.ReaderRows.Concat(usage.WriterRows).Distinct()];

        int distinctFileCount = touchingRows
            .Select(row => row.PartialFile)
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (distinctFileCount > MaxFilesForClusterSignal)
            continue;

        List<int> indices = [.. touchingRows.Select(row => indexByRow[row])];

        for (int i = 1; i < indices.Count; i++)
        {
            Union(indices[0], indices[i]);
        }
    }

    Dictionary<MethodRow, HashSet<string>> callerFilesByCallee = [];

    foreach (MethodRow row in methodRows)
    {
        foreach (MethodRow calleeRow in row.CalledRows)
        {
            if (!callerFilesByCallee.TryGetValue(calleeRow, out HashSet<string>? callerFiles))
            {
                callerFiles = new HashSet<string>(StringComparer.Ordinal);
                callerFilesByCallee[calleeRow] = callerFiles;
            }

            callerFiles.Add(row.PartialFile);
        }
    }

    for (int i = 0; i < count; i++)
    {
        foreach (MethodRow calleeRow in methodRows[i].CalledRows)
        {
            if (!indexByRow.TryGetValue(calleeRow, out int calleeIndex))
                continue;

            if (callerFilesByCallee[calleeRow].Count > MaxFilesForClusterSignal)
                continue;

            Union(i, calleeIndex);
        }
    }

    Dictionary<int, List<int>> components = [];

    for (int i = 0; i < count; i++)
    {
        int root = Find(i);

        if (!components.TryGetValue(root, out List<int>? list))
        {
            list = [];
            components[root] = list;
        }

        list.Add(i);
    }

    List<ExtractionCandidate> candidates = [];

    foreach (List<int> indices in components.Values)
    {
        List<MethodRow> members = [.. indices.Select(i => methodRows[i])];

        List<string> distinctFiles = [.. members
            .Select(m => m.PartialFile)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)];

        if (members.Count < 2 || distinctFiles.Count < 2)
            continue;

        HashSet<string> memberIds = members
            .Select(m => $"{m.PartialFile}::{m.MethodName}")
            .ToHashSet(StringComparer.Ordinal);

        List<string> fieldsInvolved = [.. fieldUsages
            .Where(kvp => kvp.Value.ReadBy.Concat(kvp.Value.WrittenBy).Any(memberIds.Contains))
            .Select(kvp => kvp.Key.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(f => f, StringComparer.Ordinal)];

        List<string> outsideDependencies = [.. members
            .SelectMany(m => m.ExternalTypes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)];

        string dominantFile = members
            .GroupBy(m => m.PartialFile, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .First()
            .Key;

        // ponytail: capping fan-in per edge (above) stops one hub field from welding
        // everything together directly, but long chains of *distinct* low-fan-in edges
        // can still transitively connect a large swath of the class. A component this
        // big is a background artifact of that chaining, not one real service boundary
        // — call it out rather than presenting it as an actionable cluster.
        const int BackgroundArtifactMethodThreshold = 20;

        bool isBackgroundArtifact = members.Count > BackgroundArtifactMethodThreshold;

        string risk = isBackgroundArtifact
            ? "high"
            : distinctFiles.Count == 3 || members.Count > 4
                ? "medium"
                : "low";

        string name = isBackgroundArtifact
            ? $"Background transitive-closure artifact (+{distinctFiles.Count - 1} other file(s))"
            : $"{StripPartialName(dominantFile)} cluster (+{distinctFiles.Count - 1} other file(s))";

        string why = isBackgroundArtifact
            ? $"{members.Count} methods across {distinctFiles.Count} files end up connected only by chaining " +
              "together many separately-narrow field/call edges (each below the fan-in cap on its own). Not one " +
              "real cluster — treat it as background noise and look at the smaller candidates below, or at " +
              "field-usage.csv / cross-partial-calls.csv directly, for actionable leads."
            : risk == "low"
                ? $"{members.Count} methods across {distinctFiles.Count} files share " +
                  $"{fieldsInvolved.Count} field(s)/call edges; small enough that this is likely just a couple of " +
                  $"misplaced methods that belong in {dominantFile}."
                : $"{members.Count} methods across {distinctFiles.Count} files share " +
                  $"{fieldsInvolved.Count} field(s)/call edges; a real cross-cutting cluster that deserves its own " +
                  "scoped extraction pass before anything is moved.";

        candidates.Add(
            new ExtractionCandidate(
                Name: name,
                LikelySourcePartial: dominantFile,
                Methods: [.. memberIds.OrderBy(id => id, StringComparer.Ordinal)],
                Fields: fieldsInvolved,
                OutsideDependencies: outsideDependencies,
                Risk: risk,
                Why: why,
                IsBackgroundArtifact: isBackgroundArtifact));
    }

    return [.. candidates
        .OrderBy(c => c.IsBackgroundArtifact)
        .ThenByDescending(c => c.Methods.Count)];
}

static string StripPartialName(string fileName)
{
    return fileName
        .Replace("MainWindow.", string.Empty, StringComparison.Ordinal)
        .Replace(".cs", string.Empty, StringComparison.Ordinal);
}

static string Csv(string value)
{
    if (value.IndexOfAny(['"', ',', '\n', '\r']) < 0)
        return value;

    return "\"" + value.Replace("\"", "\"\"") + "\"";
}

static string JoinList(IEnumerable<string> values)
{
    return string.Join(";", values);
}

static void WritePartialSummary(
    string reportsDir,
    List<PartialPart> partialParts,
    List<MethodRow> methodRows,
    Dictionary<IFieldSymbol, FieldUsage> fieldUsages)
{
    StringBuilder sb = new();

    sb.AppendLine("# MainWindow Partial Summary");
    sb.AppendLine();
    sb.AppendLine("| File | Lines | Methods | Fields | Event handlers | Public | Private |");
    sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");

    foreach (PartialPart part in partialParts)
    {
        List<MethodRow> fileMethods = [.. methodRows.Where(m => m.PartialFile == part.FileName)];
        int fieldCount = fieldUsages.Values.Count(u => u.DeclaredInFile == part.FileName);
        int eventHandlerCount = fileMethods.Count(m => m.IsEventHandlerLike);
        int publicCount = fileMethods.Count(m => m.IsPublic);
        int privateCount = fileMethods.Count - publicCount;
        int lineCount = part.Tree.GetText().Lines.Count;

        sb.AppendLine(
            $"| {part.FileName} | {lineCount} | {fileMethods.Count} | {fieldCount} | " +
            $"{eventHandlerCount} | {publicCount} | {privateCount} |");
    }

    sb.AppendLine();
    sb.AppendLine("## Method inventory per file");

    foreach (PartialPart part in partialParts)
    {
        List<MethodRow> fileMethods = [.. methodRows.Where(m => m.PartialFile == part.FileName)];

        sb.AppendLine();
        sb.AppendLine($"### {part.FileName}");

        sb.AppendLine();
        sb.AppendLine("Public:");

        foreach (string name in fileMethods.Where(m => m.IsPublic).Select(m => m.MethodName).OrderBy(n => n, StringComparer.Ordinal))
        {
            sb.AppendLine($"- {name}");
        }

        sb.AppendLine();
        sb.AppendLine("Private:");

        foreach (string name in fileMethods.Where(m => !m.IsPublic).Select(m => m.MethodName).OrderBy(n => n, StringComparer.Ordinal))
        {
            sb.AppendLine($"- {name}");
        }
    }

    File.WriteAllText(Path.Combine(reportsDir, "partial-summary.md"), sb.ToString());
}

static void WriteMethodDependenciesCsv(string reportsDir, List<MethodRow> methodRows)
{
    StringBuilder sb = new();

    sb.AppendLine(
        "PartialFile,MethodName,IsAsync,IsEventHandlerLike,FieldsRead,FieldsWritten," +
        "MainWindowMethodsCalled,ExternalTypesUsed,AwaitedCalls,XamlEventsReferencingMethod");

    foreach (MethodRow row in methodRows
        .OrderBy(r => r.PartialFile, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.MethodName, StringComparer.Ordinal))
    {
        sb.AppendLine(string.Join(
            ",",
            Csv(row.PartialFile),
            Csv(row.MethodName),
            row.IsAsync,
            row.IsEventHandlerLike,
            Csv(JoinList(row.FieldsRead)),
            Csv(JoinList(row.FieldsWritten)),
            Csv(JoinList(row.MethodsCalled)),
            Csv(JoinList(row.ExternalTypes)),
            Csv(JoinList(row.AwaitedCalls)),
            Csv(JoinList(row.XamlEvents))));
    }

    File.WriteAllText(Path.Combine(reportsDir, "method-dependencies.csv"), sb.ToString());
}

static void WriteFieldUsageCsv(string reportsDir, Dictionary<IFieldSymbol, FieldUsage> fieldUsages)
{
    StringBuilder sb = new();

    sb.AppendLine(
        "FieldName,DeclaredInFile,ReadByMethods,WrittenByMethods,UsedByPartialFiles," +
        "UsageCount,CandidateOwnerPartial,Notes");

    foreach ((IFieldSymbol field, FieldUsage usage) in fieldUsages
        .OrderBy(kvp => kvp.Key.Name, StringComparer.Ordinal))
    {
        List<string> usedByFiles = [.. usage.ReadBy
            .Concat(usage.WrittenBy)
            .Select(id => id.Split("::", 2)[0])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)];

        Dictionary<string, int> fileWeights = usage.ReadBy
            .Concat(usage.WrittenBy)
            .GroupBy(id => id.Split("::", 2)[0], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        string candidateOwner = fileWeights.Count == 0
            ? usage.DeclaredInFile
            : fileWeights.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).First().Key;

        int usageCount = usage.ReadCount + usage.WriteCount;

        string notes = usedByFiles.Count == 0
            ? "Unused field (dead code candidate)."
            : usedByFiles.Count == 1 && usedByFiles[0] == usage.DeclaredInFile
                ? "Used only within declaring file."
                : candidateOwner != usage.DeclaredInFile
                    ? $"Predominantly used by {candidateOwner}; consider moving field there."
                    : $"Used across {usedByFiles.Count} files; declaring file remains the best owner.";

        sb.AppendLine(string.Join(
            ",",
            Csv(field.Name),
            Csv(usage.DeclaredInFile),
            Csv(JoinList(usage.ReadBy.OrderBy(s => s, StringComparer.Ordinal))),
            Csv(JoinList(usage.WrittenBy.OrderBy(s => s, StringComparer.Ordinal))),
            Csv(JoinList(usedByFiles)),
            usageCount,
            Csv(candidateOwner),
            Csv(notes)));
    }

    File.WriteAllText(Path.Combine(reportsDir, "field-usage.csv"), sb.ToString());
}

static void WriteCrossPartialCallsCsv(string reportsDir, List<MethodRow> methodRows)
{
    HashSet<(string CallerFile, string CallerMethod, string CalleeFile, string CalleeMethod)> pairs = [];

    foreach (MethodRow row in methodRows)
    {
        foreach (MethodRow calleeRow in row.CalledRows)
        {
            if (calleeRow.PartialFile == row.PartialFile)
                continue;

            pairs.Add((row.PartialFile, row.MethodName, calleeRow.PartialFile, calleeRow.MethodName));
        }
    }

    HashSet<(string, string)> fileLevelDirections = [.. pairs.Select(p => (p.CallerFile, p.CalleeFile))];

    StringBuilder sb = new();
    sb.AppendLine("CallerFile,CallerMethod,CalleeFile,CalleeMethod,Direction,Notes");

    foreach ((string callerFile, string callerMethod, string calleeFile, string calleeMethod) in pairs
        .OrderBy(p => p.CallerFile, StringComparer.OrdinalIgnoreCase)
        .ThenBy(p => p.CallerMethod, StringComparer.Ordinal)
        .ThenBy(p => p.CalleeFile, StringComparer.OrdinalIgnoreCase)
        .ThenBy(p => p.CalleeMethod, StringComparer.Ordinal))
    {
        bool bidirectional = fileLevelDirections.Contains((calleeFile, callerFile));
        string notes = bidirectional ? "Bidirectional file-level coupling." : string.Empty;

        sb.AppendLine(string.Join(
            ",",
            Csv(callerFile),
            Csv(callerMethod),
            Csv(calleeFile),
            Csv(calleeMethod),
            Csv($"{callerFile} -> {calleeFile}"),
            Csv(notes)));
    }

    File.WriteAllText(Path.Combine(reportsDir, "cross-partial-calls.csv"), sb.ToString());
}

static void WriteExtractionCandidatesReport(string reportsDir, List<ExtractionCandidate> candidates)
{
    StringBuilder sb = new();

    sb.AppendLine("# Extraction Candidates");
    sb.AppendLine();
    sb.AppendLine(
        "Generated by clustering methods that share fields or call each other, via union-find. " +
        "Only clusters spanning more than one existing partial file are listed below — a cluster confined " +
        "to a single file is already correctly grouped and needs no action.");

    if (candidates.Count == 0)
    {
        sb.AppendLine();
        sb.AppendLine("No cross-partial clusters found.");
        File.WriteAllText(Path.Combine(reportsDir, "extraction-candidates.md"), sb.ToString());
        return;
    }

    const int MaxListedItems = 15;

    static string Truncated(List<string> items)
    {
        if (items.Count == 0)
            return "(none)";

        if (items.Count <= MaxListedItems)
            return JoinList(items);

        return JoinList(items.Take(MaxListedItems)) + $" (+{items.Count - MaxListedItems} more)";
    }

    for (int i = 0; i < candidates.Count; i++)
    {
        ExtractionCandidate candidate = candidates[i];

        sb.AppendLine();
        sb.AppendLine($"## {i + 1}. {candidate.Name}");
        sb.AppendLine();
        sb.AppendLine($"- **Likely source partial:** {candidate.LikelySourcePartial}");
        sb.AppendLine($"- **Risk level:** {candidate.Risk}");
        sb.AppendLine($"- **Methods involved ({candidate.Methods.Count}):** {Truncated(candidate.Methods)}");
        sb.AppendLine($"- **Fields involved ({candidate.Fields.Count}):** {Truncated(candidate.Fields)}");
        sb.AppendLine($"- **Outside dependencies:** {Truncated(candidate.OutsideDependencies)}");
        sb.AppendLine($"- **Why:** {candidate.Why}");
    }

    File.WriteAllText(Path.Combine(reportsDir, "extraction-candidates.md"), sb.ToString());
}

static void WriteXamlEventHandlersCsv(string reportsDir, List<XamlHandlerRow> xamlHandlers)
{
    StringBuilder sb = new();
    sb.AppendLine("XamlFile,ElementName,EventName,HandlerName,HandlerPartialFile");

    foreach (XamlHandlerRow handler in xamlHandlers)
    {
        sb.AppendLine(string.Join(
            ",",
            Csv(handler.XamlFile),
            Csv(handler.ElementName),
            Csv(handler.EventName),
            Csv(handler.HandlerName),
            Csv(handler.HandlerPartialFile)));
    }

    File.WriteAllText(Path.Combine(reportsDir, "xaml-event-handlers.csv"), sb.ToString());
}

static void WriteFocusedReport(
    string reportsDir,
    PartialPart focusPart,
    List<MethodRow> methodRows,
    Dictionary<IFieldSymbol, FieldUsage> fieldUsages)
{
    string focusFile = focusPart.FileName;

    List<MethodRow> focusMethods = [.. methodRows
        .Where(m => m.PartialFile == focusFile)
        .OrderBy(m => m.MethodName, StringComparer.Ordinal)];

    StringBuilder sb = new();

    // 1. Header
    sb.AppendLine($"# Focused Report: {focusFile}");
    sb.AppendLine();
    sb.AppendLine("## Header");
    sb.AppendLine();
    sb.AppendLine($"- **Focused partial file:** {focusFile}");
    sb.AppendLine($"- **Line count:** {focusPart.Tree.GetText().Lines.Count}");
    sb.AppendLine($"- **Method count:** {focusMethods.Count}");
    sb.AppendLine($"- **Event handler count:** {focusMethods.Count(m => m.IsEventHandlerLike)}");

    // 2. Method inventory
    sb.AppendLine();
    sb.AppendLine("## Method Inventory");

    foreach (MethodRow row in focusMethods)
    {
        sb.AppendLine();
        sb.AppendLine($"### {row.MethodName}");
        sb.AppendLine();
        sb.AppendLine($"- **Async:** {(row.IsAsync ? "yes" : "no")}");
        sb.AppendLine($"- **Event-handler-like:** {(row.IsEventHandlerLike ? "yes" : "no")}");
        sb.AppendLine($"- **XAML events:** {DescribeOrNone(row.XamlEvents)}");
        sb.AppendLine($"- **Fields read:** {DescribeOrNone(row.FieldsRead)}");
        sb.AppendLine($"- **Fields written:** {DescribeOrNone(row.FieldsWritten)}");
        sb.AppendLine($"- **Untracked MainWindow members used:** {DescribeOrNone(row.UntrackedMainWindowMembersUsed)}");
        sb.AppendLine($"- **MainWindow methods called:** {DescribeOrNone(row.MethodsCalled)}");
        sb.AppendLine($"- **Awaited calls:** {DescribeOrNone(row.AwaitedCalls)}");
        sb.AppendLine($"- **External types used:** {DescribeOrNone(row.ExternalTypes)}");
    }

    // 3. Incoming dependencies
    List<(string CallerFile, string CallerMethod, string CalleeMethod)> incoming = [.. methodRows
        .Where(r => r.PartialFile != focusFile)
        .SelectMany(r => r.CalledRows
            .Where(c => c.PartialFile == focusFile)
            .Select(c => (CallerFile: r.PartialFile, CallerMethod: r.MethodName, CalleeMethod: c.MethodName)))
        .Distinct()
        .OrderBy(t => t.CallerFile, StringComparer.OrdinalIgnoreCase)
        .ThenBy(t => t.CallerMethod, StringComparer.Ordinal)
        .ThenBy(t => t.CalleeMethod, StringComparer.Ordinal)];

    sb.AppendLine();
    sb.AppendLine("## Incoming Dependencies");
    sb.AppendLine();
    sb.AppendLine("Methods in other MainWindow partials that call methods in this focused partial.");
    sb.AppendLine();

    if (incoming.Count == 0)
    {
        sb.AppendLine("(none)");
    }
    else
    {
        sb.AppendLine("| Caller file | Caller method | Called method (this partial) |");
        sb.AppendLine("|---|---|---|");

        foreach ((string callerFile, string callerMethod, string calleeMethod) in incoming)
        {
            sb.AppendLine($"| {callerFile} | {callerMethod} | {calleeMethod} |");
        }
    }

    // 4. Outgoing dependencies
    List<(string CallerMethod, string CalleeFile, string CalleeMethod)> outgoing = [.. focusMethods
        .SelectMany(r => r.CalledRows
            .Where(c => c.PartialFile != focusFile)
            .Select(c => (CallerMethod: r.MethodName, CalleeFile: c.PartialFile, CalleeMethod: c.MethodName)))
        .Distinct()
        .OrderBy(t => t.CallerMethod, StringComparer.Ordinal)
        .ThenBy(t => t.CalleeFile, StringComparer.OrdinalIgnoreCase)
        .ThenBy(t => t.CalleeMethod, StringComparer.Ordinal)];

    sb.AppendLine();
    sb.AppendLine("## Outgoing Dependencies");
    sb.AppendLine();
    sb.AppendLine("Methods in this focused partial that call methods in other MainWindow partials.");
    sb.AppendLine();

    if (outgoing.Count == 0)
    {
        sb.AppendLine("(none)");
    }
    else
    {
        sb.AppendLine("| Method (this partial) | Called file | Called method |");
        sb.AppendLine("|---|---|---|");

        foreach ((string callerMethod, string calleeFile, string calleeMethod) in outgoing)
        {
            sb.AppendLine($"| {callerMethod} | {calleeFile} | {calleeMethod} |");
        }
    }

    // 5. Field ownership notes
    List<FocusFieldInfo> relevantFields = [];

    foreach ((IFieldSymbol field, FieldUsage usage) in fieldUsages)
    {
        HashSet<string> usedByFiles = usage.ReadBy
            .Concat(usage.WrittenBy)
            .Select(id => id.Split("::", 2)[0])
            .ToHashSet(StringComparer.Ordinal);

        bool relevant = usedByFiles.Contains(focusFile) ||
            string.Equals(usage.DeclaredInFile, focusFile, StringComparison.Ordinal);

        if (!relevant)
            continue;

        Dictionary<string, int> fileWeights = usage.ReadBy
            .Concat(usage.WrittenBy)
            .GroupBy(id => id.Split("::", 2)[0], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        string dominantFile = fileWeights.Count == 0
            ? usage.DeclaredInFile
            : fileWeights.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).First().Key;

        string bucket = usedByFiles.Count <= 1
            ? "only"
            : dominantFile == focusFile
                ? "mostly"
                : "shared";

        relevantFields.Add(new FocusFieldInfo(field.Name, bucket));
    }

    relevantFields = [.. relevantFields.OrderBy(f => f.Name, StringComparer.Ordinal)];

    sb.AppendLine();
    sb.AppendLine("## Field Ownership Notes");

    sb.AppendLine();
    sb.AppendLine("**Used only by this focused partial:**");
    sb.AppendLine();
    AppendFieldBucket(sb, relevantFields, "only");

    sb.AppendLine();
    sb.AppendLine("**Used mostly by this focused partial:**");
    sb.AppendLine();
    AppendFieldBucket(sb, relevantFields, "mostly");

    sb.AppendLine();
    sb.AppendLine("**Shared broadly across MainWindow:**");
    sb.AppendLine();
    AppendFieldBucket(sb, relevantFields, "shared");

    // 6. Extraction seam suggestions
    HashSet<string> sharedFieldNames = relevantFields
        .Where(f => f.Bucket == "shared")
        .Select(f => f.Name)
        .ToHashSet(StringComparer.Ordinal);

    Dictionary<string, List<MethodRow>> methodsByCategory = new()
    {
        ["safe helper extraction"] = [],
        ["possible service extraction"] = [],
        ["keep in MainWindow because UI/XAML-bound"] = [],
        ["keep in MainWindow because it touches UI/generated MainWindow members"] = [],
        ["high-risk due to shared state"] = [],
    };

    foreach (MethodRow row in focusMethods)
    {
        bool isXamlBound = row.IsEventHandlerLike || row.XamlEvents.Count > 0;
        bool touchesUntrackedMembers = row.UntrackedMainWindowMembersUsed.Count > 0;
        bool touchesSharedField = row.FieldsRead.Concat(row.FieldsWritten).Any(sharedFieldNames.Contains);
        bool touchesAnyField = row.FieldsRead.Count > 0 || row.FieldsWritten.Count > 0 || row.MethodsCalled.Count > 0;

        string category = isXamlBound
            ? "keep in MainWindow because UI/XAML-bound"
            : touchesUntrackedMembers
                ? "keep in MainWindow because it touches UI/generated MainWindow members"
                : touchesSharedField
                    ? "high-risk due to shared state"
                    : !touchesAnyField
                        ? "safe helper extraction"
                        : "possible service extraction";

        methodsByCategory[category].Add(row);
    }

    sb.AppendLine();
    sb.AppendLine("## Extraction Seam Suggestions");

    foreach (string category in new[]
    {
        "safe helper extraction",
        "possible service extraction",
        "keep in MainWindow because UI/XAML-bound",
        "keep in MainWindow because it touches UI/generated MainWindow members",
        "high-risk due to shared state",
    })
    {
        List<MethodRow> members = [.. methodsByCategory[category].OrderBy(m => m.MethodName, StringComparer.Ordinal)];

        sb.AppendLine();
        sb.AppendLine($"### {category} ({members.Count})");
        sb.AppendLine();

        if (members.Count == 0)
        {
            sb.AppendLine("(none)");
            continue;
        }

        foreach (MethodRow row in members)
        {
            List<string> fieldsInvolved = [.. row.FieldsRead
                .Concat(row.FieldsWritten)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(f => f, StringComparer.Ordinal)];

            List<string> untrackedMembersInvolved = [.. row.UntrackedMainWindowMembersUsed];

            string why = category switch
            {
                "safe helper extraction" =>
                    "touches no tracked MainWindow fields, generated members, or MainWindow methods; a pure " +
                    "function of its parameters, safe to move as a static helper.",
                "possible service extraction" =>
                    $"touches field(s) owned mostly by this partial ({DescribeOrNone(fieldsInvolved)}); " +
                    "candidate for a scoped service once similar methods are grouped.",
                "keep in MainWindow because UI/XAML-bound" =>
                    "wired to a XAML event or shaped like an event handler; stays in MainWindow until the view itself is extracted.",
                "keep in MainWindow because it touches UI/generated MainWindow members" =>
                    $"references untracked MainWindow member(s) ({DescribeOrNone(untrackedMembersInvolved)}) — " +
                    "likely XAML-generated controls or bound UI properties; not safe to call \"pure\" or extract " +
                    "as a plain helper until those are abstracted behind an interface.",
                "high-risk due to shared state" =>
                    $"touches field(s) shared broadly across MainWindow ({DescribeOrNone(fieldsInvolved)}); " +
                    "moving it risks splitting shared state across files.",
                _ => string.Empty,
            };

            sb.AppendLine($"- **{row.MethodName}** — fields: {DescribeOrNone(fieldsInvolved)} — {why}");
        }
    }

    // 7. Risk summary
    int highRiskCount = methodsByCategory["high-risk due to shared state"].Count;
    int serviceCount = methodsByCategory["possible service extraction"].Count;
    int uiMemberCount = methodsByCategory["keep in MainWindow because it touches UI/generated MainWindow members"].Count;

    string overallRisk = highRiskCount > 0
        ? "high"
        : serviceCount > 0 || uiMemberCount > 0
            ? "medium"
            : "low";

    string reason = overallRisk switch
    {
        "high" =>
            $"{highRiskCount} method(s) touch fields shared broadly across MainWindow; extracting this partial " +
            "requires resolving that shared state first.",
        "medium" =>
            $"{serviceCount} method(s) are plausible service candidates and {uiMemberCount} method(s) touch " +
            "UI/generated MainWindow members; a scoped extraction pass is warranted before moving anything.",
        _ =>
            "no methods touch broadly-shared fields or UI/generated members, and none are plausible service " +
            "candidates beyond safe helpers; this partial looks low-risk to extract from.",
    };

    sb.AppendLine();
    sb.AppendLine("## Risk Summary");
    sb.AppendLine();
    sb.AppendLine($"- **Risk level:** {overallRisk}");
    sb.AppendLine($"- **Reasons:** {reason}");

    string outputName = $"focus-{StripPartialName(focusFile)}.md";
    File.WriteAllText(Path.Combine(reportsDir, outputName), sb.ToString());
}

static string DescribeOrNone(IEnumerable<string> items)
{
    List<string> list = [.. items];
    return list.Count == 0 ? "(none)" : JoinList(list);
}

static void AppendFieldBucket(StringBuilder sb, List<FocusFieldInfo> fields, string bucket)
{
    List<string> names = [.. fields.Where(f => f.Bucket == bucket).Select(f => f.Name)];

    if (names.Count == 0)
    {
        sb.AppendLine("(none)");
        return;
    }

    foreach (string name in names)
    {
        sb.AppendLine($"- {name}");
    }
}

// ===========================================================================
// Types
// ===========================================================================

internal sealed record PartialPart(SyntaxTree Tree, ClassDeclarationSyntax ClassDecl, string FilePath, string FileName);

internal sealed class MethodRow(string PartialFile, string MethodName, bool IsPublic, bool IsAsync)
{
    public string PartialFile { get; } = PartialFile;
    public string MethodName { get; } = MethodName;
    public bool IsPublic { get; } = IsPublic;
    public bool IsAsync { get; } = IsAsync;
    public bool IsEventHandlerLike { get; set; }
    public SortedSet<string> FieldsRead { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> FieldsWritten { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> MethodsCalled { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> ExternalTypes { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> AwaitedCalls { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> XamlEvents { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> UntrackedMainWindowMembersUsed { get; } = new(StringComparer.Ordinal);
    public List<MethodRow> CalledRows { get; } = [];
}

internal sealed class FieldUsage(string DeclaredInFile)
{
    public string DeclaredInFile { get; } = DeclaredInFile;
    public SortedSet<string> ReadBy { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> WrittenBy { get; } = new(StringComparer.Ordinal);
    public List<MethodRow> ReaderRows { get; } = [];
    public List<MethodRow> WriterRows { get; } = [];
    public int ReadCount { get; set; }
    public int WriteCount { get; set; }
}

internal sealed record XamlHandlerRow(string XamlFile, string ElementName, string EventName, string HandlerName, string HandlerPartialFile);

internal sealed record FocusFieldInfo(string Name, string Bucket);

internal sealed record ExtractionCandidate(
    string Name,
    string LikelySourcePartial,
    List<string> Methods,
    List<string> Fields,
    List<string> OutsideDependencies,
    string Risk,
    string Why,
    bool IsBackgroundArtifact);
