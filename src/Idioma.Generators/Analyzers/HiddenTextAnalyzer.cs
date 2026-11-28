using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a member of a set that hides a text of a class it derives from, instead of overriding it (<c>LOC302</c>).
/// </summary>
/// <remarks>
/// A derived set implements the contract through its base class, so a hiding member is never shown. A set that lists
/// the contract again maps it to the hiding member, so that member isn't reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HiddenTextAnalyzer : DiagnosticAnalyzer
{
    public const string HiddenTextDiagnosticId = "LOC302";

    /// <summary>
    /// The diagnostic property with the contract's full name, for the code fix that lists the contract again.
    /// </summary>
    public const string ContractProperty = "Contract";

    private static readonly DiagnosticDescriptor HiddenTextRule = new(
        id: HiddenTextDiagnosticId,
        title: "Text hidden instead of overridden",
        messageFormat: "'{0}.{1}' hides '{2}.{1}' instead of overriding it, so users of {3} won't see its wording. {4}.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A set that derives from another implements the contract through the class it derives from. A member that hides a text of that class, as one marked 'new' does, is never shown, because the contract calls the member it hides. Override the text instead, which needs it to be virtual. Where it can't be, list the contract again next to the class the set derives from, which maps the contract to the set's own member.",
        helpLinkUri: PackageNames.HelpLink(HiddenTextDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [HiddenTextRule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // A set is written by hand.
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
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, BaseType.SpecialType: not SpecialType.System_Object } set
            || !set.AllInterfaces.Contains(localizationSet, SymbolEqualityComparer.Default))
        {
            return;
        }

        string? language = null;

        foreach (var contract in set.AllInterfaces)
        {
            if (!PackageNames.IsContract(contract))
                continue;

            ImmutableDictionary<string, string?>? properties = null;

            foreach (var text in contract.GetMembers())
            {
                // A text the set implements itself isn't hidden.
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

                    language ??= AnalyzerSupport.DescribeLanguage(set, context.Compilation, context.CancellationToken);

                    properties ??= ImmutableDictionary<string, string?>.Empty.Add(ContractProperty, contract.ToDisplayString());

                    // The member an override would override, which may be a sealed override between the two.
                    var hidden = ContractTexts.IsExplicitImplementation(implementation) ? implementation : FindHidden(member, set) ?? implementation;

                    context.ReportDiagnostic(Diagnostic.Create(
                        HiddenTextRule,
                        member.Locations[0],
                        properties,
                        set.Name,
                        text.Name,
                        hidden.ContainingType.Name,
                        language,
                        GetAdvice(text.Name, hidden, implementation, set, contract.Name, context.Compilation)));
                }
            }
        }
    }

    /// <summary>
    /// The advice: override, make virtual or unsealed and override, or list the contract again where nothing can be
    /// overridden, as for another assembly's member or an explicit implementation.
    /// </summary>
    /// <param name="hidden">The member an override would override (<see cref="FindHidden"/>).</param>
    /// <param name="implementation">The member that implements the text.</param>
    private static string GetAdvice(string text, ISymbol hidden, ISymbol implementation, INamedTypeSymbol set, string contract, Compilation compilation)
    {
        // The contract is listed again in the set's own base list, next to the class it derives from.
        var derivedFrom = set.BaseType!.Name;
        var listAgain = $"list '{contract}' again next to '{derivedFrom}'";
        var holder = hidden.ContainingType.Name;

        // An explicit implementation can be neither virtual nor overridden.
        if (ContractTexts.IsExplicitImplementation(implementation))
            return $"List '{contract}' again next to '{derivedFrom}', because '{holder}' implements '{text}' explicitly, which can't be overridden";

        // A member of a class in between, such as one marked new, would be overridden instead of the text.
        if (!Overrides.IsOrOverrides(hidden, implementation))
            return $"List '{contract}' again next to '{derivedFrom}', because '{holder}.{text}' hides the text too";

        if (hidden is { IsVirtual: true } or { IsAbstract: true } or { IsOverride: true, IsSealed: false })
            return $"Override '{holder}.{text}' instead, or {listAgain}";

        var isOwn = SymbolEqualityComparer.Default.Equals(hidden.ContainingAssembly, compilation.Assembly);

        if (hidden.IsSealed)
        {
            return isOwn
                ? $"Remove 'sealed' from '{holder}.{text}' and override it, or {listAgain}"
                : $"List '{contract}' again next to '{derivedFrom}', because '{holder}.{text}' is sealed and lies in another assembly";
        }

        if (isOwn)
            return $"Make '{holder}.{text}' virtual and override it, or {listAgain}";

        return $"List '{contract}' again next to '{derivedFrom}', because '{holder}.{text}' isn't virtual and lies in another assembly";
    }

    /// <summary>
    /// The first member of a class the set derives from that a member hides, which an override would override, or
    /// <see langword="null"/>. The code fix uses it too.
    /// </summary>
    internal static ISymbol? FindHidden(ISymbol member, INamedTypeSymbol set)
    {
        for (var type = set.BaseType; type is not null; type = type.BaseType)
        {
            foreach (var candidate in type.GetMembers(member.Name))
                if (Hides(member, candidate))
                    return candidate;
        }

        return null;
    }

    /// <summary>
    /// Whether a member hides the member that implements a text: a public, non-override property or method of the same
    /// signature. The code fix uses it too.
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
}
