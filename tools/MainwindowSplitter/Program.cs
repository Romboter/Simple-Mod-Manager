using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length < 1 || args.Length > 2)
{
    Console.Error.WriteLine(
        "Usage: MainWindowSplitter <path-to-MainWindow.xaml.cs> [output.csv]");

    return 1;
}

string sourceFilePath = Path.GetFullPath(args[0]);

if (!File.Exists(sourceFilePath))
{
    Console.Error.WriteLine($"File not found: {sourceFilePath}");
    return 1;
}

string? outputFilePath = args.Length == 2
    ? Path.GetFullPath(args[1])
    : null;

string sourceCode;

try
{
    sourceCode = await File.ReadAllTextAsync(sourceFilePath);
}
catch (Exception exception)
{
    Console.Error.WriteLine(
        $"Unable to read '{sourceFilePath}': {exception.Message}");

    return 1;
}

SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
    sourceCode,
    path: sourceFilePath);

SyntaxNode root = await syntaxTree.GetRootAsync();

List<Diagnostic> parseErrors = syntaxTree
    .GetDiagnostics()
    .Where(diagnostic =>
        diagnostic.Severity == DiagnosticSeverity.Error)
    .ToList();

if (parseErrors.Count > 0)
{
    Console.Error.WriteLine(
        $"Roslyn found {parseErrors.Count} syntax error(s):");

    foreach (Diagnostic diagnostic in parseErrors)
    {
        Console.Error.WriteLine(diagnostic);
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine(
        "No inventory was generated because the source could not be parsed safely.");

    return 1;
}

ClassDeclarationSyntax? mainWindowClass = root
    .DescendantNodes()
    .OfType<ClassDeclarationSyntax>()
    .FirstOrDefault(classDeclaration =>
        classDeclaration.Identifier.ValueText == "MainWindow");

if (mainWindowClass is null)
{
    Console.Error.WriteLine(
        $"Could not find a class named MainWindow in '{sourceFilePath}'.");

    return 1;
}

List<MemberInventoryItem> inventory =
    BuildMemberInventory(
        syntaxTree,
        mainWindowClass);

List<MethodCallInventoryItem> methodCalls =
    BuildMethodCallInventory(
        syntaxTree,
        mainWindowClass);

PrintInventory(
    sourceFilePath,
    syntaxTree,
    mainWindowClass,
    inventory);

if (outputFilePath is not null)
{
    try
    {
        string outputDirectory =
            Path.GetDirectoryName(outputFilePath)
            ?? Directory.GetCurrentDirectory();

        Directory.CreateDirectory(outputDirectory);

        await WriteMemberInventoryCsvAsync(
            outputFilePath,
            inventory);

        string outputName =
            Path.GetFileNameWithoutExtension(outputFilePath);

        string methodCallsPath = Path.Combine(
            outputDirectory,
            $"{outputName}-calls.csv");

        await WriteMethodCallsCsvAsync(
            methodCallsPath,
            methodCalls);

        Console.WriteLine();
        Console.WriteLine(
            $"Member inventory CSV written to: {outputFilePath}");

        Console.WriteLine(
            $"Method call CSV written to:     {methodCallsPath}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(
            $"Unable to write output files: {exception.Message}");

        return 1;
    }
}

return 0;

static List<MemberInventoryItem> BuildMemberInventory(
    SyntaxTree syntaxTree,
    ClassDeclarationSyntax mainWindowClass)
{
    List<MemberInventoryItem> inventory = [];

    foreach (MemberDeclarationSyntax member in mainWindowClass.Members)
    {
        FileLinePositionSpan location =
            syntaxTree.GetLineSpan(member.Span);

        int startLine =
            location.StartLinePosition.Line + 1;

        int endLine =
            location.EndLinePosition.Line + 1;

        inventory.Add(
            new MemberInventoryItem(
                Type: GetMemberType(member),
                Name: GetMemberName(member),
                StartLine: startLine,
                EndLine: endLine,
                LineCount: endLine - startLine + 1,
                Modifiers: GetModifiers(member),
                ReturnType: GetReturnType(member),
                Parameters: GetParameters(member)));
    }

    return inventory;
}

static List<MethodCallInventoryItem> BuildMethodCallInventory(
    SyntaxTree syntaxTree,
    ClassDeclarationSyntax mainWindowClass)
{
    List<MethodDeclarationSyntax> methods = mainWindowClass
        .Members
        .OfType<MethodDeclarationSyntax>()
        .ToList();

    HashSet<string> mainWindowMethodNames = methods
        .Select(method =>
            method.Identifier.ValueText)
        .ToHashSet(StringComparer.Ordinal);

    List<MethodCallInventoryItem> methodCalls = [];

    foreach (MethodDeclarationSyntax method in methods)
    {
        FileLinePositionSpan location =
            syntaxTree.GetLineSpan(method.Span);

        int startLine =
            location.StartLinePosition.Line + 1;

        int endLine =
            location.EndLinePosition.Line + 1;

        List<string> calledMethods = method
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(GetInvokedMethodName)
            .Where(name =>
                name is not null &&
                mainWindowMethodNames.Contains(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(
                name => name,
                StringComparer.Ordinal)
            .ToList();

        methodCalls.Add(
            new MethodCallInventoryItem(
                Name: method.Identifier.ValueText,
                StartLine: startLine,
                EndLine: endLine,
                LineCount: endLine - startLine + 1,
                Calls: calledMethods));
    }

    return methodCalls;
}

static void PrintInventory(
    string sourceFilePath,
    SyntaxTree syntaxTree,
    ClassDeclarationSyntax classDeclaration,
    IReadOnlyCollection<MemberInventoryItem> inventory)
{
    FileLinePositionSpan classLocation =
        syntaxTree.GetLineSpan(classDeclaration.Span);

    int classStartLine =
        classLocation.StartLinePosition.Line + 1;

    int classEndLine =
        classLocation.EndLinePosition.Line + 1;

    Console.WriteLine($"Source:  {sourceFilePath}");
    Console.WriteLine(
        $"Class:   {classDeclaration.Identifier.ValueText}");

    Console.WriteLine(
        $"Lines:   {classStartLine}-{classEndLine}");

    Console.WriteLine($"Members: {inventory.Count}");
    Console.WriteLine();

    Console.WriteLine(
        $"{"Type",-20} " +
        $"{"Name",-50} " +
        $"{"Start",8} " +
        $"{"End",8} " +
        $"{"Lines",8}");

    Console.WriteLine(new string('-', 100));

    foreach (MemberInventoryItem item in inventory)
    {
        Console.WriteLine(
            $"{item.Type,-20} " +
            $"{Truncate(item.Name, 50),-50} " +
            $"{item.StartLine,8} " +
            $"{item.EndLine,8} " +
            $"{item.LineCount,8}");
    }
}

static async Task WriteMemberInventoryCsvAsync(
    string outputFilePath,
    IEnumerable<MemberInventoryItem> inventory)
{
    StringBuilder csv = new();

    csv.AppendLine(
        "Type,Name,StartLine,EndLine,LineCount," +
        "Modifiers,ReturnType,Parameters");

    foreach (MemberInventoryItem item in inventory)
    {
        csv.AppendLine(
            string.Join(
                ",",
                EscapeCsv(item.Type),
                EscapeCsv(item.Name),
                item.StartLine,
                item.EndLine,
                item.LineCount,
                EscapeCsv(item.Modifiers),
                EscapeCsv(item.ReturnType),
                EscapeCsv(item.Parameters)));
    }

    await File.WriteAllTextAsync(
        outputFilePath,
        csv.ToString(),
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false));
}

static async Task WriteMethodCallsCsvAsync(
    string outputFilePath,
    IEnumerable<MethodCallInventoryItem> methodCalls)
{
    StringBuilder csv = new();

    csv.AppendLine(
        "Name,StartLine,EndLine,LineCount,Calls,CallCount");

    foreach (MethodCallInventoryItem method in methodCalls)
    {
        string calls = string.Join(
            ";",
            method.Calls);

        csv.AppendLine(
            string.Join(
                ",",
                EscapeCsv(method.Name),
                method.StartLine,
                method.EndLine,
                method.LineCount,
                EscapeCsv(calls),
                method.Calls.Count));
    }

    await File.WriteAllTextAsync(
        outputFilePath,
        csv.ToString(),
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false));
}

static string GetMemberType(
    MemberDeclarationSyntax member)
{
    return member switch
    {
        FieldDeclarationSyntax =>
            "Field",

        PropertyDeclarationSyntax =>
            "Property",

        ConstructorDeclarationSyntax =>
            "Constructor",

        MethodDeclarationSyntax =>
            "Method",

        EventFieldDeclarationSyntax =>
            "Event",

        EventDeclarationSyntax =>
            "Event",

        ClassDeclarationSyntax =>
            "Nested class",

        StructDeclarationSyntax =>
            "Nested struct",

        RecordDeclarationSyntax =>
            "Nested record",

        EnumDeclarationSyntax =>
            "Nested enum",

        DelegateDeclarationSyntax =>
            "Delegate",

        IndexerDeclarationSyntax =>
            "Indexer",

        OperatorDeclarationSyntax =>
            "Operator",

        ConversionOperatorDeclarationSyntax =>
            "Conversion operator",

        DestructorDeclarationSyntax =>
            "Destructor",

        _ =>
            member.Kind().ToString()
    };
}

static string GetMemberName(
    MemberDeclarationSyntax member)
{
    return member switch
    {
        FieldDeclarationSyntax field =>
            string.Join(
                ", ",
                field.Declaration.Variables.Select(
                    variable =>
                        variable.Identifier.ValueText)),

        PropertyDeclarationSyntax property =>
            property.Identifier.ValueText,

        ConstructorDeclarationSyntax constructor =>
            constructor.Identifier.ValueText,

        MethodDeclarationSyntax method =>
            method.Identifier.ValueText,

        EventFieldDeclarationSyntax eventField =>
            string.Join(
                ", ",
                eventField.Declaration.Variables.Select(
                    variable =>
                        variable.Identifier.ValueText)),

        EventDeclarationSyntax eventDeclaration =>
            eventDeclaration.Identifier.ValueText,

        ClassDeclarationSyntax nestedClass =>
            nestedClass.Identifier.ValueText,

        StructDeclarationSyntax nestedStruct =>
            nestedStruct.Identifier.ValueText,

        RecordDeclarationSyntax nestedRecord =>
            nestedRecord.Identifier.ValueText,

        EnumDeclarationSyntax nestedEnum =>
            nestedEnum.Identifier.ValueText,

        DelegateDeclarationSyntax delegateDeclaration =>
            delegateDeclaration.Identifier.ValueText,

        IndexerDeclarationSyntax =>
            "this[]",

        OperatorDeclarationSyntax operatorDeclaration =>
            $"operator " +
            $"{operatorDeclaration.OperatorToken.ValueText}",

        ConversionOperatorDeclarationSyntax conversionOperator =>
            $"{conversionOperator.ImplicitOrExplicitKeyword.ValueText} " +
            "operator",

        DestructorDeclarationSyntax destructor =>
            $"~{destructor.Identifier.ValueText}",

        _ =>
            "(unknown)"
    };
}

static string GetModifiers(
    MemberDeclarationSyntax member)
{
    return member switch
    {
        BaseFieldDeclarationSyntax field =>
            field.Modifiers.ToString(),

        BaseMethodDeclarationSyntax method =>
            method.Modifiers.ToString(),

        BasePropertyDeclarationSyntax property =>
            property.Modifiers.ToString(),

        BaseTypeDeclarationSyntax type =>
            type.Modifiers.ToString(),

        DelegateDeclarationSyntax delegateDeclaration =>
            delegateDeclaration.Modifiers.ToString(),

        _ =>
            string.Empty
    };
}

static string GetReturnType(
    MemberDeclarationSyntax member)
{
    return member switch
    {
        MethodDeclarationSyntax method =>
            method.ReturnType.ToString(),

        PropertyDeclarationSyntax property =>
            property.Type.ToString(),

        FieldDeclarationSyntax field =>
            field.Declaration.Type.ToString(),

        EventFieldDeclarationSyntax eventField =>
            eventField.Declaration.Type.ToString(),

        EventDeclarationSyntax eventDeclaration =>
            eventDeclaration.Type.ToString(),

        OperatorDeclarationSyntax operatorDeclaration =>
            operatorDeclaration.ReturnType.ToString(),

        ConversionOperatorDeclarationSyntax conversionOperator =>
            conversionOperator.Type.ToString(),

        _ =>
            string.Empty
    };
}

static string GetParameters(
    MemberDeclarationSyntax member)
{
    ParameterListSyntax? parameterList = member switch
    {
        MethodDeclarationSyntax method =>
            method.ParameterList,

        ConstructorDeclarationSyntax constructor =>
            constructor.ParameterList,

        OperatorDeclarationSyntax operatorDeclaration =>
            operatorDeclaration.ParameterList,

        ConversionOperatorDeclarationSyntax conversionOperator =>
            conversionOperator.ParameterList,

        DelegateDeclarationSyntax delegateDeclaration =>
            delegateDeclaration.ParameterList,

        DestructorDeclarationSyntax destructor =>
            destructor.ParameterList,

        _ =>
            null
    };

    if (parameterList is null)
    {
        return string.Empty;
    }

    return string.Join(
        ", ",
        parameterList.Parameters.Select(parameter =>
        {
            string modifiers = parameter.Modifiers.ToString();
            string type = parameter.Type?.ToString() ?? string.Empty;
            string name = parameter.Identifier.ValueText;

            string parameterText = string.Join(
                " ",
                new[]
                {
                    modifiers,
                    type,
                    name
                }
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value)));

            if (parameter.Default is not null)
            {
                parameterText +=
                    $" = {parameter.Default.Value}";
            }

            return parameterText;
        }));
}

