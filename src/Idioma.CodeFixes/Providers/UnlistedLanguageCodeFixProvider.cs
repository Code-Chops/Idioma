using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a set of a language that <c>[assembly: SupportedLanguages]</c> doesn't list (<c>LOC202</c>). It adds the
/// language at the end of the attribute: <c>[assembly: SupportedLanguages("en", "nl")]</c> becomes
/// <c>[assembly: SupportedLanguages("en", "nl", "de")]</c>.
/// </summary>
/// <remarks>
/// <para>
/// A set whose name ends in a longer code adds that code, which its name then gives it: <c>en-GB</c> for
/// <c>ShopLocalizationEnGb</c>. The diagnostic carries the language to add
/// (<see cref="LocalizationSetsGenerator.SuggestedNameProperty"/>).
/// </para>
/// <para>
/// Every other contract then lacks a set of that language, which <c>LOC201</c> reports, and which its fix adds. The
/// attribute changes only where each of its arguments is a string written in it, in whichever file it is.
/// </para>
/// <para>
/// There's no "Fix all", because one fix serves every set of the language.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnlistedLanguageCodeFixProvider))]
[Shared]
public sealed class UnlistedLanguageCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [LocalizationSetsGenerator.UnlistedLanguageDiagnosticId];

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (semanticModel is null || await FindAttributeAsync(semanticModel.Compilation, context.CancellationToken).ConfigureAwait(false) is not { ArgumentList: { } arguments } attribute)
            return;

        // Each argument a string written in the attribute, so that one more fits after them.
        if (!arguments.Arguments.All(static argument => argument is { NameColon: null, NameEquals: null, Expression: LiteralExpressionSyntax literal } && literal.IsKind(SyntaxKind.StringLiteralExpression)))
            return;

        var listed = arguments.Arguments.Select(static argument => ((LiteralExpressionSyntax)argument.Expression).Token.ValueText).ToArray();

        foreach (var diagnostic in context.Diagnostics)
        {
            // Listed since the diagnostic was reported.
            if (!diagnostic.Properties.TryGetValue(LocalizationSetsGenerator.SuggestedNameProperty, out var language)
                || language is null
                || listed.Contains(language, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Add '{language}' to [assembly: SupportedLanguages]",
                    createChangedSolution: cancellationToken => CodeFixSupport.ReplaceNodesAsync(document.Project.Solution, [arguments], original => Add(original, language), cancellationToken),
                    equivalenceKey: $"ListLanguage:{language}"),
                diagnostic);
        }
    }

    /// <summary>
    /// <c>[assembly: SupportedLanguages(...)]</c> as written in a file of the project, or <see langword="null"/> when
    /// no file holds it.
    /// </summary>
    private static async Task<AttributeSyntax?> FindAttributeAsync(Compilation compilation, CancellationToken cancellationToken)
    {
        var attribute = compilation.Assembly.GetAttributes().FirstOrDefault(static attribute
            => String.Equals(attribute.AttributeClass?.ToDisplayString(), PackageNames.SupportedLanguagesAttribute, StringComparison.Ordinal));

        if (attribute?.ApplicationSyntaxReference is not { } reference)
            return null;

        return await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) as AttributeSyntax;
    }

    /// <summary>
    /// The arguments with a language after the last one, which takes over what stood after that one.
    /// </summary>
    private static AttributeArgumentListSyntax Add(AttributeArgumentListSyntax arguments, string language)
    {
        var last = arguments.Arguments[^1];
        var added = SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(language)));

        var items = arguments.Arguments.Replace(last, last.WithoutTrailingTrivia()).GetWithSeparators()
            .Add(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space))
            .Add(added.WithTrailingTrivia(last.GetTrailingTrivia()));

        var result = arguments.WithArguments(SyntaxFactory.SeparatedList<AttributeArgumentSyntax>(items));
        return result;
    }
}
