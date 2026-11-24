using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Applies a fix that replaces nodes, also in other documents than the diagnostic's, to every diagnostic of a
/// document, a project, or the solution at once. Each node changes once, however many diagnostics ask for it.
/// </summary>
/// <param name="title">The title of the "Fix all".</param>
/// <param name="equivalenceKey">The key of the fix it applies.</param>
/// <param name="fix">The fix of one diagnostic.</param>
/// <param name="prepare">
/// Called for every diagnostic before the first fix is computed, or <see langword="null"/>. A fix that reads the whole
/// project can then read it once for all of them.
/// </param>
/// <param name="findStart">
/// The node the fix of a diagnostic starts from, or <see langword="null"/>. A diagnostic whose node an earlier fix
/// replaced already is skipped then, because its own fix would replace the same nodes.
/// </param>
/// <param name="merge">
/// What a node becomes when two fixes replace it, from the earlier replacement and the later one. Without it, the first
/// replacement stands, because every diagnostic asks the same of the node.
/// </param>
// Not the batch fixer: that merges the changes of each diagnostic's fix, and two diagnostics of one text change the
// same nodes.
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
    /// The nodes the fix of a diagnostic replaces, each with what it becomes, or <see langword="null"/> when the
    /// diagnostic has no fix.
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
