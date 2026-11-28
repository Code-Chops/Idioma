using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a member marked <c>[Localized]</c> that gets no text (<c>LOC502</c>), an unmarked enum member next to marked
/// ones (<c>LOC501</c>), and a <c>GetTextByMember</c> or <c>GetTextOfType</c> call that names something without a
/// text (<c>LOC601</c>).
/// </summary>
/// <remarks>
/// An analyzer reports these, and not the generator, because it sees the generated attribute in the compilation. So it
/// knows which attribute each <c>[Localized]</c> binds to, also in Razor's output.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LocalizedMemberAnalyzer : DiagnosticAnalyzer
{
    public const string UnmarkedEnumMemberDiagnosticId = "LOC501";
    public const string NoTextToFindDiagnosticId = "LOC601";
    public const string CannotRegisterDiagnosticId = "LOC502";

    private const string GetTextByMemberName = "GetTextByMember";
    private const string GetTextOfTypeName = "GetTextOfType";

    private static readonly DiagnosticDescriptor CannotRegisterRule = new(
        id: CannotRegisterDiagnosticId,
        title: "Text of a member can't be registered",
        messageFormat: "The text of '{0}' can't be registered because {1}, so it won't be found. {2}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The source generator reads each [Localized] as the project builds, and writes its text as code at the top level of the assembly. That code can't name a private or protected nested type, or a file-local one. The generator can't read what Razor generates from a .razor or .cshtml file either, except the @code and the @attribute of a component on SDK 11 or later. A component's code-behind file is C#, so the generator reads it on any SDK. A wording that is null gives no text, and neither does an attribute on something a text isn't found by, such as an indexer. Without this warning, LocalizedText.GetTextByMember would silently give nothing for the member.",
        helpLinkUri: PackageNames.HelpLink(CannotRegisterDiagnosticId));

    private static readonly DiagnosticDescriptor UnmarkedEnumMemberRule = new(
        id: UnmarkedEnumMemberDiagnosticId,
        title: "Enum member without a text next to members that have one",
        messageFormat: "'{0}.{1}' has no text, but other members of '{0}' have one, so users would see '{1}' as written. Mark it {2}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An enum whose members are marked [Localized] shows the name of a member that isn't, in every language. That's usually a member added later. Mark it too. Where every member of an enum needs a text, a set of the enum (ILocalizationSet<TEnum>) makes a missing one a build error. Its GetText then comes from the set. An enum with a text of its own and no marked member isn't reported, because its values may never be shown.",
        helpLinkUri: PackageNames.HelpLink(UnmarkedEnumMemberDiagnosticId));

    private static readonly DiagnosticDescriptor NoTextToFindRule = new(
        id: NoTextToFindDiagnosticId,
        title: "Text asked of a member or a type that has none",
        messageFormat: "'{0}' has no text, so {1} gives null. {2}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "LocalizedText.GetTextByMember and GetTextOfType give the text that [Localized] writes on a member or a type, or null where there's none. A call that names its type and its member in the code asks for a text the build can look for, so a member or a type without [Localized] is a mistake it can see. A type of another assembly is checked only when that assembly doesn't use Idioma, because the attribute isn't kept in a compiled assembly.",
        helpLinkUri: PackageNames.HelpLink(NoTextToFindDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [CannotRegisterRule, UnmarkedEnumMemberRule, NoTextToFindRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a member marked in markup must be caught there.
        // AnalyzerSupport.IsHandWritten leaves out the rest.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        // Without languages there's no attribute, which LOC100 reports.
        var codes = SupportedLanguages.ReadCodes(context.Compilation.Assembly);

        if (codes.Length is 0)
            return;

        var sample = $"[{LocalizedMembers.AttributeName}({String.Join(", ", codes.Select(static code => $"{SupportedLanguages.ToParameterName(code)}: \"...\""))})]";

        context.RegisterSyntaxNodeAction(c => AnalyzeLocalized(c, codes), SyntaxKind.Attribute);
        // Lazy, because only an enum with both marked and unmarked members asks.
        var compilation = context.Compilation;
        var enumsWithSets = new Lazy<ImmutableHashSet<INamedTypeSymbol>>(() => FindEnumsWithSets(compilation), LazyThreadSafetyMode.PublicationOnly);

        context.RegisterSyntaxNodeAction(c => AnalyzeEnum(c, sample, enumsWithSets), SyntaxKind.EnumDeclaration);

        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizedText) is not { } text)
            return;

        var usesIdioma = new ConcurrentDictionary<IAssemblySymbol, bool>(SymbolEqualityComparer.Default);

        context.RegisterOperationAction(c => AnalyzeLookup(c, text, sample, usesIdioma), OperationKind.Invocation);
    }

    /// <summary>
    /// Reports a <c>GetTextByMember</c> or <c>GetTextOfType</c> call whose type and member are constants in the code,
    /// when that member or type has no text (<c>LOC601</c>).
    /// </summary>
    // A MemberInfo or Type known only at run time isn't checked, because null is a valid answer there.
    private static void AnalyzeLookup(OperationAnalysisContext context, INamedTypeSymbol text, string sample, ConcurrentDictionary<IAssemblySymbol, bool> usesIdioma)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.Name is not (GetTextByMemberName or GetTextOfTypeName)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, text)
            || !AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree))
        {
            return;
        }

        var type = method.IsGenericMethod
            ? method.TypeArguments[0]
            : method.Parameters is [{ Type: { Name: nameof(Type), ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } } }, ..]
              && invocation.Arguments is [{ Value: ITypeOfOperation typeOf }, ..]
                ? typeOf.TypeOperand
                : null;

        if (type is not INamedTypeSymbol { TypeKind: not TypeKind.Error } named)
            return;

        IOperation? memberArgument = null;
        string? member = null;

        if (method.Name is GetTextByMemberName)
        {
            memberArgument = invocation.Arguments.FirstOrDefault(static argument => argument.Parameter?.Type.SpecialType is SpecialType.System_String)?.Value;

            if (memberArgument?.ConstantValue is not { HasValue: true, Value: string name })
                return;

            member = name;
        }

        if (FindMissingText(named.OriginalDefinition, member, sample, context.Compilation, usesIdioma) is not { } missing)
            return;

        var location = memberArgument?.Syntax.GetLocation()
                       ?? (invocation.Syntax is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } ? access.Name.GetLocation() : invocation.Syntax.GetLocation());

        context.ReportDiagnostic(Diagnostic.Create(NoTextToFindRule, location, missing.Subject, method.Name, missing.Advice));
    }

    /// <summary>
    /// The subject and advice for a missing text of <paramref name="member"/>, or of <paramref name="type"/> itself
    /// when it's <see langword="null"/>. Gives <see langword="null"/> where there may be a text.
    /// </summary>
    // As LocalizedText finds one: a member's text also on a base class, and a type's own text on the type alone.
    private static (string Subject, string Advice)? FindMissingText(
        INamedTypeSymbol type,
        string? member,
        string sample,
        Compilation compilation,
        ConcurrentDictionary<IAssemblySymbol, bool> usesIdioma)
    {
        var subject = member is null ? type.Name : $"{type.Name}.{member}";

        if (member is null)
        {
            if (!IsOwn(type, compilation))
                return UsesIdioma(type.ContainingAssembly) ? null : (subject, OfOtherAssembly(type));

            return HasGeneratedAttribute(type, compilation) ? null : (subject, $"Mark '{type.Name}' {sample}");
        }

        // The first member of that name, which is where the attribute goes.
        ISymbol? declared = null;

        // The first class of an assembly without Idioma that declares a member of that name, such as Exception for
        // Message, where no class of this assembly declares one.
        INamedTypeSymbol? declaredWithoutIdioma = null;

        for (var current = type; current is not null && current.SpecialType is not SpecialType.System_Object; current = current.BaseType?.OriginalDefinition)
        {
            // A base class that doesn't bind has no assembly to tell, so it may have a text.
            if (current.ContainingAssembly is null)
                return null;

            if (!IsOwn(current, compilation))
            {
                // The attribute isn't kept in a compiled assembly, so the build can't see its text.
                if (UsesIdioma(current.ContainingAssembly))
                    return null;

                if (declaredWithoutIdioma is null && current.GetMembers(member).Any(static candidate => candidate is IPropertySymbol or IFieldSymbol))
                    declaredWithoutIdioma = current;

                continue;
            }

            foreach (var candidate in current.GetMembers(member))
            {
                if (candidate is not (IPropertySymbol or IFieldSymbol))
                    continue;

                if (HasGeneratedAttribute(candidate, compilation))
                    return null;

                declared ??= candidate;
            }
        }

        if (declared is null)
        {
            if (!IsOwn(type, compilation))
                return (subject, OfOtherAssembly(type));

            // Such as Message on a class of this assembly that derives from Exception.
            if (declaredWithoutIdioma is not null)
                return (subject, $"'{member}' is declared in '{declaredWithoutIdioma.Name}' of '{declaredWithoutIdioma.ContainingAssembly.Name}', which doesn't use Idioma, so it has no text written as an attribute");

            return (subject, $"'{type.Name}' has no property or field of that name. Name the member with nameof, which the compiler checks");
        }

        var where = SymbolEqualityComparer.Default.Equals(declared.ContainingType, type) ? declared.Name : $"{declared.ContainingType.Name}.{declared.Name}";
        return (subject, $"Mark '{where}' {sample}");

        bool UsesIdioma(IAssemblySymbol assembly) => usesIdioma.GetOrAdd(assembly, LocalizationDiscovery.ReferencesPackage);

        static string OfOtherAssembly(INamedTypeSymbol type) =>
            $"'{type.Name}' is a type of '{type.ContainingAssembly.Name}', which doesn't use Idioma, so it has no texts written as attributes";
    }

    private static bool IsOwn(INamedTypeSymbol type, Compilation compilation)
    {
        var isOwn = SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly);
        return isOwn;
    }

    private static bool HasGeneratedAttribute(ISymbol symbol, Compilation compilation)
    {
        var hasGeneratedAttribute = symbol.GetAttributes().Any(attribute => LocalizedMembers.IsGeneratedAttribute(attribute.AttributeClass, compilation.Assembly));
        return hasGeneratedAttribute;
    }

    /// <summary>
    /// Reports a <c>[Localized]</c> that gives its member no text (<c>LOC502</c>).
    /// </summary>
    private static void AnalyzeLocalized(SyntaxNodeAnalysisContext context, ImmutableArray<string> codes)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (IsWrittenThroughAlias(attribute, context))
        {
            var subject = LocalizedMembers.FindMarked(attribute, context.SemanticModel, context.CancellationToken) is [var aliased, ..]
                ? aliased.Name
                : attribute.Name.ToString();

            context.ReportDiagnostic(Diagnostic.Create(CannotRegisterRule, attribute.GetLocation(), subject,
                $"the attribute is written as '{attribute.Name}', an alias the source generator doesn't follow", $"Write it as [{LocalizedMembers.AttributeName}]"));

            return;
        }

        if (!IsGeneratedAttribute(attribute, context))
            return;

        var marked = LocalizedMembers.FindMarked(attribute, context.SemanticModel, context.CancellationToken);

        // On something a text isn't found by. Anywhere else, the compiler rejects the attribute itself.
        if (marked.Length is 0)
        {
            const string behindProperty = "the attribute is on the field behind the property";

            var (subject, problem, advice) = attribute.Parent?.Parent switch
            {
                IndexerDeclarationSyntax => ("this[]", "an indexer has no name to find it by", "Remove the attribute"),
                PropertyDeclarationSyntax property => (property.Identifier.ValueText, behindProperty, "Write the attribute without 'field:'"),
                ParameterSyntax parameter => (parameter.Identifier.ValueText, behindProperty, "Write the attribute with 'property:'"),
                _ => (null, null, null),
            };

            if (subject is not null)
                context.ReportDiagnostic(Diagnostic.Create(CannotRegisterRule, attribute.GetLocation(), subject, problem, advice));

            return;
        }

        var member = marked[0];
        var type = member as INamedTypeSymbol ?? member.ContainingType;
        var name = member is INamedTypeSymbol || type is null ? member.Name : $"{type.Name}.{member.Name}";

        // In Razor's output from markup, which the generator never sees. It does see Razor's declarations file (SDK 11+).
        if (context.IsGeneratedCode && !AnalyzerSupport.IsRazorDeclaration(attribute.SyntaxTree))
        {
            Report("it's written in markup, which the source generator can't read", AdviseOnMarkup(member, type, attribute.SyntaxTree));
            return;
        }

        if (LocalizedMembers.DescribeUnnameable(member) is { } unnameable)
        {
            Report($"generated code can't name '{unnameable.Name}', which is {unnameable.Kind}", unnameable.Advice);
            return;
        }

        if (FindNullWording(attribute, context, codes) is { } language)
            Report($"its wording for '{language}' is null", $"Write a wording for '{language}'");

        void Report(string problem, string advice)
        {
            context.ReportDiagnostic(Diagnostic.Create(CannotRegisterRule, attribute.GetLocation(), name, problem, advice));
        }
    }

    /// <summary>
    /// Where to move a <c>[Localized]</c> written in markup. A component's member or attribute moves to a code-behind
    /// file, because its markup stays in the <c>.razor</c> file. Any other class moves to a C# file whole.
    /// </summary>
    private static string AdviseOnMarkup(ISymbol member, INamedTypeSymbol? type, SyntaxTree tree)
    {
        // Razor writes a component as a top-level class, and a class in its @code as a class inside it.
        var isComponent = type is { ContainingType: null } && tree.FilePath.EndsWith("_razor.g.cs", StringComparison.Ordinal);

        if (!isComponent)
            return $"Move '{type?.Name ?? member.Name}' to a C# file";

        var codeBehind = $"a code-behind file, such as 'partial class {type!.Name}' in '{type.Name}.razor.cs'";

        var advice = member is INamedTypeSymbol
            ? $"Move the attribute to {codeBehind}"
            : $"Move '{member.Name}' to {codeBehind}";

        return advice;
    }

    /// <summary>
    /// The enums a set of this compilation implements <c>ILocalizationSet&lt;TEnum&gt;</c> for.
    /// </summary>
    private static ImmutableHashSet<INamedTypeSymbol> FindEnumsWithSets(Compilation compilation)
    {
        var enums = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var namespaces = new Stack<INamespaceSymbol>([compilation.Assembly.GlobalNamespace]);

        while (namespaces.Count > 0)
        {
            var @namespace = namespaces.Pop();

            foreach (var child in @namespace.GetNamespaceMembers())
                namespaces.Push(child);

            foreach (var type in @namespace.GetTypeMembers())
                AddEnumsOfSet(type, enums);
        }

        return enums.ToImmutable();

        static void AddEnumsOfSet(INamedTypeSymbol type, ImmutableHashSet<INamedTypeSymbol>.Builder enums)
        {
            if (type.TypeKind is TypeKind.Class)
                foreach (var @interface in type.AllInterfaces)
                    if (PackageNames.IsLocalizationSetOfEnum(@interface) && @interface.TypeArguments is [INamedTypeSymbol { TypeKind: TypeKind.Enum } @enum])
                        enums.Add(@enum);

            foreach (var nested in type.GetTypeMembers())
                AddEnumsOfSet(nested, enums);
        }
    }

    /// <summary>
    /// Reports each unmarked enum member where another member has <c>[Localized]</c> (<c>LOC501</c>).
    /// </summary>
    private static void AnalyzeEnum(SyntaxNodeAnalysisContext context, string sample, Lazy<ImmutableHashSet<INamedTypeSymbol>> enumsWithSets)
    {
        var declaration = (EnumDeclarationSyntax)context.Node;

        if (!AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, declaration.SyntaxTree))
            return;

        var marked = new List<EnumMemberDeclarationSyntax>();
        var unmarked = new List<EnumMemberDeclarationSyntax>();

        foreach (var member in declaration.Members)
        {
            var isMarked = member.AttributeLists
                .SelectMany(static list => list.Attributes)
                .Any(attribute => IsGeneratedAttribute(attribute, context));

            (isMarked ? marked : unmarked).Add(member);
        }

        if (unmarked.Count is 0 || marked.Count is 0)
            return;

        // An enum with a set gets its GetText() from the set, which has a text for every member (CS0535).
        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is { } declared && enumsWithSets.Value.Contains(declared))
            return;

        // GetText() finds a text by value, so a member such as Default = Active shows Active's text.
        var markedValues = new HashSet<object>();
        foreach (var member in marked)
            if (FindValue(member, context) is { } value)
                markedValues.Add(value);

        foreach (var member in unmarked)
        {
            if (FindValue(member, context) is { } value && markedValues.Contains(value))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(UnmarkedEnumMemberRule, member.Identifier.GetLocation(),
                declaration.Identifier.ValueText, member.Identifier.ValueText, sample));
        }
    }

    private static object? FindValue(EnumMemberDeclarationSyntax member, SyntaxNodeAnalysisContext context)
    {
        var value = context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken) is { HasConstantValue: true } field
            ? field.ConstantValue
            : null;

        return value;
    }

    /// <summary>
    /// Whether an attribute is the generated <c>[Localized]</c> written through a using alias. The source generator
    /// finds the attribute by name, so such a member gets no text.
    /// </summary>
    // A 'global using' alias of another file isn't followed.
    private static bool IsWrittenThroughAlias(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        if (attribute.Name is not IdentifierNameSyntax { Identifier.ValueText: var name }
            || LocalizedMembers.IsNamedLocalized(attribute)
            || !AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, attribute.SyntaxTree))
        {
            return false;
        }

        var isAlias = false;

        for (SyntaxNode? node = attribute; node is not null && !isAlias; node = node.Parent)
        {
            var usings = node switch
            {
                CompilationUnitSyntax unit => unit.Usings,
                BaseNamespaceDeclarationSyntax @namespace => @namespace.Usings,
                _ => default,
            };

            foreach (var directive in usings)
                isAlias |= directive.Alias is { Name.Identifier.ValueText: var alias } && String.Equals(alias, name, StringComparison.Ordinal);
        }

        var isWrittenThroughAlias = isAlias
                                    && context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is IMethodSymbol constructor
                                    && LocalizedMembers.IsGeneratedAttribute(constructor.ContainingType, context.Compilation.Assembly);

        return isWrittenThroughAlias;
    }

    /// <summary>
    /// Whether a hand-written attribute is the generated <c>[Localized]</c>, also where its arguments don't compile.
    /// </summary>
    private static bool IsGeneratedAttribute(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        if (!LocalizedMembers.IsNamedLocalized(attribute) || !AnalyzerSupport.IsHandWritten(context.IsGeneratedCode, attribute.SyntaxTree))
            return false;

        var symbol = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken);

        var isGeneratedAttribute = (symbol.Symbol ?? symbol.CandidateSymbols.FirstOrDefault()) is IMethodSymbol constructor
                                   && LocalizedMembers.IsGeneratedAttribute(constructor.ContainingType, context.Compilation.Assembly);

        return isGeneratedAttribute;
    }

    /// <summary>
    /// The language of the first argument that's the constant <see langword="null"/>.
    /// </summary>
    private static string? FindNullWording(AttributeSyntax attribute, SyntaxNodeAnalysisContext context, ImmutableArray<string> codes)
    {
        if (attribute.ArgumentList is not { Arguments: var arguments }
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor)
        {
            return null;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            if (AnalyzerSupport.FindParameterOfArgument(arguments, i, constructor) is { } parameter
                && context.SemanticModel.GetConstantValue(arguments[i].Expression, context.CancellationToken) is { HasValue: true, Value: null })
            {
                return SupportedLanguages.FindLanguageOfParameter(parameter, codes);
            }
        }

        return null;
    }
}
