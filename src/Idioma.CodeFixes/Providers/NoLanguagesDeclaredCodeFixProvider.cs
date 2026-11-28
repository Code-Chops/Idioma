using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a call of <c>LocalizedText.Inline</c> or a <c>[Localized]</c> in an assembly that declares no languages
/// (<c>LOC100</c>). It declares the languages the call names, in the order it names them, so the first is the default:
/// <c>LocalizedText.Inline(en: "Orders", nl: "Bestellingen")</c> adds
/// <c>[assembly: SupportedLanguages("en", "nl")]</c>.
/// </summary>
/// <remarks>
/// A call with an unnamed argument gets no fix, because only a name says which language a wording is in. A set gets no
/// fix, because its name doesn't say which language is the default. There's no "Fix all", because one attribute serves
/// every call.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NoLanguagesDeclaredCodeFixProvider))]
[Shared]
public sealed class NoLanguagesDeclaredCodeFixProvider : CodeFixProvider
{
    private const string AttributeName = "SupportedLanguages";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [LocalizationSetsPart.NoLanguagesDeclaredDiagnosticId];

    public override FixAllProvider? GetFixAllProvider() => null;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // The diagnostic sits on the name of the method or of the attribute.
        if (root is not CompilationUnitSyntax compilationUnit
            || compilationUnit.FindNode(context.Span, getInnermostNodeForTie: true) is not { } node
            || FindLanguages(node) is not { } languages)
        {
            return;
        }

        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (semanticModel is null)
            return;

        var arguments = String.Join(", ", languages.Select(static language => $"\"{language}\""));

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add [assembly: {AttributeName}({arguments})]",
                createChangedDocument: _ => Task.FromResult(document.WithSyntaxRoot(AddAttribute(compilationUnit, semanticModel, languages))),
                equivalenceKey: "DeclareLanguages"),
            context.Diagnostics);
    }

    /// <summary>
    /// The languages the argument names give, in order, or <see langword="null"/> when one is missing, invalid or repeated.
    /// </summary>
    private static string[]? FindLanguages(SyntaxNode node)
    {
        var names = node.Parent switch
        {
            MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax invocation } access when access.Name == node
                => invocation.ArgumentList.Arguments.Select(static argument => argument.NameColon?.Name.Identifier.ValueText).ToArray(),
            AttributeSyntax { ArgumentList: { } attributeArguments } attribute when attribute.Name == node
                => attributeArguments.Arguments.Select(static argument => argument.NameColon?.Name.Identifier.ValueText).ToArray(),
            _ => null,
        };

        if (names is not { Length: > 0 } || names.Any(static name => name is null))
            return null;

        var languages = names.Select(static name => ToLanguage(name!)).ToArray();

        var isValid = languages.All(SupportedLanguages.IsCultureName)
                      && languages.Distinct(StringComparer.OrdinalIgnoreCase).Count() == languages.Length;

        return isValid ? languages : null;
    }

    /// <summary>
    /// The reverse of <see cref="SupportedLanguages.ToParameterName"/>: <c>en-GB</c> for <c>enGb</c>, <c>zh-Hans</c>
    /// for <c>zhHans</c>.
    /// </summary>
    private static string ToLanguage(string parameterName)
    {
        var parts = new List<string>();
        var start = 0;

        for (var i = 1; i <= parameterName.Length; i++)
        {
            if (i < parameterName.Length && !Char.IsUpper(parameterName[i]))
                continue;

            var part = parameterName.Substring(start, i - start);

            parts.Add(parts.Count is 0
                ? part.ToLowerInvariant()
                : part.Length switch
                {
                    2 => part.ToUpperInvariant(),
                    4 => Char.ToUpperInvariant(part[0]) + part.Substring(1).ToLowerInvariant(),
                    _ => part.ToLowerInvariant(),
                });

            start = i;
        }

        var language = String.Join("-", parts);
        return language;
    }

    /// <summary>
    /// The file with <c>[assembly: SupportedLanguages(...)]</c> after its usings and other assembly attributes, by its
    /// name alone where that binds, else by its full name.
    /// </summary>
    private static CompilationUnitSyntax AddAttribute(CompilationUnitSyntax compilationUnit, SemanticModel semanticModel, string[] languages)
    {
        var endOfLine = CodeFixSupport.FindEndOfLine(compilationUnit);
        var first = compilationUnit.Members.FirstOrDefault();
        var position = first?.SpanStart ?? compilationUnit.EndOfFileToken.SpanStart;

        var bindsToAttribute = CodeFixSupport.BindsAt(semanticModel, position, $"{AttributeName}Attribute",
            static bound => String.Equals(bound.ToDisplayString(), PackageNames.SupportedLanguagesAttribute, StringComparison.Ordinal));

        var name = bindsToAttribute ? AttributeName : $"{PackageNames.Namespace}.{AttributeName}";
        var comma = SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space);

        var arguments = SyntaxFactory.SeparatedList(
            languages.Select(static language => SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(language)))),
            languages.Skip(1).Select(_ => comma));

        var attribute = SyntaxFactory.AttributeList(
                SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.AssemblyKeyword), SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space)),
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.ParseName(name), SyntaxFactory.AttributeArgumentList(arguments))))
            .WithTrailingTrivia(endOfLine);

        if (compilationUnit.AttributeLists.Count > 0 || first is null)
            return compilationUnit.AddAttributeLists(attribute);

        // Before the first member and a blank line. The member keeps its comments and directives.
        var leading = first.GetLeadingTrivia();
        var blank = 0;

        while (blank < leading.Count && leading[blank].Kind() is SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia)
            blank++;

        return compilationUnit
            .ReplaceNode(first, first.WithLeadingTrivia(leading.Skip(blank).Prepend(endOfLine)))
            .AddAttributeLists(attribute.WithLeadingTrivia(leading.Take(blank)));
    }
}
