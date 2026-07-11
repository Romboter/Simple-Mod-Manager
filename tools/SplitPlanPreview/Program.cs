using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length != 3)
{
    PrintUsage();
    return 2;
}

string sourceFilePath = Path.GetFullPath(args[0]);
string splitPlanPath = Path.GetFullPath(args[1]);
string outputDirectoryPath = Path.GetFullPath(args[2]);

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

string sourceDirectoryPath =
    Path.GetDirectoryName(sourceFilePath)
    ?? throw new InvalidOperationException(
        "Unable to determine source directory.");

if (IsSameOrChildPath(
        outputDirectoryPath,
        sourceDirectoryPath))
{
    Console.Error.WriteLine(
        "The preview output directory cannot be inside the application " +
        "source directory.");

    Console.Error.WriteLine(
        $"Source directory: {sourceDirectoryPath}");

    Console.Error.WriteLine(
        $"Output directory: {outputDirectoryPath}");

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

CompilationUnitSyntax compilationUnit =
    (CompilationUnitSyntax)await syntaxTree.GetRootAsync();

ClassDeclarationSyntax? mainWindowClass = compilationUnit
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

string? namespaceName =
    GetNamespaceName(mainWindowClass);

if (string.IsNullOrWhiteSpace(namespaceName))
{
    Console.Error.WriteLine(
        "Could not determine the namespace containing MainWindow.");

    return 2;
}

List<SourceMember> sourceMembers =
    BuildSourceMemberInventory(
        syntaxTree,
        mainWindowClass);

List<SplitPlanEntry> planEntries;

try
{
    planEntries =
        ParseSplitPlan(splitPlanText);
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

PrintValidationSummary(validation);

if (!validation.IsValid)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine(
        "Preview generation stopped because split-plan validation failed.");

    return 1;
}

try
{
    PrepareOutputDirectory(outputDirectoryPath);
}
catch (Exception exception)
{
    Console.Error.WriteLine(
        $"Unable to prepare output directory: {exception.Message}");

    return 2;
}

Dictionary<string, SourceMember> sourceById =
    sourceMembers.ToDictionary(
        member => member.MemberId,
        StringComparer.Ordinal);

Dictionary<string, List<SplitPlanEntry>> entriesByTargetFile =
    planEntries
        .GroupBy(
            entry => entry.SuggestedFile,
            StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            group => group.Key,
            group => group
                .OrderBy(entry => entry.StartLine)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);

string generatedFileHeader =
    BuildGeneratedFileHeader(
        sourceFilePath,
        splitPlanPath);

string usingText =
    BuildUsingText(compilationUnit);

List<GeneratedFileResult> generatedFiles = [];
HashSet<string> writtenMemberIds =
    new(StringComparer.Ordinal);

foreach ((string targetFile, List<SplitPlanEntry> entries)
         in entriesByTargetFile
             .OrderBy(
                 item => item.Key,
                 StringComparer.OrdinalIgnoreCase))
{
    string targetPath =
        Path.Combine(
            outputDirectoryPath,
            targetFile);

    bool isCoreFile =
        string.Equals(
            targetFile,
            "MainWindow.Core.cs",
            StringComparison.OrdinalIgnoreCase);

    string generatedCode =
        BuildPartialClassFile(
            generatedFileHeader,
            usingText,
            namespaceName,
            mainWindowClass,
            entries,
            sourceById,
            isCoreFile,
            writtenMemberIds);

    await File.WriteAllTextAsync(
        targetPath,
        generatedCode,
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false));

    generatedFiles.Add(
        new GeneratedFileResult(
            FileName: targetFile,
            FullPath: targetPath,
            MemberCount: entries.Count,
            ApproximateSourceLines:
                entries.Sum(entry => entry.LineCount),
            ByteCount:
                new FileInfo(targetPath).Length,
            Sha256:
                CalculateSha256(targetPath)));
}

List<string> missingWrittenMembers = sourceMembers
    .Where(member =>
        !writtenMemberIds.Contains(member.MemberId))
    .Select(member =>
        member.MemberId)
    .ToList();

if (missingWrittenMembers.Count > 0)
{
    Console.Error.WriteLine(
        "Preview verification failed: some source members were not written.");

    foreach (string memberId in missingWrittenMembers)
    {
        Console.Error.WriteLine($"  {memberId}");
    }

    return 1;
}

if (writtenMemberIds.Count != sourceMembers.Count)
{
    Console.Error.WriteLine(
        "Preview verification failed: written member count does not " +
        "match source member count.");

    Console.Error.WriteLine(
        $"Source members:  {sourceMembers.Count}");

    Console.Error.WriteLine(
        $"Written members: {writtenMemberIds.Count}");

    return 1;
}

