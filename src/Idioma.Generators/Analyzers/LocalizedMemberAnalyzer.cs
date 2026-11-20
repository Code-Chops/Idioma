using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a member marked <c>[Localized]</c> that gets no text (<c>LOC400</c>), and a member of an enum without
/// <c>[Localized]</c> next to members that have it (<c>LOC503</c>). Without these, <c>GetTextByMember</c> would
/// silently give nothing, and <c>GetText()</c> would silently show the member's name.
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
/// <c>.cshtml</c> file, because no source generator sees what another writes.</item>
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
    public const string CannotRegisterDiagnosticId = LocalizationRegistrationGenerator.CannotRegisterDiagnosticId;
    public const string UnmarkedEnumMemberDiagnosticId = "LOC503";

    // The code of a set that can't be registered, which the generator reports. Both say that texts won't be found.
    // So the code is shared on purpose.
#pragma warning disable RS1019 // Diagnostic Id is already used by another analyzer
    private static readonly DiagnosticDescriptor CannotRegisterRule = new(
        id: CannotRegisterDiagnosticId,
        title: "Text of a member can't be registered",
        messageFormat: "The text of '{0}' can't be registered because {1}, so it won't be found. {2}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The source generator reads each [Localized] as the project builds, and writes its text as code at the top level of the assembly. That code can't name a private or protected nested type, or a file-local one. The generator can't read what Razor generates from a .razor or .cshtml file either. A wording that is null gives no text, and neither does an attribute on something a text isn't found by, such as an indexer. Without this warning, LocalizableText.GetTextByMember would silently give nothing for the member.",
        helpLinkUri: PackageNames.HelpLink(CannotRegisterDiagnosticId));
#pragma warning restore RS1019

    private static readonly DiagnosticDescriptor UnmarkedEnumMemberRule = new(
        id: UnmarkedEnumMemberDiagnosticId,
        title: "Enum member without a text next to members that have one",
        messageFormat: "'{0}.{1}' has no text, but other members of '{0}' have one, so users would see '{1}' as written. Mark it {2}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An enum whose members are marked [Localized] shows the name of a member that isn't, in every language. That's usually a member added later. Mark it too. Where every member of an enum needs a text, a set of the enum (ILocalizationSet<TEnum>) makes a missing one a build error. An enum with a text of its own and no marked member isn't reported, because its values may never be shown.",
        helpLinkUri: PackageNames.HelpLink(UnmarkedEnumMemberDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [CannotRegisterRule, UnmarkedEnumMemberRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a member marked in markup must be caught there.
        // OneLanguageTextAnalyzer.IsHandWritten leaves out the rest.
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
        context.RegisterSyntaxNodeAction(c => AnalyzeEnum(c, sample), SyntaxKind.EnumDeclaration);
    }

    /// <summary>
    /// Reports a <c>[Localized]</c> that gives its member no text (<c>LOC400</c>).
    /// </summary>
    private static void AnalyzeLocalized(SyntaxNodeAnalysisContext context, ImmutableArray<string> codes)
    {
        var attribute = (AttributeSyntax)context.Node;

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

        // In what Razor generated from the user's markup: the attribute compiles, and the generator never saw it.
        if (context.IsGeneratedCode)
        {
            Report("it's written in markup, which the source generator can't read", $"Move '{type?.Name ?? member.Name}' to a C# file");
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
    /// Reports each member of an enum without <c>[Localized]</c>, where another member of the enum has it
    /// (<c>LOC503</c>).
    /// </summary>
    private static void AnalyzeEnum(SyntaxNodeAnalysisContext context, string sample)
    {
        var declaration = (EnumDeclarationSyntax)context.Node;

        if (!OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, declaration.SyntaxTree))
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
    /// Whether an attribute is the generated <c>[Localized]</c>, in code that is the user's own. Also one whose
    /// arguments don't compile, such as one that leaves a language out: its member is marked all the same.
    /// </summary>
    private static bool IsGeneratedAttribute(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
    {
        // By its name first, which binds nothing: nearly every attribute is another one.
        if (!LocalizedMembers.IsNamedLocalized(attribute) || !OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, attribute.SyntaxTree))
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
