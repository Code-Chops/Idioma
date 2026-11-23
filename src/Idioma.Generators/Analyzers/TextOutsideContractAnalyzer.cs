using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a text of a set that its contract doesn't have (<c>LOC209</c>), such as a Dutch text that the
/// default-language set lacks.
/// </summary>
/// <remarks>
/// <para>
/// The contract is written from the texts of the default-language set. A text that only another language declares isn't
/// in it, so no generated text reads it, and no user sees its wording. It compiles without a warning, because a class
/// may have members that its interfaces don't.
/// </para>
/// <para>
/// A member is a text by the shape discovery reads a text by: a public string property, or a method of the shape of a
/// text that takes values. These aren't reported:
/// </para>
/// <list type="bullet">
/// <item>A member marked <c>[ExcludeFromLocalizationDiscovery]</c>, and a method that can't be a text
/// (<c>LOC300</c>).</item>
/// <item>A member of the name of a member of an interface the set implements. What's wrong with it is reported
/// elsewhere: a member that hides a text (<c>LOC207</c>), a property named like a text that takes values
/// (<c>LOC502</c>), or a method with other parameters (<c>CS0535</c>).</item>
/// <item>A member of the name of a member of a class the set derives from. It overrides or hides that member, which is
/// reported where it's declared.</item>
/// </list>
/// <para>
/// So the default-language set isn't reported, because its contract has each text it declares.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextOutsideContractAnalyzer : DiagnosticAnalyzer
{
    public const string TextOutsideContractDiagnosticId = "LOC209";

    private const string ExcludeAdvice = "or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text";

    private static readonly DiagnosticDescriptor TextOutsideContractRule = new(
        id: TextOutsideContractDiagnosticId,
        title: "Text the default-language set lacks",
        messageFormat: "'{0}.{1}' isn't a text of '{2}', so users of {3} won't see it. {4}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The contract is written from the texts of the default-language set. A text that only another language declares isn't in it, so no generated text reads it, and no user sees its wording. Add it to the default-language set, so that every language words it. A set of a library's contract can't add a text to it, so remove the text there. Mark a member that isn't a text [ExcludeFromLocalizationDiscovery].",
        helpLinkUri: PackageNames.HelpLink(TextOutsideContractDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [TextOutsideContractRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // A set is written by hand, and what a generator writes is not its author's to change.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.LocalizationSet) is not { } localizationSet)
            return;

        context.RegisterSymbolAction(c => AnalyzeSet(c, localizationSet), SymbolKind.NamedType);
    }

    private static void AnalyzeSet(SymbolAnalysisContext context, INamedTypeSymbol localizationSet)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } set
            || !set.AllInterfaces.Contains(localizationSet, SymbolEqualityComparer.Default))
        {
            return;
        }

        // The contract the message names, which is the first the set implements. A set without one is LOC204.
        var contract = set.AllInterfaces.FirstOrDefault(@interface => IsContract(@interface, localizationSet));

        if (contract is null)
            return;

        // Read once, and only for a set that has a text to report.
        HashSet<string>? interfaceMembers = null;
        string? language = null;

        foreach (var member in set.GetMembers())
        {
            if (!IsText(member))
                continue;

            interfaceMembers ??= new HashSet<string>(set.AllInterfaces.SelectMany(static @interface => @interface.MemberNames), StringComparer.Ordinal);

            if (interfaceMembers.Contains(member.Name) || HasInheritedMember(set, member.Name))
                continue;

            language ??= LocalizationDiscovery.ReadLanguage(set, context.Compilation, context.CancellationToken) is { } code
                ? $"'{code}'"
                : "that language";

            context.ReportDiagnostic(Diagnostic.Create(
                TextOutsideContractRule,
                member.Locations[0],
                set.Name,
                member.Name,
                contract.Name,
                language,
                Advise(contract, member.Name, context.Compilation)));
        }
    }

    /// <summary>
    /// Whether a member is a text by its shape, as discovery reads the texts of a default-language set: a public string
    /// property, or a method of the shape of a text that takes values that can be one. A member marked
    /// <c>[ExcludeFromLocalizationDiscovery]</c> is no text, and neither is <c>LocalizationLanguageCode</c>.
    /// </summary>
    private static bool IsText(ISymbol member)
    {
        var isText = (LocalizationDiscovery.IsPublicStringProperty(member, out _)
                      || (TextParameters.IsTextMethod(member, out var method) && TextParameters.FindProblem(method) is null))
                     && !String.Equals(member.Name, LocalizationDiscovery.LanguageCodeMemberName, StringComparison.Ordinal)
                     && !LocalizationDiscovery.IsExcluded(member);

        return isText;
    }

    /// <summary>
    /// Whether a class the set derives from has a member of the name, which the set's member overrides or hides.
    /// </summary>
    private static bool HasInheritedMember(INamedTypeSymbol set, string name)
    {
        for (var current = set.BaseType; current is not null && current.SpecialType is not SpecialType.System_Object; current = current.BaseType)
        {
            if (current.GetMembers(name).Length > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// What to do, as the last sentence of the message. A text goes in the default-language set, which a generated
    /// contract is written from, or in a contract written by hand. A set can't add a text to a contract of another
    /// assembly.
    /// </summary>
    private static string Advise(INamedTypeSymbol contract, string text, Compilation compilation)
    {
        if (!SymbolEqualityComparer.Default.Equals(contract.ContainingAssembly, compilation.Assembly))
            return $"Remove it, {ExcludeAdvice}";

        var advice = PackageNames.IsGeneratedContract(contract)
            ? $"Add '{text}' to the default-language set, {ExcludeAdvice}"
            : $"Add '{text}' to '{contract.Name}', {ExcludeAdvice}";

        return advice;
    }

    private static bool IsContract(INamedTypeSymbol @interface, INamedTypeSymbol localizationSet)
    {
        var isContract = !SymbolEqualityComparer.Default.Equals(@interface, localizationSet) && PackageNames.IsContract(@interface);
        return isContract;
    }
}