string manifestPath =
    Path.Combine(
        outputDirectoryPath,
        "preview-manifest.csv");

string summaryPath =
    Path.Combine(
        outputDirectoryPath,
        "preview-summary.txt");

await WriteManifestAsync(
    manifestPath,
    generatedFiles,
    entriesByTargetFile);

await WriteSummaryAsync(
    summaryPath,
    sourceFilePath,
    splitPlanPath,
    outputDirectoryPath,
    sourceMembers.Count,
    generatedFiles);

Console.WriteLine();
Console.WriteLine("PREVIEW GENERATED");
Console.WriteLine("=================");
Console.WriteLine();
Console.WriteLine($"Output:          {outputDirectoryPath}");
Console.WriteLine($"Generated files: {generatedFiles.Count}");
Console.WriteLine($"Members written: {writtenMemberIds.Count}");
Console.WriteLine($"Manifest:        {manifestPath}");
Console.WriteLine($"Summary:         {summaryPath}");
Console.WriteLine();
Console.WriteLine(
    "The original MainWindow.xaml.cs was not modified.");

return 0;

static void PrintUsage()
{
    Console.WriteLine("Usage:");

    Console.WriteLine(
        "  SplitPlanPreview " +
        "<MainWindow.xaml.cs> " +
        "<MainWindow-split-plan.csv> " +
        "<output-directory>");
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
                LineCount: endLine - startLine + 1,
                Syntax: member));
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

        result.Add(
            new SplitPlanEntry(
                CsvRowNumber: rowIndex + 1,
                MemberId: row[memberIdIndex].Trim(),
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

    List<string> problems = [];

    foreach ((string memberId, List<SplitPlanEntry> entries)
             in planById)
    {
        if (entries.Count > 1)
        {
            problems.Add(
                $"Duplicate plan member: {memberId}");
        }
    }

    foreach (SourceMember sourceMember in sourceMembers)
    {
        if (!planById.ContainsKey(sourceMember.MemberId))
        {
            problems.Add(
                $"Missing from plan: {sourceMember.MemberId}");
        }
    }

    foreach (SplitPlanEntry entry in planEntries)
    {
        if (!sourceById.TryGetValue(
                entry.MemberId,
                out SourceMember? sourceMember))
        {
            problems.Add(
                $"Unknown plan member: {entry.MemberId}");

            continue;
        }

        if (!string.Equals(
                entry.Type,
                sourceMember.Type,
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.Name,
                sourceMember.Name,
                StringComparison.Ordinal) ||
            !string.Equals(
                entry.Signature,
                sourceMember.Signature,
                StringComparison.Ordinal) ||
            entry.StartLine != sourceMember.StartLine ||
            entry.EndLine != sourceMember.EndLine ||
            entry.LineCount != sourceMember.LineCount)
        {
            problems.Add(
                $"Plan/source mismatch: {entry.MemberId}");
        }

        if (string.IsNullOrWhiteSpace(entry.SuggestedFile) ||
            !entry.SuggestedFile.StartsWith(
                "MainWindow.",
                StringComparison.Ordinal) ||
            !entry.SuggestedFile.EndsWith(
                ".cs",
                StringComparison.OrdinalIgnoreCase))
        {
            problems.Add(
                $"Invalid target file: {entry.MemberId}");
        }

        if (string.Equals(
                entry.SuggestedFile,
                "MainWindow.Unassigned.cs",
                StringComparison.OrdinalIgnoreCase))
        {
            problems.Add(
                $"Unassigned member: {entry.MemberId}");
        }

        if (string.Equals(
                entry.Confidence,
                "Low",
                StringComparison.OrdinalIgnoreCase))
        {
            problems.Add(
                $"Low-confidence member: {entry.MemberId}");
        }
    }

    return new ValidationResult(
        SourceMemberCount: sourceMembers.Count,
        PlanEntryCount: planEntries.Count,
        UniquePlanMemberCount: planById.Count,
        Problems: problems);
}

static void PrintValidationSummary(
    ValidationResult validation)
{
    Console.WriteLine("PREVIEW INPUT VALIDATION");
    Console.WriteLine("========================");
    Console.WriteLine();
    Console.WriteLine(
        $"Source members:      {validation.SourceMemberCount}");

    Console.WriteLine(
        $"Plan entries:        {validation.PlanEntryCount}");

    Console.WriteLine(
        $"Unique plan members: {validation.UniquePlanMemberCount}");

    Console.WriteLine(
        $"Problems:            {validation.Problems.Count}");

    if (validation.Problems.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("VALIDATION PASSED");
        return;
    }

    Console.WriteLine();
    Console.WriteLine("VALIDATION FAILED");

    foreach (string problem in validation.Problems)
    {
        Console.WriteLine($"  {problem}");
    }
}

static string BuildPartialClassFile(
    string generatedFileHeader,
    string usingText,
    string namespaceName,
    ClassDeclarationSyntax originalClass,
    IReadOnlyCollection<SplitPlanEntry> entries,
    IReadOnlyDictionary<string, SourceMember> sourceById,
    bool isCoreFile,
    ISet<string> writtenMemberIds)
{
    StringBuilder output = new();

    output.AppendLine(generatedFileHeader);

    if (!string.IsNullOrWhiteSpace(usingText))
    {
        output.AppendLine(usingText.TrimEnd());
        output.AppendLine();
    }

    output.Append("namespace ");
    output.Append(namespaceName);
    output.AppendLine(";");
    output.AppendLine();

    string classDeclaration =
        BuildClassDeclaration(
            originalClass,
            isCoreFile);

    output.AppendLine(classDeclaration);
    output.AppendLine("{");

    foreach (SplitPlanEntry entry in entries)
    {
        if (!sourceById.TryGetValue(
                entry.MemberId,
                out SourceMember? sourceMember))
        {
            throw new InvalidOperationException(
                $"Unable to find source member: {entry.MemberId}");
        }

        if (!writtenMemberIds.Add(entry.MemberId))
        {
            throw new InvalidOperationException(
                $"Member was written more than once: {entry.MemberId}");
        }

        string memberText =
            sourceMember.Syntax
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia()
                .ToFullString();

        output.AppendLine();
        output.AppendLine(
            IndentText(
                memberText,
                4));
    }

    output.AppendLine("}");

    return NormalizeLineEndings(
        output.ToString());
}

static string BuildClassDeclaration(
    ClassDeclarationSyntax originalClass,
    bool includeBaseList)
{
    List<string> modifiers = originalClass.Modifiers
        .Select(token =>
            token.ValueText)
        .ToList();

    if (!modifiers.Contains(
            "partial",
            StringComparer.Ordinal))
    {
        modifiers.Add("partial");
    }

    StringBuilder declaration = new();

    if (modifiers.Count > 0)
    {
        declaration.Append(
            string.Join(
                " ",
                modifiers));

        declaration.Append(' ');
    }

    declaration.Append("class ");
    declaration.Append(
        originalClass.Identifier.ValueText);

    if (originalClass.TypeParameterList is not null)
    {
        declaration.Append(
            originalClass.TypeParameterList.ToFullString().Trim());
    }

    if (includeBaseList &&
        originalClass.BaseList is not null)
    {
        declaration.Append(' ');
        declaration.Append(
            originalClass.BaseList.ToFullString().Trim());
    }

    return declaration.ToString();
}

static string BuildGeneratedFileHeader(
    string sourceFilePath,
    string splitPlanPath)
{
    return
        "// <auto-generated-preview>\n" +
        "// This file is a preview generated by SplitPlanPreview.\n" +
        "// It is not part of the application build.\n" +
        $"// Source: {sourceFilePath}\n" +
        $"// Plan: {splitPlanPath}\n" +
        "// </auto-generated-preview>\n" +
        "#nullable enable";
}

static string BuildUsingText(
    CompilationUnitSyntax compilationUnit)
{
    StringBuilder result = new();

    foreach (ExternAliasDirectiveSyntax externAlias
             in compilationUnit.Externs)
    {
        result.AppendLine(
            externAlias
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia()
                .ToFullString());
    }

    foreach (UsingDirectiveSyntax usingDirective
             in compilationUnit.Usings)
    {
        result.AppendLine(
            usingDirective
                .WithoutLeadingTrivia()
                .WithoutTrailingTrivia()
                .ToFullString());
    }

    return result.ToString();
}

static string? GetNamespaceName(
    ClassDeclarationSyntax classDeclaration)
{
    BaseNamespaceDeclarationSyntax? namespaceDeclaration =
        classDeclaration
            .Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

    return namespaceDeclaration?
        .Name
        .ToString();
}

static void PrepareOutputDirectory(
    string outputDirectoryPath)
{
    if (Directory.Exists(outputDirectoryPath))
    {
        Directory.Delete(
            outputDirectoryPath,
            recursive: true);
    }

    Directory.CreateDirectory(
        outputDirectoryPath);
}

static async Task WriteManifestAsync(
    string manifestPath,
    IReadOnlyCollection<GeneratedFileResult> generatedFiles,
    IReadOnlyDictionary<string, List<SplitPlanEntry>> entriesByTargetFile)
{
    StringBuilder csv = new();

    csv.AppendLine(
        "FileName,MemberCount,ApproximateSourceLines," +
        "ByteCount,Sha256,MemberIds");

    foreach (GeneratedFileResult file in generatedFiles
                 .OrderBy(
                     file => file.FileName,
                     StringComparer.OrdinalIgnoreCase))
    {
        string memberIds = string.Join(
            ";",
            entriesByTargetFile[file.FileName]
                .Select(entry =>
                    entry.MemberId));

        csv.AppendLine(
            string.Join(
                ",",
                EscapeCsv(file.FileName),
                file.MemberCount,
                file.ApproximateSourceLines,
                file.ByteCount,
                EscapeCsv(file.Sha256),
                EscapeCsv(memberIds)));
    }

    await File.WriteAllTextAsync(
        manifestPath,
        csv.ToString(),
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false));
}

