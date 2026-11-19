using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds a text that takes values to the parameters of its contract, in every language: to their names
/// (<c>LOC301</c>), and to the values the default wording shows (<c>LOC302</c>).
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
/// which the generated contract records on the method (<c>[ShownValue]</c>). A value the default wording only
/// chooses by, such as a form of address, may mean nothing in a language that makes no such distinction. Each method
/// of a set that implements a text of a contract is checked, whether it implements the text by its name, implements
/// it explicitly, or overrides the method of a set it derives from, also for a contract of another assembly.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextParametersAnalyzer : DiagnosticAnalyzer
{
    public const string ParameterNamesDifferDiagnosticId = "LOC301";
    public const string ValueLeftOutDiagnosticId = "LOC302";

    private static readonly DiagnosticDescriptor ParameterNamesDifferRule = new(
        id: ParameterNamesDifferDiagnosticId,
        title: "Parameter of a text named differently than in its contract",
        messageFormat: "'{0}.{1}' names this parameter '{2}', but '{3}.{1}' names it '{4}'. Use the same parameter names in every language, so that two values of one type can't swap unnoticed.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A text with parameters is a method that every language implements with the parameters of the contract. The compiler checks their types and order, but not their names. So a set that declares two parameters of one type the other way round compiles, and swaps the two values.",
        helpLinkUri: PackageNames.HelpLink(ParameterNamesDifferDiagnosticId));

    // The code of an inline text that leaves a value out, which InlineTextValuesAnalyzer reports. The code is shared on
    // purpose.
#pragma warning disable RS1019 // Diagnostic Id is already used by another analyzer
    private static readonly DiagnosticDescriptor ValueLeftOutRule = new(
        id: ValueLeftOutDiagnosticId,
        title: "Value of a text left out of its wording",
        messageFormat: "'{0}.{1}' doesn't use its parameter '{2}', but the default wording of '{3}.{1}' shows it. Use '{2}' in every language, or users of {4} won't see it.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A text with parameters is a method that every language implements. The compiler allows a parameter a method never uses, so wording that leaves a value out compiles. Every language must use a value the default wording shows. A value the default wording only uses to choose between wordings, such as a form of address, may go unused in a language that makes no such distinction.",
        helpLinkUri: PackageNames.HelpLink(ValueLeftOutDiagnosticId));
#pragma warning restore RS1019

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [ParameterNamesDifferRule, ValueLeftOutRule];

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

        // On the declaration, for its semantic model: whether a method uses a value is read from its body.
        context.RegisterSyntaxNodeAction(c => AnalyzeMethod(c, localizationSet, shownValue), SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol localizationSet, INamedTypeSymbol? shownValue)
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

        // The language of the set as LOC302 names it: 'nl', or "that language" when the build can't read it.
        string? language = null;

        foreach (var text in FindImplementedTexts(method, set, localizationSet))
        {
            var reported = false;

            for (var i = 0; i < method.Parameters.Length && i < text.Parameters.Length; i++)
            {
                var parameter = method.Parameters[i];
                var expected = text.Parameters[i];
                var location = parameter.Locations.FirstOrDefault() ?? method.Locations[0];

                if (!String.Equals(parameter.Name, expected.Name, StringComparison.Ordinal))
                {
                    reported = true;
                    context.ReportDiagnostic(Diagnostic.Create(ParameterNamesDifferRule, location, set.Name, text.Name, parameter.Name, text.ContainingType.Name, expected.Name));
                }

                if (shownValue is null || !IsShown(text, expected, shownValue))
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

                    context.ReportDiagnostic(Diagnostic.Create(ValueLeftOutRule, location, set.Name, text.Name, parameter.Name, text.ContainingType.Name, language));
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
    /// Whether the contract records on a text that its default wording shows the value of a parameter.
    /// </summary>
    private static bool IsShown(IMethodSymbol text, IParameterSymbol parameter, INamedTypeSymbol shownValue)
    {
        foreach (var attribute in text.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, shownValue)
                && attribute.ConstructorArguments is [{ Value: string name }]
                && String.Equals(name, parameter.Name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The texts of the contracts a set's method implements or overrides the implementation of: those it implements
    /// explicitly, else those the set implements through it.
    /// </summary>
    private static IEnumerable<IMethodSymbol> FindImplementedTexts(IMethodSymbol method, INamedTypeSymbol set, INamedTypeSymbol localizationSet)
    {
        if (!method.ExplicitInterfaceImplementations.IsEmpty)
            return method.ExplicitInterfaceImplementations.Where(text => IsContract(text.ContainingType, localizationSet));

        if (method.DeclaredAccessibility is not Accessibility.Public)
            return [];

        // Asked of the set which method implements the text, and not told by the name: a method of the same name with
        // other parameters implements nothing.
        var implementedTexts = set.AllInterfaces
            .Where(@interface => IsContract(@interface, localizationSet))
            .SelectMany(contract => contract.GetMembers(method.Name).OfType<IMethodSymbol>())
            .Where(text => IsOrOverrides(method, set.FindImplementationForInterfaceMember(text)));

        return implementedTexts;
    }

    /// <summary>
    /// Whether a method is the one that implements a text, or overrides it. A set deriving from another names the
    /// method of that set as the implementation, and its own method is what words the text.
    /// </summary>
    private static bool IsOrOverrides(IMethodSymbol method, ISymbol? implementation)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
            if (SymbolEqualityComparer.Default.Equals(current, implementation))
                return true;

        return false;
    }

    private static bool IsContract(INamedTypeSymbol @interface, INamedTypeSymbol localizationSet)
    {
        var isContract = !SymbolEqualityComparer.Default.Equals(@interface, localizationSet) && PackageNames.IsContract(@interface);
        return isContract;
    }
}
