using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a set that lists <c>ILocalizationSet</c> without its contract (<c>LOC205</c>). It adds the contract right after
/// <c>ILocalizationSet</c> (<c>IShopLocalization</c> for <c>ShopLocalizationNl</c>), which then requires every text.
/// </summary>
/// <remarks>
/// The fix is offered only when the build generates that contract from a default-language set.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingContractCodeFixProvider))]
[Shared]
public sealed class MissingContractCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [LocalizationSetsPart.MissingContractDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;

        if (await CodeFixSupport.FindDeclarationAsync(context).ConfigureAwait(false) is not { } declaration
            || await CodeFixSupport.FindTypeAsync(document, declaration, context.CancellationToken).ConfigureAwait(false) is not var (type, semanticModel)
            || await FindPlaceAsync(declaration, type, semanticModel.Compilation, context.CancellationToken).ConfigureAwait(false) is not var (part, index))
        {
            return;
        }

        var solution = document.Project.Solution;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(LocalizationSetsPart.SuggestedNameProperty, out var name) || name is null)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Implement '{name}'",
                    createChangedSolution: cancellationToken => AddAsync(solution, part, index, name, cancellationToken),
                    equivalenceKey: "ImplementContract"),
                diagnostic);
        }
    }

    /// <summary>
    /// Where the contract goes, right after <c>ILocalizationSet</c>, or <see langword="null"/> when no part has a base list.
    /// </summary>
    private static async Task<(TypeDeclarationSyntax Part, int Index)?> FindPlaceAsync(TypeDeclarationSyntax declaration, INamedTypeSymbol type, Compilation compilation,
        CancellationToken cancellationToken)
    {
        var parts = await CodeFixSupport.GetPartsAsync(type, cancellationToken).ConfigureAwait(false);

        foreach (var part in parts)
        {
            if (part.BaseList is not { } baseList)
                continue;

            var partModel = compilation.GetSemanticModel(part.SyntaxTree);

            for (var i = 0; i < baseList.Types.Count; i++)
            {
                if (partModel.GetTypeInfo(baseList.Types[i].Type, cancellationToken).Type is INamedTypeSymbol listed
                    && (PackageNames.IsLocalizationSet(listed) || PackageNames.IsLocalizationSetOfEnum(listed)))
                    return (part, i + 1);
            }
        }

        // A set that inherits ILocalizationSet gets it at the end of this part's base list, else the first part's.
        var last = declaration.BaseList is not null ? declaration : parts.FirstOrDefault(static part => part.BaseList is not null);

        return last?.BaseList is { } own ? (last, own.Types.Count) : null;
    }

    /// <summary>
    /// Adds the contract to one part's base list. A contract generated here may not bind yet.
    /// </summary>
    private static Task<Solution> AddAsync(Solution solution, TypeDeclarationSyntax part, int index, string name, CancellationToken cancellationToken)
    {
        var added = SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(name));
        return CodeFixSupport.ReplaceNodesAsync(solution, [part], original => CodeFixSupport.InsertBaseType(original, index, added), cancellationToken);
    }
}
