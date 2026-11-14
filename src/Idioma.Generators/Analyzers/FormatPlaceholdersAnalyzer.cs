using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds the wording of a text in every language to the placeholders of its default wording (<c>LOC016</c>), so that
/// <c>Format</c> neither throws nor leaves a value out for the users of one language: each argument of
/// <c>LocalizableText.Inline</c> to the default language's, and each text of a set to the placeholders the generated
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
        if (context.Compilation.GetTypeByMetadataName(PackageNames.TranslationSet) is not { } translations)
            return;

        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

        // A package without the attribute records no placeholders to hold a set to.
        if (context.Compilation.GetTypeByMetadataName(PackageNames.FormatPlaceholdersAttribute) is not { } attribute)
            return;

        context.RegisterSyntaxNodeAction(c => AnalyzeProperty(c, translations, attribute), SyntaxKind.PropertyDeclaration);
        context.RegisterSymbolAction(c => AnalyzeInheritedTexts(c, translations, attribute), SymbolKind.NamedType);
    }

    /// <summary>
    /// Holds each language argument of <c>LocalizableText.Inline</c> to the default language's.
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
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
                is not { IsStatic: false, Type.SpecialType: SpecialType.System_String, ContainingType: { TypeKind: TypeKind.Class } set } property
            || !set.AllInterfaces.Contains(translations, SymbolEqualityComparer.Default)
            || ConstantWording.Read(declaration, context.SemanticModel, context.CancellationToken) is not { } wording)
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
    /// Holds the texts a class implements through a property it inherits, such as one of a base class shared by the
    /// sets of several contracts. <see cref="AnalyzeProperty"/> passes such a property by: the class declaring it does
    /// not implement the contract, so there it is no text yet.
    /// </summary>
    /// <remarks>
    /// Asked of the class that brings the contract in, which the classes deriving from it then need not repeat, and
    /// reported there, where the property becomes a text. Only a property of this compilation is read, as the wording
    /// of another assembly's property is not in reach.
    /// </remarks>
    private static void AnalyzeInheritedTexts(SymbolAnalysisContext context, INamedTypeSymbol translations, INamedTypeSymbol attribute)
    {
        var set = (INamedTypeSymbol)context.Symbol;

        if (set is not { TypeKind: TypeKind.Class, BaseType: { SpecialType: not SpecialType.System_Object } baseType }
            || !set.AllInterfaces.Contains(translations, SymbolEqualityComparer.Default)
            || set.Locations.FirstOrDefault(static candidate => candidate.IsInSource) is not { SourceTree: { } tree } location
            || !OneLanguageTextAnalyzer.IsHandWritten(context.IsGeneratedCode, tree))
        {
            return;
        }

        foreach (var contract in set.AllInterfaces)
        {
            // A contract the class it derives from implements already is that class's to answer for.
            if (!IsContract(contract, translations) || baseType.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default))
                continue;

            foreach (var text in contract.GetMembers().OfType<IPropertySymbol>())
            {
                // A property the class declares itself is a text where it stands, which AnalyzeProperty holds.
                if (set.FindImplementationForInterfaceMember(text) is not IPropertySymbol { ContainingType: { TypeKind: TypeKind.Class } declaring } implementation
                    || SymbolEqualityComparer.Default.Equals(declaring, set)
                    || ReadPlaceholders(text, attribute) is not { } expected
                    || ReadWording(implementation, context.Compilation, context.CancellationToken) is not { } wording
                    || FindMismatch(wording, expected) is not { } found)
                {
                    continue;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    PlaceholdersDifferRule,
                    location,
                    $"'{set.Name}.{text.Name}', inherited from '{declaring.Name}',",
                    found,
                    $"the default wording of '{text.ContainingType.Name}.{text.Name}'",
                    FormatPlaceholders.Describe(expected)));
            }
        }
    }

    /// <summary>
    /// The wording of a property of this compilation when it is a constant, else <see langword="null"/>.
    /// </summary>
    // The property lies in the file of another class than the one being analyzed, and an analysis of a class hands out
    // no semantic model for it, so one is asked of the compilation. RS1030 advises against that for the cost of a new
    // model, which is not paid here: while analyzers run, the compilation hands out the model it keeps for a file, the
    // one the actions on that file get too. Only for a text a class inherits, which few do.
    private static string? ReadWording(IPropertySymbol property, Compilation compilation, CancellationToken cancellationToken)
    {
        #pragma warning disable RS1030 // Do not invoke Compilation.GetSemanticModel() within a diagnostic analyzer: no new model is made, see above.
        var wording = property.DeclaringSyntaxReferences is [var reference, ..]
               && reference.GetSyntax(cancellationToken) is PropertyDeclarationSyntax declaration
               && compilation.ContainsSyntaxTree(declaration.SyntaxTree)
            ? ConstantWording.Read(declaration, compilation.GetSemanticModel(declaration.SyntaxTree), cancellationToken)
            : null;
        #pragma warning restore RS1030

        return wording;
    }

    /// <summary>
    /// What a wording holds that the default wording does not, as the message says it, or <see langword="null"/> when
    /// it holds the same placeholders.
    /// </summary>
    private static string? FindMismatch(string wording, ImmutableArray<int> expected)
    {
        if (!FormatPlaceholders.TryRead(wording, out var placeholders))
            return expected.IsEmpty ? null : FormatPlaceholders.InvalidDescription;

        var match = placeholders.SequenceEqual(expected) ? null : FormatPlaceholders.Describe(placeholders);
        return match;
    }

    /// <summary>
    /// The language a parameter of the factory stands for, as the attribute lists it: <c>zh-Hans</c> for
    /// <c>zhHans</c>.
    /// </summary>
    // Read from the attribute when a wording is reported, not before: the factory is the assembly's own, written for
    // the languages it lists.
    private static string GetLanguage(IParameterSymbol parameter, Compilation compilation)
    {
        var language = SupportedLanguages.FindLanguageOfParameter(parameter.Name, SupportedLanguages.ReadCodes(compilation.Assembly));
        return language;
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

        var implementedTexts = set.AllInterfaces
            .Where(@interface => IsContract(@interface, translations))
            .SelectMany(contract => contract.GetMembers(property.Name).OfType<IPropertySymbol>());

        return implementedTexts;
    }

    private static bool IsContract(INamedTypeSymbol @interface, INamedTypeSymbol translations)
    {
        var isContract = !SymbolEqualityComparer.Default.Equals(@interface, translations)
                         && @interface.AllInterfaces.Contains(translations, SymbolEqualityComparer.Default);
        return isContract;
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
}
