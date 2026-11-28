namespace CodeChops.Idioma.Generators;

/// <summary>
/// Finds the strings a text returns as written, where a placeholder reaches the user (<c>LOC403</c>).
/// </summary>
/// <remarks>
/// A string counts when returned by itself, or as part of an interpolation, <c>+</c>, <c>??</c>, <c>?:</c> or a
/// <see langword="switch"/> expression. A string passed to a method, kept in a local, or inside a condition, local
/// function or lambda isn't the wording itself.
/// </remarks>
internal static class WrittenWording
{
    /// <summary>
    /// The strings a property or method returns as written.
    /// </summary>
    public static IEnumerable<SyntaxNode> FindStrings(MemberDeclarationSyntax text)
    {
        foreach (var returned in FindReturned(text))
            foreach (var written in FindStrings(returned))
                yield return written;
    }

    /// <summary>
    /// The strings an expression shows as written: each a <see cref="LiteralExpressionSyntax"/> or an
    /// <see cref="InterpolatedStringTextSyntax"/>.
    /// </summary>
    public static IEnumerable<SyntaxNode> FindStrings(ExpressionSyntax? expression)
    {
        if (expression is LiteralExpressionSyntax literal)
        {
            if (literal.IsKind(SyntaxKind.StringLiteralExpression))
                yield return literal;

            yield break;
        }

        if (expression is InterpolatedStringExpressionSyntax interpolated)
        {
            foreach (var content in interpolated.Contents)
            {
                if (content is InterpolatedStringTextSyntax text)
                {
                    yield return text;
                }
                else if (content is InterpolationSyntax value)
                {
                    foreach (var written in FindStrings(value.Expression))
                        yield return written;
                }
            }

            yield break;
        }

        foreach (var part in FindParts(expression))
            foreach (var written in FindStrings(part))
                yield return written;
    }

    /// <summary>
    /// A string found here, without the escapes of the source.
    /// </summary>
    public static string Read(SyntaxNode written)
    {
        var read = written is InterpolatedStringTextSyntax text
            ? MethodWording.ReadText(text)
            : ((LiteralExpressionSyntax)written).Token.ValueText;

        return read;
    }

    /// <summary>
    /// The parts an expression shows, such as both sides of <c>+</c>. Empty for a call or a variable.
    /// </summary>
    private static ExpressionSyntax[] FindParts(ExpressionSyntax? expression)
    {
        ExpressionSyntax[] parts = expression switch
        {
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) || binary.IsKind(SyntaxKind.CoalesceExpression) => [binary.Left, binary.Right],
            ConditionalExpressionSyntax conditional => [conditional.WhenTrue, conditional.WhenFalse],
            SwitchExpressionSyntax @switch => [.. @switch.Arms.Select(static arm => arm.Expression)],
            ParenthesizedExpressionSyntax parenthesized => [parenthesized.Expression],
            CastExpressionSyntax cast => [cast.Expression],
            PostfixUnaryExpressionSyntax suppression when suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression) => [suppression.Operand],
            _ => [],
        };

        return parts;
    }

    private static IEnumerable<ExpressionSyntax> FindReturned(MemberDeclarationSyntax text)
    {
        switch (text)
        {
            case PropertyDeclarationSyntax property:
                if (property.ExpressionBody is { } expressionBody)
                    yield return expressionBody.Expression;

                if (property.Initializer is { } initializer)
                    yield return initializer.Value;

                if (property.AccessorList is not { } accessors)
                    break;

                foreach (var accessor in accessors.Accessors)
                    if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                        foreach (var returned in FindReturned(accessor.ExpressionBody, accessor.Body))
                            yield return returned;

                break;

            case MethodDeclarationSyntax method:
                foreach (var returned in FindReturned(method.ExpressionBody, method.Body))
                    yield return returned;

                break;
        }
    }

    /// <summary>
    /// A body's expression, or that of each <see langword="return"/> in its block.
    /// </summary>
    private static IEnumerable<ExpressionSyntax> FindReturned(ArrowExpressionClauseSyntax? expressionBody, BlockSyntax? body)
    {
        if (expressionBody is not null)
            yield return expressionBody.Expression;

        if (body is null)
            yield break;

        // Not what a local function or lambda returns, which doesn't reach the user.
        foreach (var node in body.DescendantNodes(static node => node is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)))
            if (node is ReturnStatementSyntax { Expression: { } returned })
                yield return returned;
    }
}
