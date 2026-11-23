using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a member of a set that hides a text of a class it derives from, instead of overriding it (<c>LOC207</c>).
/// </summary>
/// <remarks>
/// <para>
/// A set that derives from another, such as a regional set or an app's rewording of a library's set, implements the
/// contract through the class it derives from. The contract then calls the member of that class. So a member of the
/// same name that hides it, as <c>public new string Color => "Colour";</c> does, is never shown. It compiles without a
/// warning, because <see langword="new"/> is what the IDE adds for the compiler's warning about hiding (<c>CS0108</c>).
/// </para>
/// <para>
/// A set that lists the contract again next to the class it derives from maps the contract to the hiding member, which
/// then words the text. Such a member isn't reported. Neither is a member that hides something that isn't a text, nor
/// a method that only shares its name with a text.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HiddenTextAnalyzer : DiagnosticAnalyzer
{
    public const string HiddenTextDiagnosticId = "LOC207";

    /// <summary>
    /// The property of a diagnostic that holds the full name of the contract whose text is hidden, such as
    /// <c>TestApp.IShopLocalization</c>, for the code fix that lists the contract again.
    /// </summary>
    public const string ContractProperty = "Contract";

    private static readonly DiagnosticDescriptor HiddenTextRule = new(
        id: HiddenTextDiagnosticId,
        title: "Text hidden instead of overridden",
        messageFormat: "'{0}.{1}' hides '{2}.{1}' instead of overriding it, so users of {3} won't see its wording. Make '{2}.{1}' virtual and override it, or list '{4}' again next to '{2}'.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A set that derives from another implements the contract through the class it derives from. A member that hides a text of that class, as one marked 'new' does, is never shown, because the contract calls the member it hides. Override the text instead, which needs it to be virtual. Where it can't be, list the contract again next to the class the set derives from, which maps the contract to the set's own member.",
        helpLinkUri: PackageNames.HelpLink(HiddenTextDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [HiddenTextRule];

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
        // Only a class that derives from another can hide a member.
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, BaseType.SpecialType: not SpecialType.System_Object } set
            || !set.AllInterfaces.Contains(localizationSet, SymbolEqualityComparer.Default))
        {
            return;
        }

        // The language of the set as the message names it: 'en-GB', or "that language" when the build can't read it.
        string? language = null;

        foreach (var contract in set.AllInterfaces)
        {
            if (!IsContract(contract, localizationSet))
                continue;

            ImmutableDictionary<string, string?>? properties = null;

            foreach (var text in contract.GetMembers())
            {
                // A text the set implements itself, as it does after listing the contract again, isn't hidden.
                if (text is not (IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary })
                    || set.FindImplementationForInterfaceMember(text) is not { } implementation
                    || SymbolEqualityComparer.Default.Equals(implementation.ContainingType, set))
                {
                    continue;
                }

                foreach (var member in set.GetMembers(text.Name))
                {
                    if (!Hides(member, implementation))
                        continue;

                    language ??= LocalizationDiscovery.ReadLanguage(set, context.Compilation, context.CancellationToken) is { } code
                        ? $"'{code}'"
                        : "that language";

                    properties ??= ImmutableDictionary<string, string?>.Empty.Add(ContractProperty, contract.ToDisplayString());

                    context.ReportDiagnostic(Diagnostic.Create(
                        HiddenTextRule,
                        member.Locations[0],
                        properties,
                        set.Name,
                        text.Name,
                        implementation.ContainingType.Name,
                        language,
                        contract.Name));
                }
            }
        }
    }

    /// <summary>
    /// Whether a member of a set hides the member that implements a text: a public property of its name, or a public
    /// method of its name with the same parameters, that doesn't override it. The code fix asks the same of each class
    /// the set derives from, to find the member an override would override.
    /// </summary>
    internal static bool Hides(ISymbol member, ISymbol implementation)
    {
        var hides = (member, implementation) switch
        {
            (IPropertySymbol { IsStatic: false, IsOverride: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public, Type.SpecialType: SpecialType.System_String }, IPropertySymbol) => true,
            (IMethodSymbol { IsStatic: false, IsOverride: false, MethodKind: MethodKind.Ordinary, DeclaredAccessibility: Accessibility.Public, ReturnType.SpecialType: SpecialType.System_String } method, IMethodSymbol implementing)
                => method.Parameters.Length == implementing.Parameters.Length
                   && method.Parameters.Zip(implementing.Parameters, static (parameter, other)
                       => SymbolEqualityComparer.Default.Equals(parameter.Type, other.Type) && parameter.RefKind == other.RefKind).All(static same => same),
            _ => false,
        };

        return hides;
    }

    private static bool IsContract(INamedTypeSymbol @interface, INamedTypeSymbol localizationSet)
    {
        var isContract = !SymbolEqualityComparer.Default.Equals(@interface, localizationSet) && PackageNames.IsContract(@interface);
        return isContract;
    }
}
