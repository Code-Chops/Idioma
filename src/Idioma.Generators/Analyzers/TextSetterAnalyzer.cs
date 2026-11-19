using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reports a text of a set that has a setter (<c>LOC206</c>), whatever the accessibility of the setter.
/// </summary>
/// <remarks>
/// <para>
/// One instance of each set serves every user. A setter lets code change the wording after the set is created, and
/// every user then sees the new wording. An <see langword="init"/> accessor is no setter, because it can only be used
/// while the set is created.
/// </para>
/// <para>
/// Only a property that implements a text of a contract is reported (<see cref="ContractTexts.ImplementsText"/>), so
/// a helper of a set may have a setter. A getter that returns a field which a method changes isn't caught. A getter
/// that reads from a store stays the way to change wording at run time.
/// </para>
/// <para>
/// The IDE fix removes the setter (<c>TextSetterCodeFixProvider</c> in the code fixes).
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TextSetterAnalyzer : DiagnosticAnalyzer
{
    public const string TextSetterDiagnosticId = "LOC206";

    private static readonly DiagnosticDescriptor TextSetterRule = new(
        id: TextSetterDiagnosticId,
        title: "Text with a setter",
        messageFormat: "'{0}.{1}' has a setter, so its wording can change after the set is created. Remove the setter. To set the wording when the set is created, use the constructor or 'init'. To change it at run time, read it in the getter.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "One instance of each set serves every user. A text with a setter can get other wording after the set is created, and every user then sees it. Remove the setter. To set the wording when the set is created, assign it in the constructor or use an 'init' accessor. To change wording at run time, read it in the getter, for example from a store.",
        helpLinkUri: PackageNames.HelpLink(TextSetterDiagnosticId));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [TextSetterRule];

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

        // On the setter, and not on the property: most texts have no setter, and are then never bound here.
        context.RegisterSyntaxNodeAction(c => AnalyzeSetter(c, localizationSet), SyntaxKind.SetAccessorDeclaration);
    }

    private static void AnalyzeSetter(SyntaxNodeAnalysisContext context, INamedTypeSymbol localizationSet)
    {
        var setter = (AccessorDeclarationSyntax)context.Node;

        // A property only: an indexer has a setter too, and is no text.
        if (setter.Parent?.Parent is not PropertyDeclarationSyntax declaration
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { IsStatic: false, ContainingType: { TypeKind: TypeKind.Class } set } property
            || !set.AllInterfaces.Contains(localizationSet, SymbolEqualityComparer.Default)
            || !ContractTexts.ImplementsText(property, set, localizationSet))
        {
            return;
        }

        // The name of the text, without the contract an explicit implementation carries in front of it.
        var text = property.Name.Substring(property.Name.LastIndexOf('.') + 1);

        context.ReportDiagnostic(Diagnostic.Create(TextSetterRule, setter.Keyword.GetLocation(), set.Name, text));
    }
}
