using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds a text that takes values to the parameters of its contract, in every language: to their names
/// (<c>LOC401</c>), and to the values the default wording shows (<c>LOC402</c>).
/// </summary>
/// <remarks>
/// <para>
/// The compiler checks the types and order of the parameters, not their names, so two of one type can swap unnoticed.
/// </para>
/// <para>
/// A value counts as left out when the method doesn't use it, and only where the default wording shows it
/// (<c>[ShownValue]</c>). A value the default wording only chooses by, such as a form of address, may mean nothing in
/// another language.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextParametersAnalyzer : DiagnosticAnalyzer
{
    public const string ParameterNamesDifferDiagnosticId = "LOC401";
    public const string ValueLeftOutDiagnosticId = "LOC402";

    /// <summary>
    /// The property of <c>LOC401</c> that holds the contract's parameter names, as <c>host,guest</c>. The code fix reads
    /// it, so it works also where the generator hasn't run.
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

    // InlineTextValuesAnalyzer reports LOC402 too (SharedRules).
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [ParameterNamesDifferRule, SharedRules.ValueLeftOut];

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

        var shownValue = context.Compilation.GetTypeByMetadataName(PackageNames.ShownValueAttribute);

        // What the default wording of a hand-written contract's text shows, read once per text.
        var shownByHand = new ConcurrentDictionary<IMethodSymbol, bool[]?>(SymbolEqualityComparer.Default);

        // On the declaration, because whether a method uses a value is read from its body.
        context.RegisterSyntaxNodeAction(c => AnalyzeMethod(c, localizationSet, shownValue, shownByHand), SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol localizationSet, INamedTypeSymbol? shownValue,
        ConcurrentDictionary<IMethodSymbol, bool[]?> shownByHand)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;

        // By its shape first, which binds nothing.
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

        // Both read lazily, once.
        ImmutableArray<ISymbol>? used = null;
        string? language = null;

        // Each part of a partial method is read for itself, so LOC401 is reported at the declaring part alone.
        var declaringPart = method.PartialDefinitionPart;

        foreach (var text in ContractTexts.FindImplementedTexts(method, set).OfType<IMethodSymbol>())
        {
            var reported = false;

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

                // Not for a method without a body, or one that doesn't compile.
                if (used is { IsDefault: false } values && !values.Contains(parameter, SymbolEqualityComparer.Default))
                {
                    reported = true;

                    language ??= AnalyzerSupport.DescribeLanguage(set, context.SemanticModel.Compilation, context.CancellationToken);

                    context.ReportDiagnostic(Diagnostic.Create(SharedRules.ValueLeftOut, location, $"'{set.Name}.{text.Name}'", $"its parameter '{parameter.Name}'", $"the default wording of '{text.ContainingType.Name}.{text.Name}'", parameter.Name, language));
                }
            }

            // Once, though the method may implement the text of several contracts.
            if (reported)
                return;
        }
    }

    /// <summary>
    /// What a method reads in its body, or a default array where there's no body or it doesn't compile. A body that only
    /// throws has no wording yet, as where "Implement interface" wrote <c>=> throw new NotImplementedException()</c>, so
    /// it gives a default array too.
    /// </summary>
    private static ImmutableArray<ISymbol> FindUsedValues(MethodDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        var flow = declaration switch
        {
            { ExpressionBody.Expression: ThrowExpressionSyntax } or { Body.Statements: [ThrowStatementSyntax] } => null,
            { ExpressionBody.Expression: { } expression } => semanticModel.AnalyzeDataFlow(expression),
            { Body: { } body } => semanticModel.AnalyzeDataFlow(body),
            _ => null,
        };

        var used = flow is { Succeeded: true } ? flow.ReadInside : default;
        return used;
    }

    /// <summary>
    /// Whether the default wording of a text shows a parameter's value: marked with <c>[ShownValue]</c>, or for a
    /// hand-written contract read from the default-language set (<see cref="TextParameters.FindShownByDefaultSet"/>).
    /// </summary>
    private static bool IsShown(IMethodSymbol text, IParameterSymbol parameter, INamedTypeSymbol? shownValue,
        ConcurrentDictionary<IMethodSymbol, bool[]?> shownByHand, Compilation compilation, CancellationToken cancellationToken)
    {
        var marked = false;

        foreach (var attribute in text.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, shownValue))
                continue;

            marked = true;

            if (attribute.ConstructorArguments is [{ Value: string name }] && String.Equals(name, parameter.Name, StringComparison.Ordinal))
                return true;
        }

        // A generated contract marks every shown value.
        if (marked || PackageNames.IsGeneratedContract(text.ContainingType))
            return false;

        var shown = shownByHand.GetOrAdd(text, written => TextParameters.FindShownByDefaultSet(written, compilation, cancellationToken));

        var isShown = shown is not null && parameter.Ordinal < shown.Length && shown[parameter.Ordinal];
        return isShown;
    }
}
