using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds a text that takes values to the parameter names of its contract, in every language (<c>LOC020</c>):
/// <c>Invited(string guest, string host)</c> in the Dutch set where the contract has
/// <c>Invited(string host, string guest)</c> compiles, and words the host as the guest for the users of one language.
/// </summary>
/// <remarks>
/// The compiler holds a set to the types of the parameters and to their order, not to their names, so two parameters of
/// one type can swap unnoticed. Each method of a set that implements a text of a contract is checked, whether it
/// implements the text by its name, implements it explicitly, or overrides the method of a set it derives from, also
/// for a contract of another assembly.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextParameterNamesAnalyzer : DiagnosticAnalyzer
{
    public const string ParameterNamesDifferDiagnosticId = "LOC020";

    private static readonly DiagnosticDescriptor ParameterNamesDifferRule = new(
        id: ParameterNamesDifferDiagnosticId,
        title: "Parameter of a text named differently than in its contract",
        messageFormat: "'{0}.{1}' names this parameter '{2}', and '{3}.{1}' names it '{4}'. Give the parameters of a text the same names in every language: otherwise two values of one type can swap unnoticed for the users of one language.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A text that takes values is a method, which every language implements with the parameters of the contract. The compiler checks their types and their order, and not their names, so a set that declares two parameters of one type the other way round compiles, and words each value as the other.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(ParameterNamesDifferRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // A set is written by hand, and what a generator writes is not its author's to change.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(PackageNames.TranslationSet) is not { } translations)
            return;

        context.RegisterSymbolAction(c => AnalyzeMethod(c, translations), SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol translations)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (method is not
            {
                IsStatic: false,
                MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation,
                Parameters.Length: > 0,
                ReturnType.SpecialType: SpecialType.System_String,
                ContainingType: { TypeKind: TypeKind.Class } set,
            }
            || !set.AllInterfaces.Contains(translations, SymbolEqualityComparer.Default))
        {
            return;
        }

        foreach (var text in FindImplementedTexts(method, set, translations))
        {
            var differs = false;

            for (var i = 0; i < method.Parameters.Length && i < text.Parameters.Length; i++)
            {
                var parameter = method.Parameters[i];
                var expected = text.Parameters[i];

                if (String.Equals(parameter.Name, expected.Name, StringComparison.Ordinal))
                    continue;

                differs = true;
                context.ReportDiagnostic(Diagnostic.Create(
                    ParameterNamesDifferRule,
                    parameter.Locations.FirstOrDefault() ?? method.Locations[0],
                    set.Name,
                    text.Name,
                    parameter.Name,
                    text.ContainingType.Name,
                    expected.Name));
            }

            // Once, though the method may implement the text of several contracts.
            if (differs)
                return;
        }
    }

    /// <summary>
    /// The texts of the contracts a set's method implements or overrides the implementation of: those it implements
    /// explicitly, else those the set implements through it.
    /// </summary>
    private static IEnumerable<IMethodSymbol> FindImplementedTexts(IMethodSymbol method, INamedTypeSymbol set, INamedTypeSymbol translations)
    {
        if (!method.ExplicitInterfaceImplementations.IsEmpty)
            return method.ExplicitInterfaceImplementations.Where(text => IsContract(text.ContainingType, translations));

        if (method.DeclaredAccessibility is not Accessibility.Public)
            return [];

        // Asked of the set which method implements the text, and not told by the name: a method of the same name with
        // other parameters implements nothing.
        var implementedTexts = set.AllInterfaces
            .Where(@interface => IsContract(@interface, translations))
            .SelectMany(contract => contract.GetMembers(method.Name).OfType<IMethodSymbol>())
            .Where(text => IsOrOverrides(method, set.FindImplementationForInterfaceMember(text)));

        return implementedTexts;
    }

    /// <summary>
    /// Whether a method is the one that implements a text, or overrides it: a set deriving from another names the
    /// method of that set as the implementation, and its own method is what words the text.
    /// </summary>
    private static bool IsOrOverrides(IMethodSymbol method, ISymbol? implementation)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
            if (SymbolEqualityComparer.Default.Equals(current, implementation))
                return true;

        return false;
    }

    private static bool IsContract(INamedTypeSymbol @interface, INamedTypeSymbol translations)
    {
        var isContract = !SymbolEqualityComparer.Default.Equals(@interface, translations)
                         && @interface.AllInterfaces.Contains(translations, SymbolEqualityComparer.Default);
        return isContract;
    }
}
