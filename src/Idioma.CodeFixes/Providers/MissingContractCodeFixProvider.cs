using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a set that lists <c>ILocalizationSet</c> without its contract (<c>LOC204</c>). It adds the contract right after
/// <c>ILocalizationSet</c> (<c>IShopLocalization</c> for <c>ShopLocalizationNl</c>), which then requires every text.
/// </summary>
/// <remarks>
/// A set without its contract gets the fix only when the build generates that contract. Without a default-language set
/// to generate it from, there is nothing to implement yet.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingContractCodeFixProvider))]
[Shared]
public sealed class MissingContractCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [LocalizationSetsGenerator.MissingContractDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;

        if (await CodeFixSupport.FindDeclarationAsync(context).ConfigureAwait(false) is not { } declaration)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(LocalizationSetsGenerator.SuggestedNameProperty, out var name) || name is null)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Implement '{name}'",
                    createChangedSolution: cancellationToken => AddAsync(document, declaration, name, cancellationToken),
                    equivalenceKey: "ImplementContract"),
                diagnostic);
        }
    }

    /// <summary>
    /// Adds the contract right after <c>ILocalizationSet</c>, in whichever part of the class lists it. The contract is one
    /// that binds, or one generated here, which binds to nothing yet.
    /// </summary>
    private static async Task<Solution> AddAsync(Document document, TypeDeclarationSyntax declaration, string name, CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;

        if (await CodeFixSupport.FindTypeAsync(document, declaration, cancellationToken).ConfigureAwait(false) is not var (type, semanticModel))
            return solution;

        foreach (var part in await CodeFixSupport.GetPartsAsync(type, cancellationToken).ConfigureAwait(false))
        {
            if (part.BaseList is not { } baseList)
                continue;

            var partModel = semanticModel.Compilation.GetSemanticModel(part.SyntaxTree);

            for (var i = 0; i < baseList.Types.Count; i++)
            {
                if (partModel.GetTypeInfo(baseList.Types[i].Type, cancellationToken).Type is INamedTypeSymbol listed
                    && (PackageNames.IsLocalizationSet(listed) || PackageNames.IsLocalizationSetOfEnum(listed)))
                    return await AddAsync(solution, part, i + 1, name, cancellationToken).ConfigureAwait(false);
            }
        }

        // Nothing to go next to, as for a set that gets ILocalizationSet through a class it derives from: at the end.
        return declaration.BaseList is { } own
            ? await AddAsync(solution, declaration, own.Types.Count, name, cancellationToken).ConfigureAwait(false)
            : solution;
    }

    /// <summary>
    /// Adds the contract to the base list of one part at an index.
    /// </summary>
    private static Task<Solution> AddAsync(Solution solution, TypeDeclarationSyntax part, int index, string name, CancellationToken cancellationToken)
    {
        var added = SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(name));
        return CodeFixSupport.ReplaceNodesAsync(solution, [part], original => CodeFixSupport.InsertBaseType(original, index, added), cancellationToken);
    }
}
