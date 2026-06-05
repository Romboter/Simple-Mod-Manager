using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length != 2)
{
    PrintUsage();
    return 2;
}

string sourceFilePath = Path.GetFullPath(args[0]);
string splitPlanPath = Path.GetFullPath(args[1]);

if (!File.Exists(sourceFilePath))
{
    Console.Error.WriteLine(
        $"Source file not found: {sourceFilePath}");

    return 2;
}

if (!File.Exists(splitPlanPath))
{
    Console.Error.WriteLine(
        $"Split-plan file not found: {splitPlanPath}");

    return 2;
}

string sourceCode;
string splitPlanText;

try
{
    sourceCode = await File.ReadAllTextAsync(
        sourceFilePath);

    splitPlanText = await File.ReadAllTextAsync(
        splitPlanPath);
}
catch (Exception exception)
{
    Console.Error.WriteLine(
        $"Unable to read an input file: {exception.Message}");

    return 2;
}

SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
    sourceCode,
    path: sourceFilePath);

List<Diagnostic> syntaxErrors = syntaxTree
    .GetDiagnostics()
    .Where(diagnostic =>
        diagnostic.Severity == DiagnosticSeverity.Error)
    .ToList();

if (syntaxErrors.Count > 0)
{
    Console.Error.WriteLine(
        $"Roslyn found {syntaxErrors.Count} syntax error(s):");

    foreach (Diagnostic diagnostic in syntaxErrors)
    {
        Console.Error.WriteLine(diagnostic);
    }

    return 2;
}

SyntaxNode root = await syntaxTree.GetRootAsync();

ClassDeclarationSyntax? mainWindowClass = root
    .DescendantNodes()
    .OfType<ClassDeclarationSyntax>()
    .FirstOrDefault(classDeclaration =>
        classDeclaration.Identifier.ValueText == "MainWindow");

if (mainWindowClass is null)
{
    Console.Error.WriteLine(
        $"Could not find class MainWindow in: {sourceFilePath}");

    return 2;
}

List<SourceMember> sourceMembers =
    BuildSourceMemberInventory(
        syntaxTree,
        mainWindowClass);

List<SplitPlanEntry> planEntries;

try
{
    planEntries = ParseSplitPlan(splitPlanText);
}
catch (Exception exception)
{
    Console.Error.WriteLine(
        $"Unable to parse split plan: {exception.Message}");

    return 2;
}

ValidationResult validation =
    ValidatePlan(
        sourceMembers,
        planEntries);

PrintValidationResult(
    sourceFilePath,
    splitPlanPath,
    validation);

return validation.IsValid
    ? 0
    : 1;

static void PrintUsage()
{
    Console.WriteLine(
        "Usage:");

    Console.WriteLine(
        "  SplitPlanValidator " +
        "<MainWindow.xaml.cs> " +
        "<MainWindow-split-plan.csv>");
}

static List<SourceMember> BuildSourceMemberInventory(
    SyntaxTree syntaxTree,
    ClassDeclarationSyntax mainWindowClass)
{
    List<SourceMember> result = [];

    foreach (MemberDeclarationSyntax member in mainWindowClass.Members)
    {
        FileLinePositionSpan location =
            syntaxTree.GetLineSpan(member.Span);

        int startLine =
            location.StartLinePosition.Line + 1;

        int endLine =
            location.EndLinePosition.Line + 1;

        string memberType =
            GetMemberType(member);

        string name =
            GetMemberName(member);

        string signature =
            GetMemberSignature(member);

        string memberId =
            BuildMemberId(
                memberType,
                signature,
                startLine);

        result.Add(
            new SourceMember(
                MemberId: memberId,
                Type: memberType,
                Name: name,
                Signature: signature,
                StartLine: startLine,
                EndLine: endLine,
                LineCount: endLine - startLine + 1));
    }

    return result;
}

