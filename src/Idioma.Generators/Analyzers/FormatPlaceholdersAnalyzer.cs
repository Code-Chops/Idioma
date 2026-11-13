using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds the wording of a text in every language to the placeholders of its default wording (<c>LOC016</c>), so that
/// <c>Format</c> neither throws nor leaves a value out for the users of one language: each argument of
/// <c>LocalizableText.Translated</c> to the default language's, and each text of a set to the placeholders the generated
/// contract records for it (<c>[FormatPlaceholders]</c>), also a set of another assembly's contract.
/// </summary>
/// <remarks>
/// Only wording written as a constant is read. A default wording without placeholders holds the others to none, as a
/// text shown without <c>Format</c> shows a placeholder as written. For the same reason a lone brace is reported only in
/// the wording of a text whose default holds placeholders, where <c>Format</c> would throw on it.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FormatPlaceholdersAnalyzer : DiagnosticAnalyzer
{
    public const string PlaceholdersDifferDiagnosticId = "LOC016";

    private const string TranslationsFullName = "CodeChops.Idioma.ITranslations";

    private static readonly DiagnosticDescriptor PlaceholdersDifferRule = new(
        id: PlaceholdersDifferDiagnosticId,
        title: "Placeholders differ between languages",
        messageFormat: "{0} has {1}, while {2} has {3}. Give every language the same placeholders: otherwise Format throws, or leaves out a value, for the users of one language.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Format fills in the placeholders of the wording for the current UI culture with the same values in every language. Wording with a placeholder the default wording lacks makes Format throw for the users of that language, and wording without a placeholder the default wording has leaves that value out.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(PlaceholdersDifferRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Razor's output counts as generated code, and a text written in markup must be checked there.
        // OneLanguageTextAnalyzer.IsHandWritten leaves out the rest.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(Start);
    }

    private static void Start(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(TranslationsFullName) is not { } translations)
            return;

        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

        // A package without the attribute records no placeholders to hold a set to.
        if (context.Compilation.GetTypeByMetadataName(FormatPlaceholders.AttributeFullName) is { } attribute)
            context.RegisterSyntaxNodeAction(c => AnalyzeProperty(c, translations, attribute), SyntaxKind.PropertyDeclaration);
    }

    /// <summary>
    /// Holds each language argument of <c>LocalizableText.Translated</c> to the default language's.
    /// </summary>
    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!OneLanguageTextAnalyzer.IsFactory(invocation.TargetMethod)
            || !OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, invocation.Syntax.SyntaxTree))
        {
            return;
        }

        // The first parameter is the default language.
        var reference = invocation.Arguments.FirstOrDefault(static argument => argument.Parameter?.Ordinal is 0);

        if (reference is not { Parameter: { } referenceParameter, Value.ConstantValue: { HasValue: true, Value: string defaultWording } }
            || !FormatPlaceholders.TryRead(defaultWording, out var expected))
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            if (argument is not { Parameter: { Ordinal: > 0 } parameter, Value.ConstantValue: { HasValue: true, Value: string wording } }
                || FindMismatch(wording, expected) is not { } found)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                PlaceholdersDifferRule,
                argument.Syntax.GetLocation(),
                $"The '{GetLanguage(parameter, context.Compilation)}' wording",
                found,
                $"the '{GetLanguage(referenceParameter, context.Compilation)}' wording",
                FormatPlaceholders.Describe(expected)));
        }
    }

    /// <summary>
    /// Holds a text of a set to the placeholders the contract records for it.
    /// </summary>
    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context, INamedTypeSymbol translations, INamedTypeSymbol attribute)
    {
        var declaration = (PropertyDeclarationSyntax)context.Node;

        if (!OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, declaration.SyntaxTree)
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { IsStatic: false, Type.SpecialType: SpecialType.System_String } property
            || property.ContainingType is not { TypeKind: TypeKind.Class } set
            || !set.AllInterfaces.Contains(translations, SymbolEqualityComparer.Default)
            || GetConstantWording(declaration, context.SemanticModel, context.CancellationToken) is not { } wording)
        {
            return;
        }

        foreach (var text in FindImplementedTexts(property, set, translations))
        {
            if (ReadPlaceholders(text, attribute) is not { } expected || FindMismatch(wording, expected) is not { } found)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                PlaceholdersDifferRule,
                declaration.Identifier.GetLocation(),
                $"'{set.Name}.{text.Name}'",
                found,
                $"the default wording of '{text.ContainingType.Name}.{text.Name}'",
                FormatPlaceholders.Describe(expected)));

            // Once, though the property may implement the text of several contracts.
            return;
        }
    }

    /// <summary>
    /// What a wording holds that the default wording does not, as the message says it, or <see langword="null"/> when
    /// it holds the same placeholders.
    /// </summary>
    private static string? FindMismatch(string wording, ImmutableArray<int> expected)
    {
        if (!FormatPlaceholders.TryRead(wording, out var placeholders))
            return expected.IsEmpty ? null : FormatPlaceholders.InvalidDescription;

        return placeholders.SequenceEqual(expected) ? null : FormatPlaceholders.Describe(placeholders);
    }

    /// <summary>
    /// The language a parameter of the factory stands for, as the attribute lists it: <c>zh-Hans</c> for
    /// <c>zhHans</c>.
    /// </summary>
    // Read from the attribute when a wording is reported, not before: the factory is the assembly's own, written for
    // the languages it lists.
    private static string GetLanguage(IParameterSymbol parameter, Compilation compilation)
    {
        return SupportedLanguages.FindLanguageOfParameter(parameter.Name, SupportedLanguages.ReadCodes(compilation.Assembly));
    }

    /// <summary>
    /// The texts of the contracts a set's property implements or overrides the implementation of: those it implements
    /// explicitly, else those of its name.
    /// </summary>
    private static IEnumerable<IPropertySymbol> FindImplementedTexts(IPropertySymbol property, INamedTypeSymbol set, INamedTypeSymbol translations)
    {
        if (!property.ExplicitInterfaceImplementations.IsEmpty)
            return property.ExplicitInterfaceImplementations.Where(text => IsContract(text.ContainingType, translations));

        if (property.DeclaredAccessibility is not Accessibility.Public)
            return [];

        return set.AllInterfaces
            .Where(@interface => IsContract(@interface, translations))
            .SelectMany(contract => contract.GetMembers(property.Name).OfType<IPropertySymbol>());
    }

    private static bool IsContract(INamedTypeSymbol @interface, INamedTypeSymbol translations)
    {
        return !SymbolEqualityComparer.Default.Equals(@interface, translations)
               && @interface.AllInterfaces.Contains(translations, SymbolEqualityComparer.Default);
    }

    /// <summary>
    /// The placeholders the contract records for a text, or <see langword="null"/> when it records none.
    /// </summary>
    private static ImmutableArray<int>? ReadPlaceholders(IPropertySymbol text, INamedTypeSymbol attribute)
    {
        foreach (var data in text.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute)
                && data.ConstructorArguments.Length is 1
                && data.ConstructorArguments[0] is { Kind: TypedConstantKind.Array, IsNull: false } placeholders)
            {
                return placeholders.Values.Select(static value => value.Value).OfType<int>().OrderBy(static index => index).ToImmutableArray();
            }
        }

        return null;
    }

    /// <summary>
    /// The property's wording when it is a constant (<c>=> "Hello, {0}!"</c>, a getter returning one, or an initializer),
    /// else <see langword="null"/>.
    /// </summary>
    private static string? GetConstantWording(PropertyDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var expression = declaration.ExpressionBody?.Expression ?? declaration.Initializer?.Value;

        if (expression is null && declaration.AccessorList is not null)
        {
            foreach (var accessor in declaration.AccessorList.Accessors)
            {
                if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                {
                    expression = accessor.ExpressionBody?.Expression
                                 ?? (accessor.Body is { Statements: [ReturnStatementSyntax { Expression: { } returned }] } ? returned : null);
                }
            }
        }

        return expression is not null && semanticModel.GetConstantValue(expression, cancellationToken) is { HasValue: true, Value: string wording }
            ? wording
            : null;
    }
}
