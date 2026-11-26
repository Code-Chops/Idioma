using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a member marked <c>[Localized]</c> that gets no text (<c>LOC502</c>), a member of an enum without
/// <c>[Localized]</c> next to members that have it (<c>LOC501</c>), and a call of <c>GetTextByMember</c> or
/// <c>GetTextOfType</c> that names a member or a type without a text (<c>LOC601</c>). Without these,
/// <c>GetTextByMember</c> would silently give nothing, and <c>GetText()</c> would silently show the member's name.
/// </summary>
/// <remarks>
/// <para>
/// A marked member gets no text when the source generator can't write it:
/// </para>
/// <list type="bullet">
/// <item>Its type is private or protected inside another type, or file-local, so that generated code can't name
/// it.</item>
/// <item>A wording is <see langword="null"/>.</item>
/// <item>It's written in markup. The generator can't read what Razor generates from a <c>.razor</c> or
/// <c>.cshtml</c> file, because no source generator sees what another writes. The exception is the file of
/// declarations that Razor writes from SDK 11 on, which other generators do see.</item>
/// <item>The attribute is on something a text isn't found by, such as an indexer, or the field behind a
/// property.</item>
/// </list>
/// <para>
/// An analyzer reports these, and not the generator, because it sees the compilation with the generated attribute in
/// it. So it knows which attribute each <c>[Localized]</c> binds to, also in Razor's output.
/// </para>
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
        description: "The source generator reads each [Localized] as the project builds, and writes its text as code at the top level of the assembly. That code can't name a private or protected nested type, a file-local one, or one marked [Obsolete] as an error. The generator can't read what Razor generates from a .razor or .cshtml file either, except the @code and the @attribute of a component on SDK 11 or later. A component's code-behind file is C#, so the generator reads it on any SDK. A wording that is null gives no text, and neither does an attribute on something a text isn't found by, such as an indexer. Without this warning, LocalizedText.GetTextByMember would silently give nothing for the member.",
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
        // UnnamedLanguageAnalyzer.IsHandWritten leaves out the rest.
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
        // Found when an enum first needs them, which is rare: only an enum with members both marked and not asks.
        var compilation = context.Compilation;
        var enumsWithSets = new Lazy<ImmutableHashSet<INamedTypeSymbol>>(() => FindEnumsWithSets(compilation), LazyThreadSafetyMode.PublicationOnly);

        context.RegisterSyntaxNodeAction(c => AnalyzeEnum(c, sample, enumsWithSets), SyntaxKind.EnumDeclaration);

        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizedText) is not { } text)
            return;

        // Per assembly the calls name a type of, whether it uses Idioma.
        var usesIdioma = new ConcurrentDictionary<IAssemblySymbol, bool>(SymbolEqualityComparer.Default);

        context.RegisterOperationAction(c => AnalyzeLookup(c, text, sample, usesIdioma), OperationKind.Invocation);
    }

    /// <summary>
    /// Reports a call of <c>GetTextByMember</c> or <c>GetTextOfType</c> that names its type and its member in the code,
    /// when that member or type has no text (<c>LOC601</c>): <c>GetTextByMember&lt;Address&gt;(nameof(Address.Street))</c>,
    /// <c>GetTextByMember(typeof(Address), "Street")</c>, or <c>GetTextOfType&lt;Address&gt;()</c>.
    /// </summary>
    // Not a call that takes a MemberInfo or a Type the code only has at run time, such as the property a form shows,
    // where null is the answer for a member without a text, and not a type parameter.
    private static void AnalyzeLookup(OperationAnalysisContext context, INamedTypeSymbol text, string sample, ConcurrentDictionary<IAssemblySymbol, bool> usesIdioma)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.Name is not (GetTextByMemberName or GetTextOfTypeName)
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, text)
            || !UnnamedLanguageAnalyzer.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree))
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
    /// What a call misses that asks for the text of <paramref name="member"/> of <paramref name="type"/>, or for the
    /// type's own text when <paramref name="member"/> is <see langword="null"/>: the member as a message names it, and
    /// what to do. It's <see langword="null"/> where there's a text, or may be one the build can't see.
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

        for (var current = type; current is not null && current.SpecialType is not SpecialType.System_Object; current = current.BaseType?.OriginalDefinition)
        {
            if (!IsOwn(current, compilation))
            {
                // A text the build can't see, as the attribute isn't kept in a compiled assembly.
                if (UsesIdioma(current.ContainingAssembly))
                    return null;

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
            return IsOwn(type, compilation)
                ? (subject, $"'{type.Name}' has no property or field of that name. Name the member with nameof, which the compiler checks")
                : (subject, OfOtherAssembly(type));
        }

        var where = SymbolEqualityComparer.Default.Equals(declared.ContainingType, type) ? declared.Name : $"{declared.ContainingType.Name}.{declared.Name}";
        return (subject, $"Mark '{where}' {sample}");

        bool UsesIdioma(IAssemblySymbol assembly) => usesIdioma.GetOrAdd(assembly, LocalizationDiscovery.ReferencesPackage);

        static string OfOtherAssembly(INamedTypeSymbol type) =>
            $"'{type.Name}' is a type of '{type.ContainingAssembly.Name}', which doesn't use Idioma, so it has no texts written as attributes";
    }

    /// <summary>
    /// Whether a type is one of this assembly's own, whose attributes the build sees.
    /// </summary>
    private static bool IsOwn(INamedTypeSymbol type, Compilation compilation)
    {
        var isOwn = SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly);
        return isOwn;
    }

    /// <summary>
    /// Whether a member or a type is marked with the generated <c>[Localized]</c>.
    /// </summary>
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

        // In what Razor generated from the user's markup: the attribute compiles, and the generator never saw it. It does
        // see a file of declarations, which Razor writes from SDK 11 on.
        if (context.IsGeneratedCode && !UnnamedLanguageAnalyzer.IsRazorDeclaration(attribute.SyntaxTree))
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
    /// Where to move a <c>[Localized]</c> that Razor generated from markup. A member of a component, or an attribute on
    /// the component itself, moves to a code-behind file, because the component's markup has to stay in its
    /// <c>.razor</c> file. A class declared in markup moves to a C# file as a whole.
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
    /// The enums whose texts a set of this compilation holds, which it lists as <c>ILocalizationSet&lt;TEnum&gt;</c>.
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
    /// Reports each member of an enum without <c>[Localized]</c>, where another member of the enum has it
    /// (<c>LOC501</c>). Not for an enum that a set holds the texts of, which gets its <c>GetText()</c> from the set.
    /// </summary>
    private static void AnalyzeEnum(SyntaxNodeAnalysisContext context, string sample, Lazy<ImmutableHashSet<INamedTypeSymbol>> enumsWithSets)
    {
        var declaration = (EnumDeclarationSyntax)context.Node;

        if (!UnnamedLanguageAnalyzer.IsHandWritten(context.IsGeneratedCode, declaration.SyntaxTree))
            return;

        var marked = new List<EnumMemberDeclarationSyntax>();
        var unmarked = new List<EnumMemberDeclarationSyntax>();

        foreach (var member in declaration.Members)
        {
            // By its name first, which binds nothing: nearly every enum has no such attribute.
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

        // A member with the value of a marked member, such as Default = Active, shows that member's text, because
        // GetText() finds a text by the value.
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

    /// <summary>
    /// The value of a member of an enum, or <see langword="null"/> when it has none, as in code that doesn't compile.
    /// </summary>
    private static object? FindValue(EnumMemberDeclarationSyntax member, SyntaxNodeAnalysisContext context)
    {
        var value = context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken) is { HasConstantValue: true } field
            ? field.ConstantValue
            : null;

        return value;
    }

    /// <summary>
    /// Whether an attribute is the generated <c>[Localized]</c> under another name, as after
    /// <c>using L = CodeChops.Idioma.LocalizedAttribute;</c>. The source generator finds the attribute by its name, so such a member
    /// compiles and gets no text.
    /// </summary>
    // By the using directives of its file first, which binds nothing: nearly no attribute is written through an alias.
    // An alias that another file declares with 'global using' isn't followed.
    private static bool IsWrittenThroughAlias(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        if (attribute.Name is not IdentifierNameSyntax { Identifier.ValueText: var name }
            || LocalizedMembers.IsNamedLocalized(attribute)
            || !UnnamedLanguageAnalyzer.IsHandWritten(context.IsGeneratedCode, attribute.SyntaxTree))
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
    /// Whether an attribute is the generated <c>[Localized]</c>, in code that is the user's own. Also one whose
    /// arguments don't compile, such as one that leaves a language out: its member is marked all the same.
    /// </summary>
    private static bool IsGeneratedAttribute(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        // By its name first, which binds nothing: nearly every attribute is another one.
        if (!LocalizedMembers.IsNamedLocalized(attribute) || !UnnamedLanguageAnalyzer.IsHandWritten(context.IsGeneratedCode, attribute.SyntaxTree))
            return false;

        var symbol = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken);

        var isGeneratedAttribute = (symbol.Symbol ?? symbol.CandidateSymbols.FirstOrDefault()) is IMethodSymbol constructor
                                   && LocalizedMembers.IsGeneratedAttribute(constructor.ContainingType, context.Compilation.Assembly);

        return isGeneratedAttribute;
    }

    /// <summary>
    /// The language of the first argument that is the constant <see langword="null"/>, or <see langword="null"/>
    /// when every argument holds a wording.
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
            var argument = arguments[i];

            // An argument without a name takes the parameter of its place.
            var parameter = argument.NameColon?.Name.Identifier.ValueText
                            ?? (argument.NameEquals is null && i < constructor.Parameters.Length ? constructor.Parameters[i].Name : null);

            if (parameter is not null && context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken) is { HasValue: true, Value: null })
                return SupportedLanguages.FindLanguageOfParameter(parameter, codes);
        }

        return null;
    }
}
