using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// What the code fixes for a set share: finding the class a diagnostic sits on, and changing every part of a partial
/// class, whichever file each part lies in.
/// </summary>
internal static class CodeFixSupport
{
    /// <summary>
    /// The class a diagnostic sits on the name of, or <see langword="null"/> when it sits on none.
    /// </summary>
    public static async Task<TypeDeclarationSyntax?> FindDeclarationAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        return root?.FindToken(context.Span.Start).Parent?.FirstAncestorOrSelf<TypeDeclarationSyntax>();
    }

    /// <summary>
    /// The class a declaration declares, with the semantic model of the document it lies in, or <see langword="null"/>
    /// when the document has none.
    /// </summary>
    public static async Task<(INamedTypeSymbol Type, SemanticModel SemanticModel)?> FindTypeAsync(Document document, TypeDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        return semanticModel?.GetDeclaredSymbol(declaration, cancellationToken) is { } type
            ? (type, semanticModel)
            : null;
    }

    /// <summary>
    /// The declarations of a class, one per part of a partial class.
    /// </summary>
    public static async Task<ImmutableArray<TypeDeclarationSyntax>> GetPartsAsync(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var parts = ImmutableArray.CreateBuilder<TypeDeclarationSyntax>();

        foreach (var reference in type.DeclaringSyntaxReferences)
            if (await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is TypeDeclarationSyntax part)
                parts.Add(part);

        return parts.ToImmutable();
    }

    /// <summary>
    /// Replaces nodes, each with what <paramref name="replace"/> makes of it, in every document they lie in. A node of
    /// a file that is no document of the solution, such as generated code, is left as it is.
    /// </summary>
    public static async Task<Solution> ReplaceNodesAsync<TNode>(Solution solution, IEnumerable<TNode> nodes, Func<TNode, SyntaxNode> replace, CancellationToken cancellationToken)
        where TNode : SyntaxNode
    {
        foreach (var tree in nodes.GroupBy(static node => node.SyntaxTree))
        {
            if (solution.GetDocument(tree.Key) is not { } document)
                continue;

            var root = await tree.Key.GetRootAsync(cancellationToken).ConfigureAwait(false);

            solution = solution.WithDocumentSyntaxRoot(document.Id, root.ReplaceNodes(tree, (original, _) => replace(original)));
        }

        return solution;
    }
}
