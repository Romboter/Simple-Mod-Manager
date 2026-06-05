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

IEnumerable<Diagnostic> parseErrors = syntaxTree
    .GetDiagnostics()
    .Where(diagnostic =>
        diagnostic.Severity == DiagnosticSeverity.Error);

bool hasParseErrors = false;

foreach (Diagnostic diagnostic in parseErrors)
{
    hasParseErrors = true;
    Console.Error.WriteLine(diagnostic);
}

if (hasParseErrors)
{
    Console.Error.WriteLine(
        "Roslyn found syntax errors. No inventory was generated.");

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

List<MemberInventoryItem> inventory = [];

foreach (MemberDeclarationSyntax member in mainWindowClass.Members)
{
    FileLinePositionSpan location =
        syntaxTree.GetLineSpan(member.FullSpan);

    int startLine = location.StartLinePosition.Line + 1;
    int endLine = location.EndLinePosition.Line + 1;

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

PrintInventory(sourceFilePath, mainWindowClass, inventory);

if (outputFilePath is not null)
{
    try
    {
        string? outputDirectory =
            Path.GetDirectoryName(outputFilePath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        await WriteCsvAsync(outputFilePath, inventory);

        Console.WriteLine();
        Console.WriteLine($"CSV written to: {outputFilePath}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(
            $"Unable to write CSV '{outputFilePath}': {exception.Message}");

        return 1;
    }
}

return 0;

static void PrintInventory(
    string sourceFilePath,
    ClassDeclarationSyntax classDeclaration,
    IReadOnlyCollection<MemberInventoryItem> inventory)
{
    Console.WriteLine($"Source:  {sourceFilePath}");
    Console.WriteLine($"Class:   {classDeclaration.Identifier.ValueText}");
    Console.WriteLine($"Members: {inventory.Count}");
    Console.WriteLine();

    Console.WriteLine(
        $"{"Type",-20} {"Name",-50} {"Start",8} {"End",8} {"Lines",8}");

    Console.WriteLine(new string('-', 100));

    foreach (MemberInventoryItem item in inventory)
    {
        Console.WriteLine(
            $"{item.Type,-20} {Truncate(item.Name, 50),-50} " +
            $"{item.StartLine,8} {item.EndLine,8} {item.LineCount,8}");
    }
}

static async Task WriteCsvAsync(
    string outputFilePath,
    IEnumerable<MemberInventoryItem> inventory)
{
    StringBuilder csv = new();

    csv.AppendLine(
        "Type,Name,StartLine,EndLine,LineCount,Modifiers,ReturnType,Parameters");

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
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

static string GetMemberType(MemberDeclarationSyntax member)
{
    return member switch
    {
        FieldDeclarationSyntax => "Field",
        PropertyDeclarationSyntax => "Property",
        ConstructorDeclarationSyntax => "Constructor",
        MethodDeclarationSyntax => "Method",
        EventFieldDeclarationSyntax => "Event",
        EventDeclarationSyntax => "Event",
        ClassDeclarationSyntax => "Nested class",
        StructDeclarationSyntax => "Nested struct",
        RecordDeclarationSyntax => "Nested record",
        EnumDeclarationSyntax => "Nested enum",
        DelegateDeclarationSyntax => "Delegate",
        IndexerDeclarationSyntax => "Indexer",
        OperatorDeclarationSyntax => "Operator",
        ConversionOperatorDeclarationSyntax => "Conversion operator",
        DestructorDeclarationSyntax => "Destructor",
        _ => member.Kind().ToString()
    };
}

static string GetMemberName(MemberDeclarationSyntax member)
{
    return member switch
    {
        FieldDeclarationSyntax field =>
            string.Join(
                ", ",
                field.Declaration.Variables.Select(
                    variable => variable.Identifier.ValueText)),

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
                    variable => variable.Identifier.ValueText)),

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

        _ => "(unknown)"
    };
}

static string GetModifiers(MemberDeclarationSyntax member)
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

        _ => string.Empty
    };
}

static string GetReturnType(MemberDeclarationSyntax member)
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

        _ => string.Empty
    };
}

static string GetParameters(MemberDeclarationSyntax member)
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

        _ => null
    };

    if (parameterList is null)
    {
        return string.Empty;
    }

    return string.Join(
        ", ",
        parameterList.Parameters.Select(parameter =>
        {
            string type = parameter.Type?.ToString() ?? string.Empty;
            string name = parameter.Identifier.ValueText;

            return string.IsNullOrWhiteSpace(type)
                ? name
                : $"{type} {name}";
        }));
}

static string EscapeCsv(string value)
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

static string Truncate(string value, int maximumLength)
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