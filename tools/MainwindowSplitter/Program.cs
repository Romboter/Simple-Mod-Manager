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
        "No reports were generated because the source could not be parsed safely.");

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

List<MethodDefinition> methodDefinitions =
    BuildMethodDefinitions(
        syntaxTree,
        mainWindowClass);

List<MethodCallInventoryItem> methodCalls =
    BuildMethodCallInventory(
        methodDefinitions);

List<MethodCallerInventoryItem> methodCallers =
    BuildMethodCallerInventory(
        methodDefinitions,
        methodCalls);

PrintInventory(
    sourceFilePath,
    syntaxTree,
    mainWindowClass,
    inventory);

PrintCallResolutionSummary(methodCalls);

if (outputFilePath is not null)
{
    try
    {
        string outputDirectory =
            Path.GetDirectoryName(outputFilePath)
            ?? Directory.GetCurrentDirectory();

        Directory.CreateDirectory(outputDirectory);

        string outputName =
            Path.GetFileNameWithoutExtension(outputFilePath);

        string callsPath = Path.Combine(
            outputDirectory,
            $"{outputName}-calls.csv");

        string callersPath = Path.Combine(
            outputDirectory,
            $"{outputName}-callers.csv");

        await WriteMemberInventoryCsvAsync(
            outputFilePath,
            inventory);

        await WriteMethodCallsCsvAsync(
            callsPath,
            methodCalls);

        await WriteMethodCallersCsvAsync(
            callersPath,
            methodCallers);

        Console.WriteLine();
        Console.WriteLine(
            $"Member inventory CSV written to: {outputFilePath}");

        Console.WriteLine(
            $"Method calls CSV written to:     {callsPath}");

        Console.WriteLine(
            $"Method callers CSV written to:   {callersPath}");
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

        string type =
            GetMemberType(member);

        string name =
            GetMemberName(member);

        string signature =
            GetMemberSignature(member);

        string memberId =
            BuildMemberId(
                type,
                signature,
                startLine);

        inventory.Add(
            new MemberInventoryItem(
                MemberId: memberId,
                Type: type,
                Name: name,
                Signature: signature,
                StartLine: startLine,
                EndLine: endLine,
                LineCount: endLine - startLine + 1,
                Modifiers: GetModifiers(member),
                ReturnType: GetReturnType(member),
                Parameters: GetParameters(member)));
    }

    return inventory;
}

static List<MethodDefinition> BuildMethodDefinitions(
    SyntaxTree syntaxTree,
    ClassDeclarationSyntax mainWindowClass)
{
    List<MethodDefinition> definitions = [];

    foreach (MethodDeclarationSyntax method in mainWindowClass.Members
                 .OfType<MethodDeclarationSyntax>())
    {
        FileLinePositionSpan location =
            syntaxTree.GetLineSpan(method.Span);

        int startLine =
            location.StartLinePosition.Line + 1;

        int endLine =
            location.EndLinePosition.Line + 1;

        string name =
            method.Identifier.ValueText;

        string signature =
            GetMethodSignature(method);

        string memberId =
            BuildMemberId(
                "Method",
                signature,
                startLine);

        ParameterArity arity =
            GetParameterArity(method.ParameterList);

        definitions.Add(
            new MethodDefinition(
                MemberId: memberId,
                Name: name,
                Signature: signature,
                StartLine: startLine,
                EndLine: endLine,
                LineCount: endLine - startLine + 1,
                RequiredParameterCount: arity.RequiredCount,
                MaximumParameterCount: arity.MaximumCount,
                HasParamsParameter: arity.HasParamsParameter,
                Syntax: method));
    }

    return definitions;
}

