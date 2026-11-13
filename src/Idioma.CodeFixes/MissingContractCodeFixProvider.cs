using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a set that lists <c>ITranslations</c> instead of implementing its contract (<c>LOC007</c>): it replaces
/// <c>ITranslations</c> in the base list with the contract generated from the default-language set
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
    private const string TranslationsInterfaceFullName = "CodeChops.Idioma.ITranslations";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [TranslationSetsGenerator.MissingContractDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // The diagnostic sits on the name of the class.
        var declaration = root?.FindToken(context.Span.Start).Parent?.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (declaration is null)
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
    /// Replaces <c>ITranslations</c> with the contract in the base list of whichever part of the class lists it. The
    /// contract derives from <c>ITranslations</c>, so a member implemented explicitly for it stays valid.
    /// </summary>
    private static async Task<Solution> ImplementAsync(Document document, TypeDeclarationSyntax declaration, string contract, CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (semanticModel?.GetDeclaredSymbol(declaration, cancellationToken) is not { } type)
            return solution;

        var listed = new List<TypeSyntax>();
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is not TypeDeclarationSyntax { BaseList: { } baseList } part)
                continue;

            var partModel = semanticModel.Compilation.GetSemanticModel(part.SyntaxTree);

            foreach (var baseType in baseList.Types)
            {
                if (String.Equals(partModel.GetTypeInfo(baseType.Type, cancellationToken).Type?.ToDisplayString(), TranslationsInterfaceFullName, StringComparison.Ordinal))
                    listed.Add(baseType.Type);
            }
        }

        foreach (var tree in listed.GroupBy(static name => name.SyntaxTree))
        {
            if (solution.GetDocument(tree.Key) is not { } partDocument)
                continue;

            var root = await tree.Key.GetRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = root.ReplaceNodes(tree, (original, _) => SyntaxFactory.IdentifierName(contract).WithTriviaFrom(original));

            solution = solution.WithDocumentSyntaxRoot(partDocument.Id, newRoot);
        }

        return solution;
    }
}