static List<SplitPlanEntry> ParseSplitPlan(
    string csvText)
{
    List<List<string>> rows =
        ParseCsv(csvText);

    if (rows.Count == 0)
    {
        throw new InvalidOperationException(
            "The CSV file is empty.");
    }

    List<string> headers = rows[0];

    int memberIdIndex =
        FindRequiredColumn(headers, "MemberId");

    int typeIndex =
        FindRequiredColumn(headers, "Type");

    int nameIndex =
        FindRequiredColumn(headers, "Name");

    int signatureIndex =
        FindRequiredColumn(headers, "Signature");

    int startLineIndex =
        FindRequiredColumn(headers, "StartLine");

    int endLineIndex =
        FindRequiredColumn(headers, "EndLine");

    int lineCountIndex =
        FindRequiredColumn(headers, "LineCount");

    int suggestedFileIndex =
        FindRequiredColumn(headers, "SuggestedFile");

    int confidenceIndex =
        FindRequiredColumn(headers, "Confidence");

    int reasonIndex =
        FindRequiredColumn(headers, "Reason");

    List<SplitPlanEntry> result = [];

    for (int rowIndex = 1;
         rowIndex < rows.Count;
         rowIndex++)
    {
        List<string> row = rows[rowIndex];

        if (row.Count == 1 &&
            string.IsNullOrWhiteSpace(row[0]))
        {
            continue;
        }

        if (row.Count != headers.Count)
        {
            throw new InvalidOperationException(
                $"CSV row {rowIndex + 1} has {row.Count} columns, " +
                $"but the header has {headers.Count} columns.");
        }

        string memberId =
            row[memberIdIndex].Trim();

        if (string.IsNullOrWhiteSpace(memberId))
        {
            throw new InvalidOperationException(
                $"CSV row {rowIndex + 1} has an empty MemberId.");
        }

        result.Add(
            new SplitPlanEntry(
                CsvRowNumber: rowIndex + 1,
                MemberId: memberId,
                Type: row[typeIndex].Trim(),
                Name: row[nameIndex].Trim(),
                Signature: row[signatureIndex].Trim(),
                StartLine: ParseRequiredInteger(
                    row[startLineIndex],
                    rowIndex + 1,
                    "StartLine"),
                EndLine: ParseRequiredInteger(
                    row[endLineIndex],
                    rowIndex + 1,
                    "EndLine"),
                LineCount: ParseRequiredInteger(
                    row[lineCountIndex],
                    rowIndex + 1,
                    "LineCount"),
                SuggestedFile:
                    row[suggestedFileIndex].Trim(),
                Confidence:
                    row[confidenceIndex].Trim(),
                Reason:
                    row[reasonIndex].Trim()));
    }

    return result;
}

static ValidationResult ValidatePlan(
    IReadOnlyCollection<SourceMember> sourceMembers,
    IReadOnlyCollection<SplitPlanEntry> planEntries)
{
    Dictionary<string, SourceMember> sourceById =
        sourceMembers.ToDictionary(
            member => member.MemberId,
            StringComparer.Ordinal);

    Dictionary<string, List<SplitPlanEntry>> planById =
        planEntries
            .GroupBy(
                entry => entry.MemberId,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.Ordinal);

    List<ValidationProblem> problems = [];

    foreach ((string memberId, List<SplitPlanEntry> entries)
             in planById)
    {
        if (entries.Count > 1)
        {
            string rows = string.Join(
                ", ",
                entries.Select(entry =>
                    entry.CsvRowNumber));

            problems.Add(
                new ValidationProblem(
                    Category: "Duplicate plan member",
                    MemberId: memberId,
                    Details:
                        $"Member appears {entries.Count} times " +
                        $"on CSV rows: {rows}."));
        }
    }

    foreach (SourceMember sourceMember in sourceMembers)
    {
        if (!planById.ContainsKey(sourceMember.MemberId))
        {
            problems.Add(
                new ValidationProblem(
                    Category: "Missing from plan",
                    MemberId: sourceMember.MemberId,
                    Details:
                        $"{sourceMember.Type} " +
                        $"{sourceMember.Signature} at " +
                        $"lines {sourceMember.StartLine}-" +
                        $"{sourceMember.EndLine}."));
        }
    }

    foreach (SplitPlanEntry planEntry in planEntries)
    {
        if (!sourceById.TryGetValue(
                planEntry.MemberId,
                out SourceMember? sourceMember))
        {
            problems.Add(
                new ValidationProblem(
                    Category: "Unknown plan member",
                    MemberId: planEntry.MemberId,
                    Details:
                        $"CSV row {planEntry.CsvRowNumber} does not " +
                        "match any current MainWindow member."));

            continue;
        }

        ValidateEntryMatchesSource(
            planEntry,
            sourceMember,
            problems);

        ValidateSuggestedFile(
            planEntry,
            problems);
    }

    Dictionary<string, int> targetFileCounts =
        planEntries
            .GroupBy(
                entry => entry.SuggestedFile,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.OrdinalIgnoreCase);

    return new ValidationResult(
        SourceMemberCount: sourceMembers.Count,
        PlanEntryCount: planEntries.Count,
        UniquePlanMemberCount: planById.Count,
        TargetFileCounts: targetFileCounts,
        Problems: problems);
}

