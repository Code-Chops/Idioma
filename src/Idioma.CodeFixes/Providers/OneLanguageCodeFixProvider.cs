using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes wording written in one language (<c>LOC500</c>). It makes the wording the default language's wording of an
/// inline text: <c>Title = "Orders";</c> becomes <c>Title = LocalizableText.Inline(en: "Orders");</c>.
/// </summary>
/// <remarks>
/// <para>
/// The other languages are left for the user to write. The compiler then names each language the call lacks
/// (<c>CS7036</c>), so no language gets wording that nobody wrote for it. A fix that copied the wording into every
/// language would compile, and every user would see it untranslated, which is what <c>LOC500</c> catches.
/// </para>
/// <para>
/// These get no fix:
/// </para>
/// <list type="bullet">
/// <item>Wording added to a text, as in <c>Title += " (draft)";</c>, because the text it's added to has wording of its
/// own.</item>
/// <item>A constant, as in <c>Title = Heading;</c>, because its wording is written where it's declared.</item>
/// <item>A lookup by wording, as in <c>texts.Contains("Orders")</c> or <c>counts["Orders"]</c>, because the text to
/// look up exists already, such as <c>ShopLocalization.Orders</c>.</item>
/// <item>The wording of <c>LocalizableText.Keyed</c>, or of a constructor of the text, which a call takes as an
/// argument of its own.</item>
/// </list>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OneLanguageCodeFixProvider))]
[Shared]
public sealed class OneLanguageCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// The methods of a collection that look a text up by its wording, such as <c>Contains</c>.
    /// </summary>
    private static readonly ImmutableHashSet<string> LookupMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Contains", "ContainsKey", "IndexOf", "LastIndexOf", "Remove", "TryGetValue");

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [OneLanguageTextAnalyzer.OneLanguageDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // The diagnostic sits on the wording, or on the cast that converts it to a text.
        if (root?.FindNode(context.Span, getInnermostNodeForTie: true) is not ExpressionSyntax node
            || node.Span != context.Span
            || FindWording(node) is not { } wording)
        {
            return;
        }

        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (semanticModel is null || SupportedLanguages.ReadCodes(semanticModel.Compilation.Assembly) is not [var defaultLanguage, ..])
            return;

        var parameter = SupportedLanguages.ToParameterName(defaultLanguage);
        var inline = CreateInline(wording, parameter, semanticModel, node.SpanStart).WithTriviaFrom(node);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Write it as {PackageNames.LocalizableTextName}.{LocalizableTextFactoryGenerator.FactoryName}({parameter}: ...)",
                createChangedDocument: _ => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(node, inline))),
                equivalenceKey: "WriteInline"),
            context.Diagnostics);
    }

    /// <summary>
    /// The wording a reported expression holds, without the cast or the parentheses around it, or
    /// <see langword="null"/> where the fix doesn't apply (see the remarks of the class).
    /// </summary>
    private static ExpressionSyntax? FindWording(ExpressionSyntax node)
    {
        // A call of Keyed or a constructor, which takes the wording as an argument.
        if (node is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax)
            return null;

        if (IsAddedToText(node) || IsLookup(node))
            return null;

        var wording = node is CastExpressionSyntax cast ? cast.Expression : node;

        while (wording is ParenthesizedExpressionSyntax parenthesized)
            wording = parenthesized.Expression;

        // A constant, whose wording is written where it's declared.
        if (wording is IdentifierNameSyntax or MemberAccessExpressionSyntax)
            return null;

        return wording;
    }

    /// <summary>
    /// Whether an expression is added to a text, as in <c>Title += " (draft)";</c>.
    /// </summary>
    private static bool IsAddedToText(ExpressionSyntax node)
    {
        var isAdded = node.Parent is AssignmentExpressionSyntax assignment
                      && assignment.IsKind(SyntaxKind.AddAssignmentExpression)
                      && assignment.Right == node;

        return isAdded;
    }

    /// <summary>
    /// Whether an expression is the argument of a lookup: of an indexer, as in <c>counts["Orders"]</c>, or of a method
    /// that looks a text up, as in <c>texts.Contains("Orders")</c>.
    /// </summary>
    private static bool IsLookup(ExpressionSyntax node)
    {
        if (node.Parent is not ArgumentSyntax argument)
            return false;

        if (argument.Parent is BracketedArgumentListSyntax)
            return true;

        var name = argument.Parent?.Parent is InvocationExpressionSyntax invocation
            ? invocation.Expression switch
            {
                MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                SimpleNameSyntax simpleName => simpleName.Identifier.ValueText,
                _ => null,
            }
            : null;

        var isLookup = name is not null && LookupMethods.Contains(name);
        return isLookup;
    }

    /// <summary>
    /// <c>LocalizableText.Inline(en: wording)</c>, with the text named as the code at a position can name it: by its
    /// name alone where that binds to the text, and else by its full name.
    /// </summary>
    private static InvocationExpressionSyntax CreateInline(ExpressionSyntax wording, string parameter, SemanticModel semanticModel, int position)
    {
        var simpleName = SyntaxFactory.IdentifierName(PackageNames.LocalizableTextName);

        var bindsToText = semanticModel.GetSpeculativeTypeInfo(position, simpleName, SpeculativeBindingOption.BindAsTypeOrNamespace).Type is INamedTypeSymbol bound
            && String.Equals(bound.ToDisplayString(), PackageNames.LocalizableText, StringComparison.Ordinal);

        ExpressionSyntax text = bindsToText ? simpleName : SyntaxFactory.ParseName(PackageNames.LocalizableText);

        var argument = SyntaxFactory.Argument(wording.WithoutTrivia())
            .WithNameColon(SyntaxFactory.NameColon(
                SyntaxFactory.IdentifierName(parameter),
                SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space)));

        var inline = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, text, SyntaxFactory.IdentifierName(LocalizableTextFactoryGenerator.FactoryName)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(argument)));

        return inline;
    }
}