static async Task WriteSummaryAsync(
    string summaryPath,
    string sourceFilePath,
    string splitPlanPath,
    string outputDirectoryPath,
    int memberCount,
    IReadOnlyCollection<GeneratedFileResult> generatedFiles)
{
    StringBuilder summary = new();

    summary.AppendLine("MAINWINDOW SPLIT PREVIEW");
    summary.AppendLine("========================");
    summary.AppendLine();
    summary.AppendLine($"Source: {sourceFilePath}");
    summary.AppendLine($"Plan:   {splitPlanPath}");
    summary.AppendLine($"Output: {outputDirectoryPath}");
    summary.AppendLine();
    summary.AppendLine($"Members written: {memberCount}");
    summary.AppendLine($"Partial files:   {generatedFiles.Count}");
    summary.AppendLine();
    summary.AppendLine("FILES");
    summary.AppendLine("-----");

    foreach (GeneratedFileResult file in generatedFiles
                 .OrderByDescending(file =>
                     file.ApproximateSourceLines))
    {
        summary.AppendLine(
            $"{file.FileName,-42} " +
            $"{file.MemberCount,4} members  " +
            $"{file.ApproximateSourceLines,5} approximate source lines");
    }

    summary.AppendLine();
    summary.AppendLine(
        "The original source file was not modified.");

    await File.WriteAllTextAsync(
        summaryPath,
        summary.ToString(),
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false));
}

