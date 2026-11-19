using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a call of <c>LocalizableText.Inline</c> with an argument that has no language name (<c>LOC501</c>). It names
/// every such argument after the language it stands for: <c>LocalizableText.Inline("Orders", "Bestellingen")</c>
/// becomes <c>LocalizableText.Inline(en: "Orders", nl: "Bestellingen")</c>.
/// </summary>
/// <remarks>
/// The diagnostic carries the language of each argument (<see cref="OneLanguageTextAnalyzer.ParameterNamesProperty"/>),
/// so the fix names them as they are, and never swaps two.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnnamedLanguageCodeFixProvider))]
[Shared]
public sealed class UnnamedLanguageCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [OneLanguageTextAnalyzer.UnnamedLanguageDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // The diagnostic sits on the first argument without a name.
        if (root?.FindNode(context.Span, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArgumentSyntax>()?.Parent is not ArgumentListSyntax arguments)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(OneLanguageTextAnalyzer.ParameterNamesProperty, out var value) || value is null)
                continue;

            var names = value.Split(',');

            // The call changed since the diagnostic was reported.
            if (names.Length != arguments.Arguments.Count)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Name each language",
                    createChangedDocument: _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(arguments, Name(arguments, names)))),
                    equivalenceKey: "NameLanguages"),
                diagnostic);
        }
    }

    /// <summary>
    /// The arguments with a name before each one that has none: the language it stands for.
    /// </summary>
    private static ArgumentListSyntax Name(ArgumentListSyntax arguments, string[] names)
    {
        var named = arguments.Arguments.Select((argument, i) => argument.NameColon is null ? Name(argument, names[i]) : argument);

        var result = arguments.WithArguments(SyntaxFactory.SeparatedList(named, arguments.Arguments.GetSeparators()));
        return result;
    }

    /// <summary>
    /// An argument with a name in front. The name takes over what stood before the argument, such as a line break and
    /// its indentation.
    /// </summary>
    private static ArgumentSyntax Name(ArgumentSyntax argument, string name)
    {
        var identifier = SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None ? name : $"@{name}";

        var nameColon = SyntaxFactory.NameColon(
                SyntaxFactory.IdentifierName(identifier),
                SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space))
            .WithLeadingTrivia(argument.GetLeadingTrivia());

        var namedArgument = argument.WithoutLeadingTrivia().WithNameColon(nameColon);
        return namedArgument;
    }
}
