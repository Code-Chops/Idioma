using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds a text that takes values to the parameters of its contract, in every language: to their names
/// (<c>LOC020</c>), and to the values the default wording shows (<c>LOC021</c>).
/// </summary>
/// <remarks>
/// <para>
/// The compiler holds a set to the types of the parameters and to their order, not to their names, so two parameters of
/// one type can swap unnoticed: <c>Invited(string guest, string host)</c> in the Dutch set where the contract has
/// <c>Invited(string host, string guest)</c> compiles, and words the host as the guest for the users of one language.
/// Nor does it mind a parameter a method never uses: a Dutch <c>Greeting(string name) => "Hallo!"</c> compiles, and
/// leaves the name out for the users of one language.
/// </para>
/// <para>
/// A value counts as left out when the method does not use it at all, and only where the default wording shows it,
/// which the generated contract records on the parameter (<c>[ShownValue]</c>). A value the default wording only
/// chooses by, such as a form of address, may mean nothing in a language that makes no such distinction. Each method
/// of a set that implements a text of a contract is checked, whether it implements the text by its name, implements
/// it explicitly, or overrides the method of a set it derives from, also for a contract of another assembly.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextParametersAnalyzer : DiagnosticAnalyzer
{
    public const string ParameterNamesDifferDiagnosticId = "LOC020";
    public const string ValueLeftOutDiagnosticId = "LOC021";

    private static readonly DiagnosticDescriptor ParameterNamesDifferRule = new(
        id: ParameterNamesDifferDiagnosticId,
        title: "Parameter of a text named differently than in its contract",
        messageFormat: "'{0}.{1}' names this parameter '{2}', and '{3}.{1}' names it '{4}'. Give the parameters of a text the same names in every language: otherwise two values of one type can swap unnoticed for the users of one language.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A text that takes values is a method, which every language implements with the parameters of the contract. The compiler checks their types and their order, and not their names, so a set that declares two parameters of one type the other way round compiles, and words each value as the other.");

    private static readonly DiagnosticDescriptor ValueLeftOutRule = new(
        id: ValueLeftOutDiagnosticId,
        title: "Value of a text left out of its wording",
        messageFormat: "'{0}.{1}' does not use its parameter '{2}', which the default wording of '{3}.{1}' shows. Use the value in the wording of every language: otherwise it is left out for the users of one language.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A text that takes values is a method, which every language implements. The compiler does not mind a parameter a method never uses, so wording that leaves a value out compiles. A value the default wording shows must be used by every language; a value it only chooses a wording by, such as a form of address, may go unused in a language that makes no such distinction.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(ParameterNamesDifferRule, ValueLeftOutRule);

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

        // A package without the attribute records no values to hold a set to.
        var shownValue = context.Compilation.GetTypeByMetadataName(PackageNames.ShownValueAttribute);

        // On the declaration, for its semantic model: whether a method uses a value is read from its body.
        context.RegisterSyntaxNodeAction(c => AnalyzeMethod(c, translations, shownValue), SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol translations, INamedTypeSymbol? shownValue)
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
            || !set.AllInterfaces.Contains(translations, SymbolEqualityComparer.Default))
        {
            return;
        }

        // Read once, and only for a text that has a value to hold the method to.
        ImmutableArray<ISymbol>? used = null;

        foreach (var text in FindImplementedTexts(method, set, translations))
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

                if (shownValue is null || !IsShown(expected, shownValue))
                    continue;

                used ??= FindUsedValues(declaration, context.SemanticModel);

                // Not for a method without a body, or one that does not compile: nothing then to say about its values.
                if (used is { IsDefault: false } values && !values.Contains(parameter, SymbolEqualityComparer.Default))
                {
                    reported = true;
                    context.ReportDiagnostic(Diagnostic.Create(ValueLeftOutRule, location, set.Name, text.Name, parameter.Name, text.ContainingType.Name));
                }
            }

            // Once, though the method may implement the text of several contracts.
            if (reported)
                return;
        }
    }

    /// <summary>
    /// What a method reads in its body, or a default array for a method without a body, or with one that cannot be
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
    /// Whether the contract records that the default wording shows a value.
    /// </summary>
    private static bool IsShown(IParameterSymbol parameter, INamedTypeSymbol shownValue)
    {
        foreach (var attribute in parameter.GetAttributes())
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, shownValue))
                return true;

        return false;
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
