using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Rename;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a default-language set the generator cannot extend (<c>LOC002</c>) where one edit does: it makes the class
/// partial, names it after its language (<c>ShopTranslations</c> becomes <c>ShopTranslationsEN</c>, everywhere it is
/// used), or removes the <see langword="abstract"/> or <see langword="file"/> modifier.
/// </summary>
/// <remarks>
/// A nested or generic set gets no fix: moving a class out of its container, or closing its type parameters, is a
/// decision about the surrounding code. Nor does an abstract set with abstract members, which would no longer compile,
/// or a set without a parameterless constructor, as only its author knows what the constructor can do without.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CannotExtendCodeFixProvider))]
[Shared]
public sealed class CannotExtendCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [TranslationSetsGenerator.CannotExtendDiagnosticId];

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
            if (CreateFix(document, declaration, diagnostic) is { } fix)
                context.RegisterCodeFix(fix, diagnostic);
    }

    /// <summary>
    /// The fix for what the generator reported, or <see langword="null"/> when no single edit resolves it. The shapes
    /// are told apart in the order the generator reports them, so the fix is for the problem the message names.
    /// </summary>
    private static CodeAction? CreateFix(Document document, TypeDeclarationSyntax declaration, Diagnostic diagnostic)
    {
        if (diagnostic.Properties.TryGetValue(TranslationSetsGenerator.SuggestedNameProperty, out var suggestedName) && suggestedName is not null)
        {
            return CodeAction.Create(
                title: $"Rename to '{suggestedName}'",
                createChangedSolution: cancellationToken => RenameAsync(document, declaration, suggestedName, cancellationToken),
                equivalenceKey: "Rename");
        }

        if (declaration.Parent is TypeDeclarationSyntax)
            return null;

        if (declaration.Modifiers.Any(SyntaxKind.FileKeyword))
            return RemoveModifier(document, declaration, SyntaxKind.FileKeyword);

        if (declaration.TypeParameterList is not null)
            return null;

        if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
            return RemoveModifier(document, declaration, SyntaxKind.AbstractKeyword);

        if (declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            return null;

        return CodeAction.Create(
            title: $"Make '{declaration.Identifier.ValueText}' partial",
            createChangedDocument: cancellationToken => ReplaceAsync(document, declaration, AddPartial(declaration), cancellationToken),
            equivalenceKey: "MakePartial");
    }

    private static CodeAction RemoveModifier(Document document, TypeDeclarationSyntax declaration, SyntaxKind modifier)
    {
        var keyword = SyntaxFacts.GetText(modifier);

        return CodeAction.Create(
            title: $"Remove '{keyword}' from '{declaration.Identifier.ValueText}'",
            createChangedSolution: cancellationToken => RemoveModifierAsync(document, declaration, modifier, cancellationToken),
            equivalenceKey: $"Remove.{keyword}");
    }

    private static async Task<Solution> RenameAsync(Document document, TypeDeclarationSyntax declaration, string newName, CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        return semanticModel?.GetDeclaredSymbol(declaration, cancellationToken) is { } type
            ? await Renamer.RenameSymbolAsync(solution, type, new SymbolRenameOptions(), newName, cancellationToken).ConfigureAwait(false)
            : solution;
    }

    /// <summary>
    /// Removes a modifier from every part of the class that carries it: each part of a partial class may repeat it.
    /// An abstract class stays as it is when a member of it is abstract, which a concrete class cannot hold.
    /// </summary>
    private static async Task<Solution> RemoveModifierAsync(Document document, TypeDeclarationSyntax declaration, SyntaxKind modifier, CancellationToken cancellationToken)
    {
        var solution = document.Project.Solution;
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (semanticModel?.GetDeclaredSymbol(declaration, cancellationToken) is not { } type
            || modifier is SyntaxKind.AbstractKeyword && type.GetMembers().Any(static member => member.IsAbstract))
        {
            return solution;
        }

        var parts = new List<TypeDeclarationSyntax>();
        foreach (var reference in type.DeclaringSyntaxReferences)
            if (await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is TypeDeclarationSyntax part && part.Modifiers.Any(modifier))
                parts.Add(part);

        foreach (var tree in parts.GroupBy(static part => part.SyntaxTree))
        {
            if (solution.GetDocument(tree.Key) is not { } partDocument)
                continue;

            var root = await tree.Key.GetRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = root.ReplaceNodes(tree, (original, _) => WithoutModifier(original, modifier));

            solution = solution.WithDocumentSyntaxRoot(partDocument.Id, newRoot);
        }

        return solution;
    }

    private static async Task<Document> ReplaceAsync(Document document, TypeDeclarationSyntax declaration, TypeDeclarationSyntax replacement, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        return root is null
            ? document
            : document.WithSyntaxRoot(root.ReplaceNode(declaration, replacement));
    }

    /// <summary>
    /// Adds <see langword="partial"/> as the last modifier, where the language wants it: right before
    /// <see langword="class"/>.
    /// </summary>
    private static TypeDeclarationSyntax AddPartial(TypeDeclarationSyntax declaration)
    {
        var partial = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);

        if (declaration.Modifiers.Count > 0)
            return declaration.WithModifiers(declaration.Modifiers.Add(partial));

        // Without modifiers the comments and indentation of the class sit on its keyword, and move to the modifier.
        var keyword = declaration.Keyword;

        return declaration
            .WithKeyword(keyword.WithLeadingTrivia())
            .WithModifiers(SyntaxFactory.TokenList(partial.WithLeadingTrivia(keyword.LeadingTrivia)));
    }

    /// <summary>
    /// Removes a modifier, and hands what stood before it, such as a comment or the indentation, to the token that
    /// takes its place.
    /// </summary>
    private static TypeDeclarationSyntax WithoutModifier(TypeDeclarationSyntax declaration, SyntaxKind modifier)
    {
        var index = declaration.Modifiers.IndexOf(modifier);
        var removed = declaration.Modifiers[index];
        var modifiers = declaration.Modifiers.RemoveAt(index);

        if (index < modifiers.Count)
            return declaration.WithModifiers(modifiers.Replace(modifiers[index], modifiers[index].WithLeadingTrivia(removed.LeadingTrivia)));

        return declaration
            .WithModifiers(modifiers)
            .WithKeyword(declaration.Keyword.WithLeadingTrivia(removed.LeadingTrivia));
    }
}
