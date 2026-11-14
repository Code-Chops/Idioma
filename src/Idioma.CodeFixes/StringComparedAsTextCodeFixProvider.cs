using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Simplification;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a string compared as a text with <c>Equals</c> (<c>LOC017</c>): <c>text.Equals(wording)</c> becomes
/// <c>text == wording</c>, which compares the wording the user sees, and <c>!text.Equals(wording)</c> becomes
/// <c>text != wording</c>.
/// </summary>
/// <remarks>
/// Only for <c>Equals</c> called on a text, with the string as its one argument. A lookup of a collection, such as
/// <c>texts.Contains(wording)</c>, gets no fix: it would take a lambda, as in <c>texts.Any(text => text == wording)</c>,
/// written for each kind of lookup. Nor does a text reached through <c>?.</c>, as in <c>order?.Title.Equals(wording)</c>,
/// which gives a <c>bool?</c> that no comparison stands in for. Unlike <c>Equals</c>, <c>==</c> does not throw on a <see langword="null"/> text, which
/// shows no wording, so the comparison is false.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StringComparedAsTextCodeFixProvider))]
[Shared]
public sealed class StringComparedAsTextCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [TextComparisonAnalyzer.StringComparedAsTextDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        // The diagnostic sits on the string: the one argument of text.Equals(string).
        if (root?.FindNode(context.Span, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArgumentSyntax>() is not
            {
                NameColon: null,
                RefKindKeyword.RawKind: (int)SyntaxKind.None,
                Parent: ArgumentListSyntax
                {
                    Arguments.Count: 1,
                    Parent: InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax
                        {
                            RawKind: (int)SyntaxKind.SimpleMemberAccessExpression,
                            Expression: not BaseExpressionSyntax,
                            Name.Identifier.ValueText: "Equals",
                        } access,
                    } invocation,
                },
            } argument
            || argument.Expression.Span != context.Span
            || IsConditionallyAccessed(access.Expression))
        {
            return;
        }

        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        // The text's own Equals, not that of an interface or a comparer, which == would not stand in for.
        if (semanticModel?.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsText(method.ContainingType))
        {
            return;
        }

        var negation = FindNegation(invocation);
        var @operator = negation is null ? "==" : "!=";

        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Compare the wording with '{@operator}'",
                    createChangedDocument: cancellationToken => CompareWordingAsync(document, invocation, access.Expression, argument.Expression, negation, cancellationToken),
                    equivalenceKey: "CompareWording"),
                diagnostic);
        }
    }

    /// <summary>
    /// Replaces <c>text.Equals(wording)</c> with <c>text == wording</c>, or the negation around it with
    /// <c>text != wording</c>. Parentheses go around the comparison and the string, and the simplifier removes those
    /// the code does not need: <c>(text == wording).ToString()</c> needs them, <c>return text == wording;</c> does not.
    /// </summary>
    private static async Task<Document> CompareWordingAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax text,
        ExpressionSyntax wording,
        PrefixUnaryExpressionSyntax? negation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
            return document;

        var replaced = negation ?? (ExpressionSyntax)invocation;

        var comparison = SyntaxFactory.BinaryExpression(
            negation is null ? SyntaxKind.EqualsExpression : SyntaxKind.NotEqualsExpression,
            text.WithoutTrivia(),
            Parenthesize(wording.WithoutTrivia()));

        document = document.WithSyntaxRoot(root.ReplaceNode(replaced, Parenthesize(comparison).WithTriviaFrom(replaced)));
        return document;
    }

    /// <summary>
    /// The <c>!</c> in front of the call, also with parentheses between, as in <c>!(text.Equals(wording))</c>, or
    /// <see langword="null"/> when there is none.
    /// </summary>
    private static PrefixUnaryExpressionSyntax? FindNegation(InvocationExpressionSyntax invocation)
    {
        SyntaxNode node = invocation;
        while (node.Parent is ParenthesizedExpressionSyntax parenthesized)
            node = parenthesized;

        if (node.Parent is not PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negation)
            return null;

        return negation;
    }

    /// <summary>
    /// Whether the text is reached through <c>?.</c> or <c>?[]</c>, as in <c>order?.Title.Equals(wording)</c>. The call
    /// then stands in a conditional access, which no comparison can replace it in: <c>==</c> cannot follow <c>?.</c>,
    /// and the result would be a <c>bool</c> where the call gives a <c>bool?</c>.
    /// </summary>
    private static bool IsConditionallyAccessed(ExpressionSyntax text)
    {
        var expression = text;
        while (true)
        {
            switch (expression)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    expression = memberAccess.Expression;
                    continue;
                case InvocationExpressionSyntax call:
                    expression = call.Expression;
                    continue;
                case ElementAccessExpressionSyntax elementAccess:
                    expression = elementAccess.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppression:
                    expression = suppression.Operand;
                    continue;
                default:
                    return expression is MemberBindingExpressionSyntax or ElementBindingExpressionSyntax;
            }
        }
    }

    private static ParenthesizedExpressionSyntax Parenthesize(ExpressionSyntax expression)
    {
        var parenthesize = SyntaxFactory.ParenthesizedExpression(expression).WithAdditionalAnnotations(Simplifier.Annotation);
        return parenthesize;
    }

    private static bool IsText(INamedTypeSymbol? type)
    {
        for (; type is not null; type = type.BaseType)
            if (String.Equals(type.ToDisplayString(), PackageNames.LocalizableText, StringComparison.Ordinal))
                return true;

        return false;
    }
}
