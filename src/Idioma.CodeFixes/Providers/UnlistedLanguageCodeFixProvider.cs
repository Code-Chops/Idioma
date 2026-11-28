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
/// The diagnostic carries the language to add (<see cref="LocalizationSetsPart.SuggestedNameProperty"/>). The attribute
/// changes only where each argument is a string literal. There's no "Fix all", because one fix serves every set of the
/// language.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnlistedLanguageCodeFixProvider))]
[Shared]
public sealed class UnlistedLanguageCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [LocalizationSetsPart.UnlistedLanguageDiagnosticId];

    /// <summary>
    /// The properties with a language to list, each a fix of its own, the likely one first.
    /// </summary>
    private static readonly string[] LanguageProperties = [LocalizationSetsPart.SuggestedNameProperty, LocalizationSetsPart.AlternativeNameProperty];

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (semanticModel is null
            || await FindAttributeAsync(semanticModel.Compilation, context.CancellationToken).ConfigureAwait(false) is not ({ ArgumentList: { } arguments }, var constructor))
        {
            return;
        }

        // Each argument must be a string literal, so one more fits after them. A name is fine where it names the
        // parameter of its place, as in SupportedLanguages(defaultLanguage: "en", "nl").
        for (var i = 0; i < arguments.Arguments.Count; i++)
        {
            var argument = arguments.Arguments[i];

            if (argument is not { NameEquals: null, Expression: LiteralExpressionSyntax literal }
                || !literal.IsKind(SyntaxKind.StringLiteralExpression)
                || (argument.NameColon is { } nameColon && !NamesParameterOfItsPlace(nameColon, i, constructor)))
            {
                return;
            }
        }

        var listed = arguments.Arguments.Select(static argument => ((LiteralExpressionSyntax)argument.Expression).Token.ValueText).ToArray();

        foreach (var diagnostic in context.Diagnostics)
        {
            // The language the name likely means, then the other one it may mean, such as fr-BE and be for
            // ShopLocalizationFrBe.
            foreach (var property in LanguageProperties)
            {
                // Listed since the diagnostic was reported.
                if (!diagnostic.Properties.TryGetValue(property, out var language)
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
    }

    /// <summary>
    /// <c>[assembly: SupportedLanguages(...)]</c> with its constructor, or <see langword="null"/> when no file holds it.
    /// </summary>
    private static async Task<(AttributeSyntax Syntax, IMethodSymbol? Constructor)?> FindAttributeAsync(Compilation compilation, CancellationToken cancellationToken)
    {
        var attribute = compilation.Assembly.GetAttributes().FirstOrDefault(static attribute
            => String.Equals(attribute.AttributeClass?.ToDisplayString(), PackageNames.SupportedLanguagesAttribute, StringComparison.Ordinal));

        if (attribute?.ApplicationSyntaxReference is not { } reference
            || await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false) is not AttributeSyntax syntax)
        {
            return null;
        }

        return (syntax, attribute.AttributeConstructor);
    }

    /// <summary>
    /// Whether an argument's name is that of the non-<see langword="params"/> parameter of its place, so it takes that place anyway.
    /// </summary>
    private static bool NamesParameterOfItsPlace(NameColonSyntax nameColon, int index, IMethodSymbol? constructor)
    {
        var namesIt = constructor is not null
            && index < constructor.Parameters.Length
            && constructor.Parameters[index] is { IsParams: false } parameter
            && String.Equals(parameter.Name, nameColon.Name.Identifier.ValueText, StringComparison.Ordinal);

        return namesIt;
    }

    /// <summary>
    /// The arguments with a language added at the end, taking the last one's trailing trivia.
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
