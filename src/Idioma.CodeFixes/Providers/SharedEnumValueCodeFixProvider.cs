using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a call that gets an enum member's text by a value an earlier member shares (<c>LOC600</c>):
/// <c>OrderStatus.None.GetText()</c> becomes <c>OrderStatusLocalization.None</c>.
/// </summary>
/// <remarks>
/// Only a member of a set of the enum gets the fix. A <c>[Localized]</c> member's text would come from
/// <c>LocalizedText.GetTextByMember</c> as a <c>string?</c>, which could break code that takes a <c>string</c>.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SharedEnumValueCodeFixProvider))]
[Shared]
public sealed class SharedEnumValueCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [SharedEnumValueAnalyzer.SharedValueDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            // The diagnostic sits on the member, and its first additional location is the whole call.
            if (!diagnostic.Properties.TryGetValue(SharedEnumValueAnalyzer.OwnTextProperty, out var ownText)
                || ownText is null
                || diagnostic.AdditionalLocations is not [var call, ..]
                || root.FindNode(call.SourceSpan, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            var replacement = SyntaxFactory.ParseExpression(ownText).WithTriviaFrom(invocation);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Use {ownText}",
                    createChangedDocument: _ => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement))),
                    equivalenceKey: "UseOwnTextOfMember"),
                diagnostic);
        }
    }
}
