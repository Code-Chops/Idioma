using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds a text that takes values to the parameters of its contract, in every language: to their names
/// (<c>LOC401</c>), and to the values the default wording shows (<c>LOC402</c>).
/// </summary>
/// <remarks>
/// <para>
/// The compiler holds a set to the types of the parameters and to their order, not to their names, so two parameters of
/// one type can swap unnoticed: <c>Invited(string guest, string host)</c> in the Dutch set where the contract has
/// <c>Invited(string host, string guest)</c> compiles, and words the host as the guest for Dutch users. Nor does it
/// mind a parameter a method never uses: a Dutch <c>Greeting(string name) => "Hallo!"</c> compiles, and Dutch users
/// don't see the name.
/// </para>
/// <para>
/// A value counts as left out when the method does not use it at all, and only where the default wording shows it,
/// which the generated contract records on the method (<c>[ShownValue]</c>). For a contract written by hand, it's read
/// from the default-language set that implements it in the same assembly. A value the default wording only
/// chooses by, such as a form of address, may mean nothing in a language that makes no such distinction. Each method
/// of a set that implements a text of a contract is checked, whether it implements the text by its name, implements
/// it explicitly, or overrides the method of a set it derives from, also for a contract of another assembly.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextParametersAnalyzer : DiagnosticAnalyzer
{
    public const string ParameterNamesDifferDiagnosticId = "LOC401";
    public const string ValueLeftOutDiagnosticId = "LOC402";

    /// <summary>
    /// The property of <c>LOC401</c> that holds the names the contract gives the text's parameters, in their order,
    /// separated by commas: <c>host,guest</c>. Its code fix names the parameters from it, also in a workspace that
    /// hasn't run the generator that writes the contract.
    /// </summary>
    public const string ContractParameterNamesProperty = "ContractParameterNames";

    private static readonly DiagnosticDescriptor ParameterNamesDifferRule = new(
        id: ParameterNamesDifferDiagnosticId,
        title: "Parameter of a text named differently than in its contract",
        messageFormat: "'{0}.{1}' names this parameter '{2}', but '{3}.{1}' names it '{4}'. Use the same parameter names in every language, so that two values of one type can't swap unnoticed.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A text with parameters is a method that every language implements with the parameters of the contract. The compiler checks their types and order, but not their names. So a set that declares two parameters of one type the other way round compiles, and swaps the two values.",
        helpLinkUri: PackageNames.HelpLink(ParameterNamesDifferDiagnosticId));

    // LOC402 is the code of an inline text that leaves a value out too, which InlineTextValuesAnalyzer reports
    // (SharedRules).
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [ParameterNamesDifferRule, SharedRules.ValueLeftOut];

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

        // A package without the attribute records no values to hold a set to.
        var shownValue = context.Compilation.GetTypeByMetadataName(PackageNames.ShownValueAttribute);

        // What the default wording of a text of a contract written by hand shows, read once per text for the compilation.
        var shownByHand = new ConcurrentDictionary<IMethodSymbol, bool[]?>(SymbolEqualityComparer.Default);

        // On the declaration, for its semantic model: whether a method uses a value is read from its body.
        context.RegisterSyntaxNodeAction(c => AnalyzeMethod(c, localizationSet, shownValue, shownByHand), SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol localizationSet, INamedTypeSymbol? shownValue,
        ConcurrentDictionary<IMethodSymbol, bool[]?> shownByHand)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;

        // By its shape first, which binds nothing: a method without parameters is no text that takes values.
        if (declaration.ParameterList.Parameters.Count is 0
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not
            {
                IsStatic: false,
                MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation,
                ReturnType.SpecialType: SpecialType.System_String,
                ContainingType: { TypeKind: TypeKind.Class } set,
            } method
            || !set.AllInterfaces.Contains(localizationSet, SymbolEqualityComparer.Default))
        {
            return;
        }

        // Read once, and only for a text that has a value to hold the method to.
        ImmutableArray<ISymbol>? used = null;

        // The language of the set as LOC402 names it: 'nl', or "that language" when the build can't read it.
        string? language = null;

        // A partial method is implemented by the part that holds its body, and the set names the part that declares it
        // as the implementation of a text. Each part is read for itself: the names where it declares them, and the
        // values where its body uses them. So LOC401 is reported at the declaring part alone.
        var declaringPart = method.PartialDefinitionPart;

        foreach (var text in ContractTexts.FindImplementedTexts(method, set).OfType<IMethodSymbol>())
        {
            var reported = false;

            // The names of the contract for the code fix of LOC401, made once per text that a name differs from.
            ImmutableDictionary<string, string?>? properties = null;

            for (var i = 0; i < method.Parameters.Length && i < text.Parameters.Length; i++)
            {
                var parameter = method.Parameters[i];
                var expected = text.Parameters[i];
                var location = parameter.Locations.FirstOrDefault() ?? method.Locations[0];

                if (declaringPart is null && !String.Equals(parameter.Name, expected.Name, StringComparison.Ordinal))
                {
                    reported = true;
                    properties ??= ImmutableDictionary<string, string?>.Empty.Add(ContractParameterNamesProperty, String.Join(",", text.Parameters.Select(static parameter => parameter.Name)));

                    context.ReportDiagnostic(Diagnostic.Create(ParameterNamesDifferRule, location, properties, set.Name, text.Name, parameter.Name, text.ContainingType.Name, expected.Name));
                }

                if (!IsShown(text, expected, shownValue, shownByHand, context.Compilation, context.CancellationToken))
                    continue;

                used ??= FindUsedValues(declaration, context.SemanticModel);

                // Not for a method without a body, or one that does not compile: nothing then to say about its values.
                if (used is { IsDefault: false } values && !values.Contains(parameter, SymbolEqualityComparer.Default))
                {
                    reported = true;

                    // Read only for a value left out, as it may bind the classes the set derives from.
                    language ??= LocalizationDiscovery.ReadLanguage(set, context.SemanticModel.Compilation, context.CancellationToken) is { } code
                        ? $"'{code}'"
                        : "that language";

                    context.ReportDiagnostic(Diagnostic.Create(SharedRules.ValueLeftOut, location, $"'{set.Name}.{text.Name}'", $"its parameter '{parameter.Name}'", $"the default wording of '{text.ContainingType.Name}.{text.Name}'", parameter.Name, language));
                }
            }

            // Once, though the method may implement the text of several contracts.
            if (reported)
                return;
        }
    }

    /// <summary>
    /// What a method reads in its body, or a default array for a method without a body, or with one that can't be
    /// read, as code that does not compile.
    /// </summary>
    private static ImmutableArray<ISymbol> FindUsedValues(MethodDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        var flow = declaration switch
        {
            { ExpressionBody.Expression: { } expression } => semanticModel.AnalyzeDataFlow(expression),
            { Body: { } body } => semanticModel.AnalyzeDataFlow(body),
            _ => null,
        };

        var used = flow is { Succeeded: true } ? flow.ReadInside : default;
        return used;
    }

    /// <summary>
    /// Whether the default wording of a text shows the value of a parameter. A generated contract records it on the
    /// text (<c>[ShownValue]</c>). For a contract written by hand, it's read from the default-language set that
    /// implements the text in this compilation, if any (<see cref="TextParameters.FindShownByDefaultSet"/>).
    /// </summary>
    private static bool IsShown(IMethodSymbol text, IParameterSymbol parameter, INamedTypeSymbol? shownValue,
        ConcurrentDictionary<IMethodSymbol, bool[]?> shownByHand, Compilation compilation, CancellationToken cancellationToken)
    {
        var recorded = false;

        foreach (var attribute in text.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, shownValue))
                continue;

            recorded = true;

            if (attribute.ConstructorArguments is [{ Value: string name }] && String.Equals(name, parameter.Name, StringComparison.Ordinal))
                return true;
        }

        // A generated contract records every value its default wording shows, so a value it doesn't record isn't shown.
        if (recorded || PackageNames.IsGeneratedContract(text.ContainingType))
            return false;

        var shown = shownByHand.GetOrAdd(text, written => TextParameters.FindShownByDefaultSet(written, compilation, cancellationToken));

        var isShown = shown is not null && parameter.Ordinal < shown.Length && shown[parameter.Ordinal];
        return isShown;
    }
}