static void ValidateEntryMatchesSource(
    SplitPlanEntry planEntry,
    SourceMember sourceMember,
    ICollection<ValidationProblem> problems)
{
    List<string> mismatches = [];

    if (!string.Equals(
            planEntry.Type,
            sourceMember.Type,
            StringComparison.Ordinal))
    {
        mismatches.Add(
            $"Type plan='{planEntry.Type}' " +
            $"source='{sourceMember.Type}'");
    }

    if (!string.Equals(
            planEntry.Name,
            sourceMember.Name,
            StringComparison.Ordinal))
    {
        mismatches.Add(
            $"Name plan='{planEntry.Name}' " +
            $"source='{sourceMember.Name}'");
    }

    if (!string.Equals(
            planEntry.Signature,
            sourceMember.Signature,
            StringComparison.Ordinal))
    {
        mismatches.Add(
            $"Signature plan='{planEntry.Signature}' " +
            $"source='{sourceMember.Signature}'");
    }

    if (planEntry.StartLine != sourceMember.StartLine)
    {
        mismatches.Add(
            $"StartLine plan={planEntry.StartLine} " +
            $"source={sourceMember.StartLine}");
    }

    if (planEntry.EndLine != sourceMember.EndLine)
    {
        mismatches.Add(
            $"EndLine plan={planEntry.EndLine} " +
            $"source={sourceMember.EndLine}");
    }

    if (planEntry.LineCount != sourceMember.LineCount)
    {
        mismatches.Add(
            $"LineCount plan={planEntry.LineCount} " +
            $"source={sourceMember.LineCount}");
    }

    if (mismatches.Count == 0)
    {
        return;
    }

    problems.Add(
        new ValidationProblem(
            Category: "Plan/source mismatch",
            MemberId: planEntry.MemberId,
            Details:
                $"CSV row {planEntry.CsvRowNumber}: " +
                string.Join("; ", mismatches)));
}

static void ValidateSuggestedFile(
    SplitPlanEntry planEntry,
    ICollection<ValidationProblem> problems)
{
    string suggestedFile =
        planEntry.SuggestedFile;

    if (string.IsNullOrWhiteSpace(suggestedFile))
    {
        problems.Add(
            new ValidationProblem(
                Category: "Invalid target file",
                MemberId: planEntry.MemberId,
                Details:
                    $"CSV row {planEntry.CsvRowNumber} has " +
                    "an empty SuggestedFile."));

        return;
    }

    if (!suggestedFile.EndsWith(
            ".cs",
            StringComparison.OrdinalIgnoreCase))
    {
        problems.Add(
            new ValidationProblem(
                Category: "Invalid target file",
                MemberId: planEntry.MemberId,
                Details:
                    $"CSV row {planEntry.CsvRowNumber}: " +
                    $"'{suggestedFile}' does not end in .cs."));
    }

    if (!suggestedFile.StartsWith(
            "MainWindow.",
            StringComparison.Ordinal))
    {
        problems.Add(
            new ValidationProblem(
                Category: "Invalid target file",
                MemberId: planEntry.MemberId,
                Details:
                    $"CSV row {planEntry.CsvRowNumber}: " +
                    $"'{suggestedFile}' must start with MainWindow."));
    }

    char[] invalidFileNameCharacters =
        Path.GetInvalidFileNameChars();

    if (suggestedFile.IndexOfAny(
            invalidFileNameCharacters) >= 0)
    {
        problems.Add(
            new ValidationProblem(
                Category: "Invalid target file",
                MemberId: planEntry.MemberId,
                Details:
                    $"CSV row {planEntry.CsvRowNumber}: " +
                    $"'{suggestedFile}' contains invalid " +
                    "filename characters."));
    }

    if (string.Equals(
            suggestedFile,
            "MainWindow.Unassigned.cs",
            StringComparison.OrdinalIgnoreCase))
    {
        problems.Add(
            new ValidationProblem(
                Category: "Unassigned member",
                MemberId: planEntry.MemberId,
                Details:
                    $"CSV row {planEntry.CsvRowNumber} remains " +
                    "assigned to MainWindow.Unassigned.cs."));
    }

    if (string.Equals(
            planEntry.Confidence,
            "Low",
            StringComparison.OrdinalIgnoreCase))
    {
        problems.Add(
            new ValidationProblem(
                Category: "Low-confidence member",
                MemberId: planEntry.MemberId,
                Details:
                    $"CSV row {planEntry.CsvRowNumber} is marked " +
                    "with Low confidence."));
    }
}

