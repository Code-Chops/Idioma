using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a text of a set that has a setter (<c>LOC206</c>). It removes the setter, along with its body:
/// <c>{ get; set; } = "Hallo";</c> becomes <c>{ get; } = "Hallo";</c>.
/// </summary>
/// <remarks>
/// <para>
/// An auto-property can still be assigned in the constructor of its set. Any other assignment stops compiling, which
/// marks the code that changed wording after the set was created.
/// </para>
/// <para>
/// A property whose only accessor is the setter gets no fix, because nothing of it would be left.
/// </para>
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
    /// The code without a setter, and without what belongs to it: its body, and a comment or a blank line above it.
    /// </summary>
    private static SyntaxNode Remove(SyntaxNode root, AccessorDeclarationSyntax setter)
    {
        // A directive around the setter that would lose its other half stays, such as an #if with its #endif further on.
        var withoutSetter = root.RemoveNode(setter, SyntaxRemoveOptions.KeepUnbalancedDirectives) ?? root;
        return withoutSetter;
    }
}