static string CalculateSha256(
    string filePath)
{
    using FileStream stream =
        File.OpenRead(filePath);

    byte[] hash =
        SHA256.HashData(stream);

    return Convert.ToHexString(hash);
}

static string IndentText(
    string text,
    int spaces)
{
    string indent =
        new(' ', spaces);

    string normalized =
        NormalizeLineEndings(text);

    string[] lines =
        normalized.Split('\n');

    return string.Join(
        "\n",
        lines.Select(line =>
            line.Length == 0
                ? string.Empty
                : indent + line));
}

static string NormalizeLineEndings(
    string text)
{
    return text
        .Replace("\r\n", "\n")
        .Replace('\r', '\n');
}

static bool IsSameOrChildPath(
    string candidatePath,
    string parentPath)
{
    string normalizedCandidate =
        Path.GetFullPath(candidatePath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

    string normalizedParent =
        Path.GetFullPath(parentPath)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);

    if (string.Equals(
            normalizedCandidate,
            normalizedParent,
            StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    string parentWithSeparator =
        normalizedParent +
        Path.DirectorySeparatorChar;

    return normalizedCandidate.StartsWith(
        parentWithSeparator,
        StringComparison.OrdinalIgnoreCase);
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

internal sealed record SourceMember(
    string MemberId,
    string Type,
    string Name,
    string Signature,
    int StartLine,
    int EndLine,
    int LineCount,
    MemberDeclarationSyntax Syntax);

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

internal sealed record ValidationResult(
    int SourceMemberCount,
    int PlanEntryCount,
    int UniquePlanMemberCount,
    IReadOnlyList<string> Problems)
{
    public bool IsValid =>
        Problems.Count == 0 &&
        SourceMemberCount == PlanEntryCount &&
        SourceMemberCount == UniquePlanMemberCount;
}

internal sealed record GeneratedFileResult(
    string FileName,
    string FullPath,
    int MemberCount,
    int ApproximateSourceLines,
    long ByteCount,
    string Sha256);