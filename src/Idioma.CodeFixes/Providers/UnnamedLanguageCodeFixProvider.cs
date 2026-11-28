using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a standalone text with an argument that has no language name (<c>LOC500</c>). It names each such argument:
/// <c>LocalizedText.Inline("Orders", "Bestellingen")</c> becomes
/// <c>LocalizedText.Inline(en: "Orders", nl: "Bestellingen")</c>.
/// </summary>
/// <remarks>
/// The diagnostic carries each argument's language (<see cref="UnnamedLanguageAnalyzer.ParameterNamesProperty"/>).
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnnamedLanguageCodeFixProvider))]
[Shared]
public sealed class UnnamedLanguageCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [UnnamedLanguageAnalyzer.UnnamedLanguageDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // The diagnostic sits on the first unnamed argument of a call or attribute.
        var node = root?.FindNode(context.Span, getInnermostNodeForTie: true);

        SyntaxNode? arguments = node?.FirstAncestorOrSelf<ArgumentSyntax>()?.Parent as ArgumentListSyntax;
        arguments ??= node?.FirstAncestorOrSelf<AttributeArgumentSyntax>()?.Parent as AttributeArgumentListSyntax;

        if (root is null || arguments is null)
            return;

        var count = arguments is ArgumentListSyntax call ? call.Arguments.Count : ((AttributeArgumentListSyntax)arguments).Arguments.Count;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(UnnamedLanguageAnalyzer.ParameterNamesProperty, out var value) || value is null)
                continue;

            var names = value.Split(',');

            // The call changed since the diagnostic was reported.
            if (names.Length != count)
                continue;

            var named = arguments is ArgumentListSyntax list ? (SyntaxNode)Name(list, names) : Name((AttributeArgumentListSyntax)arguments, names);

            // Each argument got its name since the diagnostic was reported.
            if (named.IsEquivalentTo(arguments))
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Name each language",
                    createChangedDocument: _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(arguments, named))),
                    equivalenceKey: "NameLanguages"),
                diagnostic);
        }
    }

    private static AttributeArgumentListSyntax Name(AttributeArgumentListSyntax arguments, string[] names)
    {
        var named = arguments.Arguments.Select((argument, i) => argument is { NameColon: null, NameEquals: null }
            ? argument.WithoutLeadingTrivia().WithNameColon(CreateName(names[i], argument.GetLeadingTrivia()))
            : argument);

        var result = arguments.WithArguments(SyntaxFactory.SeparatedList(named, arguments.Arguments.GetSeparators()));
        return result;
    }

    private static ArgumentListSyntax Name(ArgumentListSyntax arguments, string[] names)
    {
        var named = arguments.Arguments.Select((argument, i) => argument.NameColon is null ? Name(argument, names[i]) : argument);

        var result = arguments.WithArguments(SyntaxFactory.SeparatedList(named, arguments.Arguments.GetSeparators()));
        return result;
    }

    private static ArgumentSyntax Name(ArgumentSyntax argument, string name)
    {
        var namedArgument = argument.WithoutLeadingTrivia().WithNameColon(CreateName(name, argument.GetLeadingTrivia()));
        return namedArgument;
    }

    /// <summary>
    /// A language name with a keyword escaped, taking the argument's leading trivia.
    /// </summary>
    private static NameColonSyntax CreateName(string name, SyntaxTriviaList leadingTrivia)
    {
        var identifier = SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None ? name : $"@{name}";

        var nameColon = SyntaxFactory.NameColon(
                SyntaxFactory.IdentifierName(identifier),
                SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space))
            .WithLeadingTrivia(leadingTrivia);

        return nameColon;
    }
}
