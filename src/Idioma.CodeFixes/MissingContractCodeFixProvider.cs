using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a set that lists <c>ITranslationSet</c> instead of implementing its contract (<c>LOC007</c>): it replaces
/// <c>ITranslationSet</c> in the base list with the contract generated from the default-language set
/// (<c>IShopTranslations</c> for <c>ShopTranslationsNL</c>), which then demands every text.
/// </summary>
/// <remarks>
/// Only when the build generates that contract: without a default-language set to generate it from, there is nothing
/// to implement yet, and the diagnostic gets no fix.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingContractCodeFixProvider))]
[Shared]
public sealed class MissingContractCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [TranslationSetsGenerator.MissingContractDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;

        if (await CodeFixSupport.FindDeclarationAsync(context).ConfigureAwait(false) is not { } declaration)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(TranslationSetsGenerator.SuggestedNameProperty, out var contract) || contract is null)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Implement '{contract}'",
                    createChangedSolution: cancellationToken => ImplementAsync(document, declaration, contract, cancellationToken),
                    equivalenceKey: "ImplementContract"),
                diagnostic);
        }
    }

    /// <summary>
    /// Replaces <c>ITranslationSet</c> with the contract in the base list of whichever part of the class lists it. The
    /// contract derives from <c>ITranslationSet</c>, so a member implemented explicitly for it stays valid.
    /// </summary>
    private static async Task<Solution> ImplementAsync(Document document, TypeDeclarationSyntax declaration, string contract, CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;

        if (await CodeFixSupport.FindTypeAsync(document, declaration, cancellationToken).ConfigureAwait(false) is not var (type, semanticModel))
            return solution;

        var listed = new List<TypeSyntax>();
        foreach (var part in await CodeFixSupport.GetPartsAsync(type, cancellationToken).ConfigureAwait(false))
        {
            if (part.BaseList is not { } baseList)
                continue;

            var partModel = semanticModel.Compilation.GetSemanticModel(part.SyntaxTree);

            foreach (var baseType in baseList.Types)
                if (partModel.GetTypeInfo(baseType.Type, cancellationToken).Type is INamedTypeSymbol listedType && PackageNames.IsTranslationSet(listedType))
                    listed.Add(baseType.Type);
        }

        return await CodeFixSupport.ReplaceNodesAsync(
            solution,
            listed,
            original => SyntaxFactory.IdentifierName(contract).WithTriviaFrom(original),
            cancellationToken).ConfigureAwait(false);
    }
}
