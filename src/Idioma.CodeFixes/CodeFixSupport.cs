using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Formatting;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Helpers the code fixes share, such as changing every part of a partial class, whichever file it's in.
/// </summary>
internal static class CodeFixSupport
{
    /// <summary>
    /// The class a diagnostic sits on, or <see langword="null"/>.
    /// </summary>
    public static async Task<TypeDeclarationSyntax?> FindDeclarationAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        return root?.FindToken(context.Span.Start).Parent?.FirstAncestorOrSelf<TypeDeclarationSyntax>();
    }

    /// <summary>
    /// The document a diagnostic is in, found by its tree, or by its path for a diagnostic of an earlier compilation.
    /// </summary>
    public static Document? FindDocument(Project project, Diagnostic diagnostic)
    {
        if (diagnostic.Location.SourceTree is not { } tree)
            return null;

        return project.GetDocument(tree)
               ?? project.Documents.FirstOrDefault(document => document.FilePath is not null && String.Equals(document.FilePath, tree.FilePath, StringComparison.Ordinal));
    }

    /// <summary>
    /// The diagnostics a "Fix all" covers, grouped by project. A project without any is left out.
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
    /// The top-level classes of a namespace and its nested namespaces.
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
    /// The class a declaration declares, with its document's semantic model.
    /// </summary>
    public static async Task<(INamedTypeSymbol Type, SemanticModel SemanticModel)?> FindTypeAsync(Document document, TypeDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        return semanticModel?.GetDeclaredSymbol(declaration, cancellationToken) is { } type
            ? (type, semanticModel)
            : null;
    }

    /// <summary>
    /// The declarations of a class, one per partial part.
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
    /// The declarations of a class, one per partial part, for a fix that can't wait, such as one a "Fix all" provider
    /// calls (<see cref="GetPartsAsync"/>).
    /// </summary>
    public static ImmutableArray<TypeDeclarationSyntax> GetParts(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var parts = type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<TypeDeclarationSyntax>()
            .ToImmutableArray();

        return parts;
    }

    /// <summary>
    /// Whether a simple name, written at a position, binds to a type that <paramref name="isMeant"/> accepts.
    /// </summary>
    public static bool BindsAt(SemanticModel semanticModel, int position, string name, Func<INamedTypeSymbol, bool> isMeant)
    {
        var binds = semanticModel.GetSpeculativeTypeInfo(position, SyntaxFactory.IdentifierName(name), SpeculativeBindingOption.BindAsTypeOrNamespace).Type is INamedTypeSymbol bound
                    && isMeant(bound);

        return binds;
    }

    /// <summary>
    /// A type as code at a position names it: by its simple name where that binds to it, and else by its full name.
    /// </summary>
    public static string NameAt(SemanticModel semanticModel, int position, INamedTypeSymbol type)
    {
        var name = BindsAt(semanticModel, position, type.Name, bound => SymbolEqualityComparer.Default.Equals(bound, type))
            ? type.Name
            : type.ToDisplayString();

        return name;
    }

    /// <summary>
    /// A declaration with one modifier in place of another, which keeps its trivia, or as it is without that modifier.
    /// </summary>
    public static TDeclaration ReplaceModifier<TDeclaration>(TDeclaration declaration, SyntaxKind replaced, SyntaxKind replacement)
        where TDeclaration : MemberDeclarationSyntax
    {
        var index = declaration.Modifiers.IndexOf(replaced);

        if (index < 0)
            return declaration;

        var old = declaration.Modifiers[index];
        var result = (TDeclaration)declaration.WithModifiers(declaration.Modifiers.Replace(old, SyntaxFactory.Token(old.LeadingTrivia, replacement, old.TrailingTrivia)));

        return result;
    }

    /// <summary>
    /// A declaration without a modifier (<see cref="RemoveModifiers"/>).
    /// </summary>
    public static TDeclaration RemoveModifier<TDeclaration>(TDeclaration declaration, SyntaxKind modifier)
        where TDeclaration : MemberDeclarationSyntax
    {
        return RemoveModifiers(declaration, token => token.IsKind(modifier));
    }

    /// <summary>
    /// A declaration without the modifiers that match. The leading trivia of a removed modifier, such as the
    /// indentation, moves to the token after it: the next modifier, or else the token after the modifiers, such as the
    /// keyword of a class or the type of a property.
    /// </summary>
    public static TDeclaration RemoveModifiers<TDeclaration>(TDeclaration declaration, Func<SyntaxToken, bool> remove)
        where TDeclaration : MemberDeclarationSyntax
    {
        var kept = new List<SyntaxToken>();
        var carried = SyntaxTriviaList.Empty;

        foreach (var modifier in declaration.Modifiers)
        {
            if (remove(modifier))
            {
                carried = carried.AddRange(modifier.LeadingTrivia);
                continue;
            }

            kept.Add(carried.Count > 0 ? modifier.WithLeadingTrivia(carried.AddRange(modifier.LeadingTrivia)) : modifier);
            carried = SyntaxTriviaList.Empty;
        }

        if (kept.Count == declaration.Modifiers.Count)
            return declaration;

        var result = (TDeclaration)declaration.WithModifiers(SyntaxFactory.TokenList(kept));

        if (carried.Count > 0)
        {
            var next = FindTokenAfterModifiers(result);
            result = result.ReplaceToken(next, next.WithLeadingTrivia(carried.AddRange(next.LeadingTrivia)));
        }

        return result;
    }

    /// <summary>
    /// A declaration with a modifier after its last accessibility modifier, or first. A first modifier takes over the
    /// indentation of what came first.
    /// </summary>
    public static TDeclaration InsertModifier<TDeclaration>(TDeclaration declaration, SyntaxKind modifier)
        where TDeclaration : MemberDeclarationSyntax
    {
        var modifiers = declaration.Modifiers;
        var index = 0;

        for (var i = 0; i < modifiers.Count; i++)
        {
            if (SyntaxFacts.IsAccessibilityModifier(modifiers[i].Kind()))
                index = i + 1;
        }

        var keyword = SyntaxFactory.Token(modifier).WithTrailingTrivia(SyntaxFactory.Space);

        if (index is 0 && modifiers.Count > 0)
        {
            keyword = keyword.WithLeadingTrivia(modifiers[0].LeadingTrivia);
            modifiers = modifiers.Replace(modifiers[0], modifiers[0].WithLeadingTrivia());
        }
        else if (modifiers.Count is 0)
        {
            var next = FindTokenAfterModifiers(declaration);
            keyword = keyword.WithLeadingTrivia(next.LeadingTrivia);
            declaration = declaration.ReplaceToken(next, next.WithLeadingTrivia());
        }

        var result = (TDeclaration)declaration.WithModifiers(modifiers.Insert(index, keyword));
        return result;
    }

    /// <summary>
    /// The token a declaration's modifiers stand before: the keyword of a class, or the type of a property or method.
    /// </summary>
    private static SyntaxToken FindTokenAfterModifiers(MemberDeclarationSyntax declaration)
    {
        var token = declaration.Modifiers.Count > 0
            ? declaration.Modifiers[^1].GetNextToken()
            : declaration.AttributeLists.Count > 0
                ? declaration.AttributeLists[^1].GetLastToken().GetNextToken()
                : declaration.GetFirstToken();

        return token;
    }

    /// <summary>
    /// Inserts an interface into the base list at an index. The new entry takes over the trivia of the entry it
    /// displaces, such as the line break before the body.
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
    /// Removes the base list and moves its trailing trivia to the token before it, so <c>class X : Y</c> becomes
    /// <c>class X</c>.
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
    /// Replaces nodes in every document they're in. A node outside the solution's documents, such as generated code,
    /// stays as it is.
    /// </summary>
    /// <remarks>
    /// A node inside another replaced node is written into that node's replacement (<see cref="Nest"/>). A replacement
    /// marked <see cref="FormatAnnotation"/> is formatted with its file's line endings.
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

            var replaced = root.ReplaceNodes(replacements.Keys, (original, _) => replacements[original]);
            solution = solution.WithDocumentSyntaxRoot(document.Id, replaced);

            if (replaced.HasAnnotation(FormatAnnotation) || replaced.GetAnnotatedNodes(FormatAnnotation).Any())
                solution = await FormatAsync(solution.GetDocument(document.Id)!, FindEndOfLine(root), cancellationToken).ConfigureAwait(false);
        }

        return solution;
    }

    /// <summary>
    /// Marks a replacement to be formatted with its file's line endings (<see cref="ReplaceNodesAsync"/>). Use it in
    /// place of <see cref="Formatter.Annotation"/>, which takes the line endings of .editorconfig or the machine.
    /// </summary>
    public static SyntaxAnnotation FormatAnnotation { get; } = new(nameof(FormatAnnotation));

    private static async Task<Solution> FormatAsync(Document document, SyntaxTrivia endOfLine, CancellationToken cancellationToken)
    {
        var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
        var withEndOfLine = options.WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, endOfLine.ToString());

        var formatted = await Formatter.FormatAsync(document, FormatAnnotation, withEndOfLine, cancellationToken).ConfigureAwait(false);
        return formatted.Project.Solution;
    }

    /// <summary>
    /// Writes each replaced node that lies inside another replaced node into that node's replacement, and drops it.
    /// </summary>
    // ReplaceNodes would replace the outer node with a replacement made from its original, losing the inner change.
    // The inner node is found in the outer replacement by its child path.
    private static Dictionary<SyntaxNode, SyntaxNode> Nest(Dictionary<SyntaxNode, SyntaxNode> replacements)
    {
        // Smallest first, so a node holds its own nested changes before it's written into the one around it.
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

        // A node the replacement changed keeps that change.
        return found.IsEquivalentTo(inner) ? found : null;
    }

    /// <summary>
    /// The file's first line break, or the machine's when it has none.
    /// </summary>
    public static SyntaxTrivia FindEndOfLine(SyntaxNode root)
    {
        foreach (var trivia in root.DescendantTrivia())
            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                return trivia;

        return SyntaxFactory.EndOfLine(Environment.NewLine);
    }
}
