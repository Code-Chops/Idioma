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
/// <para>
/// The attribute goes at the top of the file of the call, after its usings. A name of an argument gives a language as
/// the generated factory names it, so <c>enGb</c> gives <c>en-GB</c>.
/// </para>
/// <para>
/// A call with an argument without a name gets no fix, because only a name says which language a wording is in. A set
/// gets no fix either: its name gives its language, but not which language is the default.
/// </para>
/// <para>
/// There's no "Fix all", because the one attribute serves every call.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NoLanguagesDeclaredCodeFixProvider))]
[Shared]
public sealed class NoLanguagesDeclaredCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// The name of <c>[assembly: SupportedLanguages]</c> as an attribute is written, without its suffix.
    /// </summary>
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
    /// The languages the arguments of a call or an attribute name, in the order they're written, or
    /// <see langword="null"/> when an argument has no name, or a name gives no culture name or one twice.
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
    /// The language a parameter of the generated factory stands for, the reverse of
    /// <see cref="SupportedLanguages.ToParameterName"/>: <c>en-GB</c> for <c>enGb</c>, and <c>zh-Hans</c> for
    /// <c>zhHans</c>. A region is in upper case in a culture name, and a script starts with a capital.
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
    /// The file with <c>[assembly: SupportedLanguages(...)]</c> after its usings and its other assembly attributes.
    /// The attribute is named as the file can name it: by its name alone where that binds to it, and else by its full
    /// name.
    /// </summary>
    private static CompilationUnitSyntax AddAttribute(CompilationUnitSyntax compilationUnit, SemanticModel semanticModel, string[] languages)
    {
        var endOfLine = CodeFixSupport.FindEndOfLine(compilationUnit);
        var first = compilationUnit.Members.FirstOrDefault();
        var position = first?.SpanStart ?? compilationUnit.EndOfFileToken.SpanStart;

        var bindsToAttribute = semanticModel.GetSpeculativeTypeInfo(position, SyntaxFactory.IdentifierName($"{AttributeName}Attribute"), SpeculativeBindingOption.BindAsTypeOrNamespace).Type is INamedTypeSymbol bound
            && String.Equals(bound.ToDisplayString(), PackageNames.SupportedLanguagesAttribute, StringComparison.Ordinal);

        var name = bindsToAttribute ? AttributeName : $"{PackageNames.Namespace}.{AttributeName}";
        var comma = SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space);

        var arguments = SyntaxFactory.SeparatedList(
            languages.Select(static language => SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(language)))),
            languages.Skip(1).Select(_ => comma));

        var attribute = SyntaxFactory.AttributeList(
                SyntaxFactory.AttributeTargetSpecifier(SyntaxFactory.Token(SyntaxKind.AssemblyKeyword), SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space)),
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.ParseName(name), SyntaxFactory.AttributeArgumentList(arguments))))
            .WithTrailingTrivia(endOfLine);

        // After the other assembly attributes, which keep what stands between them and the first member.
        if (compilationUnit.AttributeLists.Count > 0 || first is null)
            return compilationUnit.AddAttributeLists(attribute);

        // Before the first member, with a blank line between them. The blank lines before that member now stand before
        // the attribute, and its comments, such as its doc comment, and its directives stay with it.
        var leading = first.GetLeadingTrivia();
        var blank = 0;

        while (blank < leading.Count && leading[blank].Kind() is SyntaxKind.WhitespaceTrivia or SyntaxKind.EndOfLineTrivia)
            blank++;

        return compilationUnit
            .ReplaceNode(first, first.WithLeadingTrivia(leading.Skip(blank).Prepend(endOfLine)))
            .AddAttributeLists(attribute.WithLeadingTrivia(leading.Take(blank)));
    }
}
