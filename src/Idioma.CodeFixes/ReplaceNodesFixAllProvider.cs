using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Applies a fix that replaces nodes, also in other documents than the diagnostic's, to every diagnostic of a
/// document, a project, or the solution at once. Each node changes once, however many diagnostics ask for it.
/// </summary>
// Not the batch fixer: that merges the changes of each diagnostic's fix, and two diagnostics of one text change the
// same nodes.
internal sealed class ReplaceNodesFixAllProvider<TNode>(string title, string equivalenceKey, ReplaceNodesFixAllProvider<TNode>.Fix fix) : FixAllProvider
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
        var replaced = new Dictionary<TNode, TNode>();

        var projects = fixAllContext.Scope is FixAllScope.Solution ? fixAllContext.Solution.Projects : [fixAllContext.Project];

        foreach (var project in projects)
        {
            var diagnostics = fixAllContext is { Scope: FixAllScope.Document, Document: { } scoped }
                ? await fixAllContext.GetDocumentDiagnosticsAsync(scoped).ConfigureAwait(false)
                : await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);

            foreach (var diagnostic in diagnostics)
            {
                if (CodeFixSupport.FindDocument(project, diagnostic) is not { } document
                    || await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is not { } root
                    || await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false) is not { } semanticModel
                    || fix(root, semanticModel, diagnostic, cancellationToken) is not { } nodes)
                {
                    continue;
                }

                // The first fix of a node stands: every diagnostic asks the same of it.
                foreach (var node in nodes)
                    if (!replaced.ContainsKey(node.Key))
                        replaced.Add(node.Key, node.Value);
            }
        }

        return replaced.Count is 0
            ? null
            : CodeAction.Create(title, token => CodeFixSupport.ReplaceNodesAsync(fixAllContext.Solution, replaced.Keys, node => replaced[node], token), equivalenceKey);
    }
}