static List<MethodCallInventoryItem> BuildMethodCallInventory(
    IReadOnlyCollection<MethodDefinition> methodDefinitions)
{
    Dictionary<string, List<MethodDefinition>> methodsByName =
        methodDefinitions
            .GroupBy(
                method => method.Name,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.Ordinal);

    List<MethodCallInventoryItem> methodCalls = [];

    foreach (MethodDefinition caller in methodDefinitions)
    {
        List<InvocationReference> invocationReferences = caller.Syntax
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(GetInvocationReference)
            .Where(reference =>
                reference is not null)
            .Select(reference => reference!)
            .Where(reference =>
                methodsByName.ContainsKey(reference.Name))
            .ToList();

        List<string> calledMethodNames = invocationReferences
            .Select(reference =>
                reference.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(
                name => name,
                StringComparer.Ordinal)
            .ToList();

        HashSet<string> resolvedTargetIds =
            new(StringComparer.Ordinal);

        HashSet<string> ambiguousMethodNames =
            new(StringComparer.Ordinal);

        HashSet<string> unresolvedMethodNames =
            new(StringComparer.Ordinal);

        foreach (InvocationReference invocation in invocationReferences)
        {
            List<MethodDefinition> possibleTargets =
    methodsByName[invocation.Name]
        .Where(candidate =>
            CanAcceptArgumentCount(
                candidate,
                invocation.ArgumentCount))
        .ToList();

List<MethodDefinition> exactParameterCountTargets =
    possibleTargets
        .Where(candidate =>
            candidate.MaximumParameterCount ==
            invocation.ArgumentCount)
        .ToList();

if (exactParameterCountTargets.Count > 0)
{
    possibleTargets = exactParameterCountTargets;
}

            if (possibleTargets.Count == 1)
            {
                resolvedTargetIds.Add(
                    possibleTargets[0].MemberId);
            }
            else if (possibleTargets.Count > 1)
            {
                ambiguousMethodNames.Add(
                    invocation.Name);
            }
            else
            {
                unresolvedMethodNames.Add(
                    invocation.Name);
            }
        }

        methodCalls.Add(
            new MethodCallInventoryItem(
                MemberId: caller.MemberId,
                Name: caller.Name,
                Signature: caller.Signature,
                StartLine: caller.StartLine,
                EndLine: caller.EndLine,
                LineCount: caller.LineCount,
                CalledMethodNames: calledMethodNames,
                ResolvedTargetIds: resolvedTargetIds
                    .OrderBy(
                        id => id,
                        StringComparer.Ordinal)
                    .ToList(),
                AmbiguousMethodNames: ambiguousMethodNames
                    .OrderBy(
                        name => name,
                        StringComparer.Ordinal)
                    .ToList(),
                UnresolvedMethodNames: unresolvedMethodNames
                    .OrderBy(
                        name => name,
                        StringComparer.Ordinal)
                    .ToList()));
    }

    return methodCalls;
}

static List<MethodCallerInventoryItem> BuildMethodCallerInventory(
    IReadOnlyCollection<MethodDefinition> methodDefinitions,
    IReadOnlyCollection<MethodCallInventoryItem> methodCalls)
{
    Dictionary<string, MethodDefinition> definitionsById =
        methodDefinitions.ToDictionary(
            method => method.MemberId,
            StringComparer.Ordinal);

    Dictionary<string, List<string>> callerIdsByTargetId =
        new(StringComparer.Ordinal);

    foreach (MethodCallInventoryItem caller in methodCalls)
    {
        foreach (string targetId in caller.ResolvedTargetIds)
        {
            if (!callerIdsByTargetId.TryGetValue(
                    targetId,
                    out List<string>? callerIds))
            {
                callerIds = [];
                callerIdsByTargetId[targetId] = callerIds;
            }

            callerIds.Add(caller.MemberId);
        }
    }

    List<MethodCallerInventoryItem> result = [];

    foreach (MethodDefinition method in methodDefinitions)
    {
        List<string> callerIds =
            callerIdsByTargetId.TryGetValue(
                method.MemberId,
                out List<string>? foundCallerIds)
                ? foundCallerIds
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(
                        id => id,
                        StringComparer.Ordinal)
                    .ToList()
                : [];

        List<string> callerSignatures = callerIds
            .Where(definitionsById.ContainsKey)
            .Select(id =>
                definitionsById[id].Signature)
            .OrderBy(
                signature => signature,
                StringComparer.Ordinal)
            .ToList();

        result.Add(
            new MethodCallerInventoryItem(
                MemberId: method.MemberId,
                Name: method.Name,
                Signature: method.Signature,
                StartLine: method.StartLine,
                EndLine: method.EndLine,
                CallerIds: callerIds,
                CallerSignatures: callerSignatures));
    }

    return result;
}

static bool CanAcceptArgumentCount(
    MethodDefinition method,
    int argumentCount)
{
    if (argumentCount < method.RequiredParameterCount)
    {
        return false;
    }

    if (method.HasParamsParameter)
    {
        return true;
    }

    return argumentCount <= method.MaximumParameterCount;
}

static ParameterArity GetParameterArity(
    ParameterListSyntax parameterList)
{
    int requiredCount = 0;
    int maximumCount = parameterList.Parameters.Count;
    bool hasParamsParameter = false;

    foreach (ParameterSyntax parameter in parameterList.Parameters)
    {
        bool isParams = parameter.Modifiers.Any(
            modifier =>
                modifier.IsKind(SyntaxKind.ParamsKeyword));

        if (isParams)
        {
            hasParamsParameter = true;
        }

        bool isOptional =
            parameter.Default is not null ||
            isParams;

        if (!isOptional)
        {
            requiredCount++;
        }
    }

    return new ParameterArity(
        RequiredCount: requiredCount,
        MaximumCount: maximumCount,
        HasParamsParameter: hasParamsParameter);
}

static InvocationReference? GetInvocationReference(
    InvocationExpressionSyntax invocation)
{
    string? methodName = invocation.Expression switch
    {
        IdentifierNameSyntax identifier =>
            identifier.Identifier.ValueText,

        GenericNameSyntax genericName =>
            genericName.Identifier.ValueText,

        MemberAccessExpressionSyntax memberAccess
            when IsThisOrUnqualifiedMemberAccess(memberAccess) =>
            GetSimpleName(memberAccess.Name),

        MemberBindingExpressionSyntax memberBinding =>
            GetSimpleName(memberBinding.Name),

        _ =>
            null
    };

    if (string.IsNullOrWhiteSpace(methodName))
    {
        return null;
    }

    return new InvocationReference(
        Name: methodName,
        ArgumentCount: invocation.ArgumentList.Arguments.Count);
}

static bool IsThisOrUnqualifiedMemberAccess(
    MemberAccessExpressionSyntax memberAccess)
{
    return memberAccess.Expression switch
    {
        ThisExpressionSyntax =>
            true,

        BaseExpressionSyntax =>
            true,

        IdentifierNameSyntax identifier
            when identifier.Identifier.ValueText == "this" =>
            true,

        _ =>
            false
    };
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

static void PrintCallResolutionSummary(
    IReadOnlyCollection<MethodCallInventoryItem> methodCalls)
{
    int methodsWithAmbiguousCalls = methodCalls.Count(
        method =>
            method.AmbiguousMethodNames.Count > 0);

    int methodsWithUnresolvedCalls = methodCalls.Count(
        method =>
            method.UnresolvedMethodNames.Count > 0);

    int totalResolvedTargets = methodCalls.Sum(
        method =>
            method.ResolvedTargetIds.Count);

    Console.WriteLine();
    Console.WriteLine("Call graph:");
    Console.WriteLine(
        $"  Resolved target edges: {totalResolvedTargets}");

    Console.WriteLine(
        $"  Methods with ambiguous calls: {methodsWithAmbiguousCalls}");

    Console.WriteLine(
        $"  Methods with unresolved calls: {methodsWithUnresolvedCalls}");
}

static async Task WriteMemberInventoryCsvAsync(
    string outputFilePath,
    IEnumerable<MemberInventoryItem> inventory)
{
    StringBuilder csv = new();

    csv.AppendLine(
        "MemberId,Type,Name,Signature,StartLine,EndLine," +
        "LineCount,Modifiers,ReturnType,Parameters");

    foreach (MemberInventoryItem item in inventory)
    {
        csv.AppendLine(
            string.Join(
                ",",
                EscapeCsv(item.MemberId),
                EscapeCsv(item.Type),
                EscapeCsv(item.Name),
                EscapeCsv(item.Signature),
                item.StartLine,
                item.EndLine,
                item.LineCount,
                EscapeCsv(item.Modifiers),
                EscapeCsv(item.ReturnType),
                EscapeCsv(item.Parameters)));
    }

    await WriteUtf8FileAsync(
        outputFilePath,
        csv.ToString());
}

static async Task WriteMethodCallsCsvAsync(
    string outputFilePath,
    IEnumerable<MethodCallInventoryItem> methodCalls)
{
    StringBuilder csv = new();

    csv.AppendLine(
        "MemberId,Name,Signature,StartLine,EndLine,LineCount," +
        "CalledMethodNames,ResolvedTargetIds,AmbiguousMethodNames," +
        "UnresolvedMethodNames,CallCount,ResolvedCount," +
        "AmbiguousCount,UnresolvedCount");

    foreach (MethodCallInventoryItem method in methodCalls)
    {
        csv.AppendLine(
            string.Join(
                ",",
                EscapeCsv(method.MemberId),
                EscapeCsv(method.Name),
                EscapeCsv(method.Signature),
                method.StartLine,
                method.EndLine,
                method.LineCount,
                EscapeCsv(string.Join(
                    ";",
                    method.CalledMethodNames)),
                EscapeCsv(string.Join(
                    ";",
                    method.ResolvedTargetIds)),
                EscapeCsv(string.Join(
                    ";",
                    method.AmbiguousMethodNames)),
                EscapeCsv(string.Join(
                    ";",
                    method.UnresolvedMethodNames)),
                method.CalledMethodNames.Count,
                method.ResolvedTargetIds.Count,
                method.AmbiguousMethodNames.Count,
                method.UnresolvedMethodNames.Count));
    }

    await WriteUtf8FileAsync(
        outputFilePath,
        csv.ToString());
}

static async Task WriteMethodCallersCsvAsync(
    string outputFilePath,
    IEnumerable<MethodCallerInventoryItem> methodCallers)
{
    StringBuilder csv = new();

    csv.AppendLine(
        "MemberId,Name,Signature,StartLine,EndLine," +
        "CallerIds,CallerSignatures,CallerCount");

    foreach (MethodCallerInventoryItem method in methodCallers)
    {
        csv.AppendLine(
            string.Join(
                ",",
                EscapeCsv(method.MemberId),
                EscapeCsv(method.Name),
                EscapeCsv(method.Signature),
                method.StartLine,
                method.EndLine,
                EscapeCsv(string.Join(
                    ";",
                    method.CallerIds)),
                EscapeCsv(string.Join(
                    ";",
                    method.CallerSignatures)),
                method.CallerIds.Count));
    }

    await WriteUtf8FileAsync(
        outputFilePath,
        csv.ToString());
}

static Task WriteUtf8FileAsync(
    string outputFilePath,
    string contents)
{
    return File.WriteAllTextAsync(
        outputFilePath,
        contents,
        new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false));
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
            string modifiers =
                parameter.Modifiers.ToString();

            string type =
                parameter.Type?.ToString() ?? string.Empty;

            string name =
                parameter.Identifier.ValueText;

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
    string MemberId,
    string Type,
    string Name,
    string Signature,
    int StartLine,
    int EndLine,
    int LineCount,
    string Modifiers,
    string ReturnType,
    string Parameters);

internal sealed record MethodDefinition(
    string MemberId,
    string Name,
    string Signature,
    int StartLine,
    int EndLine,
    int LineCount,
    int RequiredParameterCount,
    int MaximumParameterCount,
    bool HasParamsParameter,
    MethodDeclarationSyntax Syntax);

internal sealed record InvocationReference(
    string Name,
    int ArgumentCount);

internal sealed record ParameterArity(
    int RequiredCount,
    int MaximumCount,
    bool HasParamsParameter);

internal sealed record MethodCallInventoryItem(
    string MemberId,
    string Name,
    string Signature,
    int StartLine,
    int EndLine,
    int LineCount,
    IReadOnlyList<string> CalledMethodNames,
    IReadOnlyList<string> ResolvedTargetIds,
    IReadOnlyList<string> AmbiguousMethodNames,
    IReadOnlyList<string> UnresolvedMethodNames);

internal sealed record MethodCallerInventoryItem(
    string MemberId,
    string Name,
    string Signature,
    int StartLine,
    int EndLine,
    IReadOnlyList<string> CallerIds,
    IReadOnlyList<string> CallerSignatures);