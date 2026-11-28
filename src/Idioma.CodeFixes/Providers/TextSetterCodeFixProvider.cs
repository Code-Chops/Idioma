using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a text of a set that has a setter (<c>LOC301</c>). It removes the setter, along with its body:
/// <c>{ get; set; } = "Hallo";</c> becomes <c>{ get; } = "Hallo";</c>.
/// </summary>
/// <remarks>
/// Any assignment outside the set's constructor then stops compiling. A property with only a setter gets no fix.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TextSetterCodeFixProvider))]
[Shared]
public sealed class TextSetterCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [TextSetterAnalyzer.TextSetterDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // The diagnostic sits on the 'set' keyword.
        if (root?.FindToken(context.Span.Start).Parent is not AccessorDeclarationSyntax { Parent: AccessorListSyntax { Accessors.Count: > 1 } } setter
            || !setter.IsKind(SyntaxKind.SetAccessorDeclaration))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Remove the setter",
                createChangedDocument: _ => Task.FromResult(document.WithSyntaxRoot(Remove(root, setter))),
                equivalenceKey: "RemoveSetter"),
            context.Diagnostics);
    }

    /// <summary>
    /// Removes the setter with its body and the trivia above it.
    /// </summary>
    private static SyntaxNode Remove(SyntaxNode root, AccessorDeclarationSyntax setter)
    {
        // Keeps a directive whose other half lies outside the setter, such as an #if.
        var withoutSetter = root.RemoveNode(setter, SyntaxRemoveOptions.KeepUnbalancedDirectives) ?? root;
        return withoutSetter;
    }
}
