using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Applies a fix that replaces nodes, possibly in other documents, to every diagnostic in the scope at once. Each node
/// changes once, however many diagnostics ask for it.
/// </summary>
/// <param name="prepare">
/// Called for every diagnostic before the first fix, so a fix that reads the whole project reads it once.
/// </param>
/// <param name="findStart">
/// The node a diagnostic's fix starts from. A diagnostic whose start node was already replaced is skipped.
/// </param>
/// <param name="merge">
/// Combines an earlier and a later replacement of the same node. Without it, the first replacement stands.
/// </param>
// Not the batch fixer, because two diagnostics of one text change the same nodes.
internal sealed class ReplaceNodesFixAllProvider<TNode>(
    string title,
    string equivalenceKey,
    ReplaceNodesFixAllProvider<TNode>.Fix fix,
    Action<SyntaxNode, SemanticModel, Diagnostic>? prepare = null,
    Func<SyntaxNode, Diagnostic, TNode?>? findStart = null,
    Func<TNode, TNode, TNode>? merge = null) : FixAllProvider
    where TNode : SyntaxNode
{
    /// <summary>
    /// The nodes a diagnostic's fix replaces, with their replacements, or <see langword="null"/> when there's no fix.
    /// </summary>
    public delegate Dictionary<TNode, TNode>? Fix(SyntaxNode root, SemanticModel semanticModel, Diagnostic diagnostic, CancellationToken cancellationToken);

    public override IEnumerable<FixAllScope> GetSupportedFixAllScopes() => [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution];

    public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
    {
        var cancellationToken = fixAllContext.CancellationToken;
        var found = new List<(SyntaxNode Root, SemanticModel SemanticModel, Diagnostic Diagnostic)>();

        foreach (var (project, diagnostics) in await CodeFixSupport.GetFixAllDiagnosticsAsync(fixAllContext).ConfigureAwait(false))
        {
            foreach (var diagnostic in diagnostics)
            {
                if (CodeFixSupport.FindDocument(project, diagnostic) is { } document
                    && await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is { } root
                    && await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false) is { } semanticModel)
                {
                    found.Add((root, semanticModel, diagnostic));
                }
            }
        }

        if (prepare is not null)
        {
            foreach (var (root, semanticModel, diagnostic) in found)
                prepare(root, semanticModel, diagnostic);
        }

        var replaced = new Dictionary<TNode, TNode>();

        foreach (var (root, semanticModel, diagnostic) in found)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (findStart?.Invoke(root, diagnostic) is { } start && replaced.ContainsKey(start))
                continue;

            if (fix(root, semanticModel, diagnostic, cancellationToken) is not { } nodes)
                continue;

            foreach (var node in nodes)
            {
                if (!replaced.TryGetValue(node.Key, out var earlier))
                    replaced.Add(node.Key, node.Value);
                else if (merge is not null)
                    replaced[node.Key] = merge(earlier, node.Value);
            }
        }

        return replaced.Count is 0
            ? null
            : CodeAction.Create(title, token => CodeFixSupport.ReplaceNodesAsync(fixAllContext.Solution, replaced.Keys, node => replaced[node], token), equivalenceKey);
    }
}