static string? GetInvokedMethodName(
    InvocationExpressionSyntax invocation)
{
    return invocation.Expression switch
    {
        IdentifierNameSyntax identifier =>
            identifier.Identifier.ValueText,

        GenericNameSyntax genericName =>
            genericName.Identifier.ValueText,

        MemberAccessExpressionSyntax memberAccess =>
            GetSimpleName(memberAccess.Name),

        MemberBindingExpressionSyntax memberBinding =>
            GetSimpleName(memberBinding.Name),

        _ =>
            null
    };
}

static string? GetSimpleName(
    SimpleNameSyntax name)
{
    return name switch
    {
        IdentifierNameSyntax identifier =>
            identifier.Identifier.ValueText,

        GenericNameSyntax genericName =>
            genericName.Identifier.ValueText,

        _ =>
            null
    };
}

static string EscapeCsv(
    string value)
{
    if (!value.Contains(',') &&
        !value.Contains('"') &&
        !value.Contains('\r') &&
        !value.Contains('\n'))
    {
        return value;
    }

    return $"\"{value.Replace("\"", "\"\"")}\"";
}

static string Truncate(
    string value,
    int maximumLength)
{
    if (value.Length <= maximumLength)
    {
        return value;
    }

    return value[..(maximumLength - 3)] + "...";
}

internal sealed record MemberInventoryItem(
    string Type,
    string Name,
    int StartLine,
    int EndLine,
    int LineCount,
    string Modifiers,
    string ReturnType,
    string Parameters);

internal sealed record MethodCallInventoryItem(
    string Name,
    int StartLine,
    int EndLine,
    int LineCount,
    IReadOnlyList<string> Calls);