static void PrintValidationResult(
    string sourceFilePath,
    string splitPlanPath,
    ValidationResult validation)
{
    Console.WriteLine("SPLIT PLAN VALIDATION");
    Console.WriteLine("=====================");
    Console.WriteLine();
    Console.WriteLine($"Source: {sourceFilePath}");
    Console.WriteLine($"Plan:   {splitPlanPath}");
    Console.WriteLine();

    Console.WriteLine(
        $"Source members:      {validation.SourceMemberCount}");

    Console.WriteLine(
        $"Plan entries:        {validation.PlanEntryCount}");

    Console.WriteLine(
        $"Unique plan members: {validation.UniquePlanMemberCount}");

    Console.WriteLine(
        $"Target files:        {validation.TargetFileCounts.Count}");

    Console.WriteLine(
        $"Problems:            {validation.Problems.Count}");

    Console.WriteLine();

    Console.WriteLine("TARGET FILES");
    Console.WriteLine("------------");

    foreach ((string fileName, int count)
             in validation.TargetFileCounts
                 .OrderBy(
                     item => item.Key,
                     StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine(
            $"{fileName,-42} {count,4} members");
    }

    Console.WriteLine();

    if (validation.IsValid)
    {
        Console.WriteLine(
            "VALIDATION PASSED");

        Console.WriteLine(
            "Every MainWindow member is represented exactly once, " +
            "and every plan entry matches the current source.");

        return;
    }

    Console.WriteLine("VALIDATION FAILED");
    Console.WriteLine("-----------------");

    foreach (IGrouping<string, ValidationProblem> group
             in validation.Problems
                 .GroupBy(problem => problem.Category)
                 .OrderBy(
                     group => group.Key,
                     StringComparer.Ordinal))
    {
        Console.WriteLine();
        Console.WriteLine(
            $"{group.Key} ({group.Count()})");

        foreach (ValidationProblem problem in group)
        {
            Console.WriteLine(
                $"  {problem.MemberId}");

            Console.WriteLine(
                $"    {problem.Details}");
        }
    }
}

static int FindRequiredColumn(
    IReadOnlyList<string> headers,
    string requiredName)
{
    for (int index = 0;
         index < headers.Count;
         index++)
    {
        if (string.Equals(
                headers[index].Trim(),
                requiredName,
                StringComparison.Ordinal))
        {
            return index;
        }
    }

    throw new InvalidOperationException(
        $"Required CSV column '{requiredName}' was not found.");
}

static int ParseRequiredInteger(
    string value,
    int csvRowNumber,
    string columnName)
{
    if (int.TryParse(
            value,
            out int result))
    {
        return result;
    }

    throw new InvalidOperationException(
        $"CSV row {csvRowNumber} contains invalid " +
        $"{columnName} value '{value}'.");
}

static List<List<string>> ParseCsv(
    string csvText)
{
    List<List<string>> rows = [];
    List<string> currentRow = [];
    StringBuilder currentValue = new();

    bool insideQuotes = false;

    for (int index = 0;
         index < csvText.Length;
         index++)
    {
        char character = csvText[index];

        if (insideQuotes)
        {
            if (character == '"')
            {
                bool escapedQuote =
                    index + 1 < csvText.Length &&
                    csvText[index + 1] == '"';

                if (escapedQuote)
                {
                    currentValue.Append('"');
                    index++;
                }
                else
                {
                    insideQuotes = false;
                }
            }
            else
            {
                currentValue.Append(character);
            }

            continue;
        }

        switch (character)
        {
            case '"':
                insideQuotes = true;
                break;

            case ',':
                currentRow.Add(
                    currentValue.ToString());

                currentValue.Clear();
                break;

            case '\r':
                if (index + 1 < csvText.Length &&
                    csvText[index + 1] == '\n')
                {
                    index++;
                }

                FinishCsvRow(
                    rows,
                    currentRow,
                    currentValue);
                break;

            case '\n':
                FinishCsvRow(
                    rows,
                    currentRow,
                    currentValue);
                break;

            default:
                currentValue.Append(character);
                break;
        }
    }

    if (insideQuotes)
    {
        throw new InvalidOperationException(
            "CSV ended inside a quoted field.");
    }

    if (currentValue.Length > 0 ||
        currentRow.Count > 0)
    {
        FinishCsvRow(
            rows,
            currentRow,
            currentValue);
    }

    return rows;
}

static void FinishCsvRow(
    ICollection<List<string>> rows,
    List<string> currentRow,
    StringBuilder currentValue)
{
    currentRow.Add(
        currentValue.ToString());

    currentValue.Clear();

    rows.Add(
        [.. currentRow]);

    currentRow.Clear();
}

static string BuildMemberId(
    string type,
    string signature,
    int startLine)
{
    return $"{type}|{signature}|L{startLine}";
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
            $"operator {operatorDeclaration.OperatorToken.ValueText}",

        ConversionOperatorDeclarationSyntax conversionOperator =>
            $"{conversionOperator.ImplicitOrExplicitKeyword.ValueText} operator",

        DestructorDeclarationSyntax destructor =>
            $"~{destructor.Identifier.ValueText}",

        _ =>
            "(unknown)"
    };
}

