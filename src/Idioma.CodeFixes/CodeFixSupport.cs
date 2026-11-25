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
    /// The document a diagnostic is in: by its tree, and else by its path, for a diagnostic of an earlier compilation.
    /// </summary>
    public static Document? FindDocument(Project project, Diagnostic diagnostic)
    {
        if (diagnostic.Location.SourceTree is not { } tree)
            return null;

        return project.GetDocument(tree)
               ?? project.Documents.FirstOrDefault(document => document.FilePath is not null && String.Equals(document.FilePath, tree.FilePath, StringComparison.Ordinal));
    }

    /// <summary>
    /// The diagnostics a "Fix all" covers, with the project of each: those of the document, of the project, or of every
    /// project of the solution. A project without such a diagnostic is left out.
    /// </summary>
    public static async Task<List<(Project Project, ImmutableArray<Diagnostic> Diagnostics)>> GetFixAllDiagnosticsAsync(FixAllContext fixAllContext)
    {
        var found = new List<(Project Project, ImmutableArray<Diagnostic> Diagnostics)>();
        var projects = fixAllContext.Scope is FixAllScope.Solution ? fixAllContext.Solution.Projects : [fixAllContext.Project];

        foreach (var project in projects)
        {
            var diagnostics = fixAllContext is { Scope: FixAllScope.Document, Document: { } scoped }
                ? await fixAllContext.GetDocumentDiagnosticsAsync(scoped).ConfigureAwait(false)
                : await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);

            if (!diagnostics.IsEmpty)
                found.Add((project, diagnostics));
        }

        return found;
    }

    /// <summary>
    /// The top-level classes of a namespace and of the namespaces inside it. A set is a top-level class.
    /// </summary>
    public static IEnumerable<INamedTypeSymbol> GetClasses(INamespaceSymbol @namespace)
    {
        foreach (var type in @namespace.GetTypeMembers())
            if (type.TypeKind is TypeKind.Class)
                yield return type;

        foreach (var inner in @namespace.GetNamespaceMembers())
            foreach (var type in GetClasses(inner))
                yield return type;
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
    /// The declaration with an interface inserted into its base list at an index. The new entry takes over what stood
    /// around the entry it displaces, such as the space or line break before the class's body.
    /// </summary>
    public static TypeDeclarationSyntax InsertBaseType(TypeDeclarationSyntax declaration, int index, BaseTypeSyntax added)
    {
        var baseList = declaration.BaseList!;
        var types = baseList.Types;
        var comma = SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space);

        // A list of entries and commas, in turn: entry i stands at 2i.
        var items = index == types.Count
            ? types.Replace(types[^1], types[^1].WithoutTrailingTrivia()).GetWithSeparators()
                .Add(comma)
                .Add(added.WithTrailingTrivia(types[^1].GetTrailingTrivia()))
            : types.Replace(types[index], types[index].WithoutLeadingTrivia()).GetWithSeparators()
                .InsertRange(2 * index, [added.WithLeadingTrivia(types[index].GetLeadingTrivia()), comma]);

        return declaration.WithBaseList(baseList.WithTypes(SyntaxFactory.SeparatedList<BaseTypeSyntax>(items)));
    }

    /// <summary>
    /// The declaration without its base list. What stood after the base list, such as the line break before the body,
    /// goes to the token before it, in place of the space that stood between the two: <c>class X : Y</c> becomes
    /// <c>class X</c>, and not <c>class X :</c>.
    /// </summary>
    public static TypeDeclarationSyntax RemoveBaseList(TypeDeclarationSyntax declaration)
    {
        var baseList = declaration.BaseList!;
        var before = baseList.GetFirstToken().GetPreviousToken();
        var kept = before.TrailingTrivia.Reverse().SkipWhile(static trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)).Reverse();

        var result = declaration
            .ReplaceToken(before, before.WithTrailingTrivia(kept.Concat(baseList.GetTrailingTrivia())))
            .WithBaseList(null);

        return result;
    }

    /// <summary>
    /// Replaces nodes, each with what <paramref name="replace"/> makes of it, in every document they lie in. A node of
    /// a file that is no document of the solution, such as generated code, is left as it is.
    /// </summary>
    /// <remarks>
    /// A node may lie inside another node that is replaced too, as a named argument does inside a method that a
    /// "Fix all" renames the parameters of. Its replacement is then written into that of the node around it
    /// (<see cref="Nest"/>).
    /// </remarks>
    public static async Task<Solution> ReplaceNodesAsync<TNode>(Solution solution, IEnumerable<TNode> nodes, Func<TNode, SyntaxNode> replace, CancellationToken cancellationToken)
        where TNode : SyntaxNode
    {
        foreach (var tree in nodes.GroupBy(static node => node.SyntaxTree))
        {
            if (solution.GetDocument(tree.Key) is not { } document)
                continue;

            var root = await tree.Key.GetRootAsync(cancellationToken).ConfigureAwait(false);
            var replacements = Nest(tree.Distinct().ToDictionary(static SyntaxNode (node) => node, node => replace(node)));

            solution = solution.WithDocumentSyntaxRoot(document.Id, root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]));
        }

        return solution;
    }

    /// <summary>
    /// The replacements of the nodes of one file, with each node that lies inside another one written into the
    /// replacement of that other node, and left out itself.
    /// </summary>
    // ReplaceNodes replaces the inner node first and then the outer one with what it was given, which was made from the
    // outer node as it was. So the change of the inner node would be lost. The inner node is found in the outer node's
    // replacement by the way down to it, child by child. A replacement that has another node there, or that changed
    // the node itself, keeps what it has.
    private static Dictionary<SyntaxNode, SyntaxNode> Nest(Dictionary<SyntaxNode, SyntaxNode> replacements)
    {
        // The smallest first, so that a node is written into the one around it after the nodes inside it were written
        // into it.
        foreach (var inner in replacements.Keys.OrderBy(static node => node.Span.Length).ToArray())
        {
            var outer = inner.Ancestors().FirstOrDefault(replacements.ContainsKey);

            if (outer is null)
                continue;

            if (FindIn(replacements[outer], outer, inner) is { } found)
                replacements[outer] = replacements[outer].ReplaceNode(found, replacements[inner]);

            replacements.Remove(inner);
        }

        return replacements;
    }

    /// <summary>
    /// The node of a replacement that stands where <paramref name="inner"/> stands in <paramref name="outer"/>, or
    /// <see langword="null"/> when the replacement has another node there.
    /// </summary>
    private static SyntaxNode? FindIn(SyntaxNode replacement, SyntaxNode outer, SyntaxNode inner)
    {
        var way = new Stack<int>();

        for (var current = inner; current != outer; current = current.Parent!)
        {
            var index = 0;

            foreach (var child in current.Parent!.ChildNodesAndTokens())
            {
                if (child.AsNode() == current)
                    break;

                index++;
            }

            way.Push(index);
        }

        var found = replacement;

        while (way.Count > 0)
        {
            var children = found.ChildNodesAndTokens();
            var index = way.Pop();

            if (index >= children.Count || children[index].AsNode() is not { } child)
                return null;

            found = child;
        }

        // Only a node the replacement left as it was: one it changed holds a change that writing over it would lose.
        return found.IsEquivalentTo(inner) ? found : null;
    }

    /// <summary>
    /// The line break a file uses, for a line a fix adds to it: its first one, and else the one of this machine.
    /// </summary>
    public static SyntaxTrivia FindEndOfLine(SyntaxNode root)
    {
        foreach (var trivia in root.DescendantTrivia())
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                return trivia;

        return SyntaxFactory.EndOfLine(Environment.NewLine);
    }
}