static string GetMemberSignature(
    MemberDeclarationSyntax member)
{
    return member switch
    {
        MethodDeclarationSyntax method =>
            GetMethodSignature(method),

        ConstructorDeclarationSyntax constructor =>
            $"{constructor.Identifier.ValueText}" +
            $"({GetParameterTypes(constructor.ParameterList)})",

        PropertyDeclarationSyntax property =>
            property.Identifier.ValueText,

        FieldDeclarationSyntax field =>
            string.Join(
                ",",
                field.Declaration.Variables.Select(
                    variable =>
                        variable.Identifier.ValueText)),

        EventFieldDeclarationSyntax eventField =>
            string.Join(
                ",",
                eventField.Declaration.Variables.Select(
                    variable =>
                        variable.Identifier.ValueText)),

        EventDeclarationSyntax eventDeclaration =>
            eventDeclaration.Identifier.ValueText,

        BaseTypeDeclarationSyntax typeDeclaration =>
            typeDeclaration.Identifier.ValueText,

        DelegateDeclarationSyntax delegateDeclaration =>
            $"{delegateDeclaration.Identifier.ValueText}" +
            $"({GetParameterTypes(delegateDeclaration.ParameterList)})",

        IndexerDeclarationSyntax indexer =>
            $"this[{GetBracketedParameterTypes(indexer.ParameterList)}]",

        OperatorDeclarationSyntax operatorDeclaration =>
            $"operator {operatorDeclaration.OperatorToken.ValueText}" +
            $"({GetParameterTypes(operatorDeclaration.ParameterList)})",

        ConversionOperatorDeclarationSyntax conversionOperator =>
            $"{conversionOperator.ImplicitOrExplicitKeyword.ValueText} " +
            $"operator {conversionOperator.Type}" +
            $"({GetParameterTypes(conversionOperator.ParameterList)})",

        DestructorDeclarationSyntax destructor =>
            $"~{destructor.Identifier.ValueText}()",

        _ =>
            GetMemberName(member)
    };
}

static string GetMethodSignature(
    MethodDeclarationSyntax method)
{
    string genericParameters =
        method.TypeParameterList is null
            ? string.Empty
            : method.TypeParameterList.ToString();

    return $"{method.Identifier.ValueText}" +
           $"{genericParameters}" +
           $"({GetParameterTypes(method.ParameterList)})";
}

static string GetParameterTypes(
    ParameterListSyntax parameterList)
{
    return string.Join(
        ",",
        parameterList.Parameters.Select(
            GetParameterType));
}

static string GetBracketedParameterTypes(
    BracketedParameterListSyntax parameterList)
{
    return string.Join(
        ",",
        parameterList.Parameters.Select(
            GetParameterType));
}

static string GetParameterType(
    ParameterSyntax parameter)
{
    string modifiers =
        parameter.Modifiers.ToString();

    string type =
        parameter.Type?.ToString() ?? "?";

    return string.IsNullOrWhiteSpace(modifiers)
        ? type
        : $"{modifiers} {type}";
}

internal sealed record SourceMember(
    string MemberId,
    string Type,
    string Name,
    string Signature,
    int StartLine,
    int EndLine,
    int LineCount);

internal sealed record SplitPlanEntry(
    int CsvRowNumber,
    string MemberId,
    string Type,
    string Name,
    string Signature,
    int StartLine,
    int EndLine,
    int LineCount,
    string SuggestedFile,
    string Confidence,
    string Reason);

internal sealed record ValidationProblem(
    string Category,
    string MemberId,
    string Details);

internal sealed record ValidationResult(
    int SourceMemberCount,
    int PlanEntryCount,
    int UniquePlanMemberCount,
    IReadOnlyDictionary<string, int> TargetFileCounts,
    IReadOnlyList<ValidationProblem> Problems)
{
    public bool IsValid =>
        Problems.Count == 0 &&
        SourceMemberCount == PlanEntryCount &&
        SourceMemberCount == UniquePlanMemberCount;
}