namespace CodeChops.Idioma.Generators;

/// <summary>
/// Finds the strings a text returns as they're written, which is where its wording stands in the source. A placeholder
/// in one of them reaches the user as written (<c>LOC502</c>).
/// </summary>
/// <remarks>
/// <para>
/// A string is found when the text returns it by itself, or as a part of what it returns:
/// </para>
/// <list type="bullet">
/// <item>The text of an interpolated string, and a string in one of its values.</item>
/// <item>Either side of a concatenation (<c>+</c>) or of a coalesce (<c>??</c>).</item>
/// <item>Either branch of a conditional (<c>?:</c>).</item>
/// <item>An arm of a <see langword="switch"/> expression.</item>
/// </list>
/// <para>
/// No other string is found, because it isn't the wording itself:
/// </para>
/// <list type="bullet">
/// <item>A string passed to a method, such as the format of <c>string.Format</c>, which fills the placeholders in.</item>
/// <item>A string kept in a local variable, which the build doesn't follow.</item>
/// <item>A string in a condition, in a local function, or in a lambda.</item>
/// </list>
/// </remarks>
// A constant, an interpolation, a concatenation, a conditional, a coalesce, and a switch count as wording written in the source.
internal static class WrittenWording
{
    /// <summary>
    /// The strings a property or method returns as they're written: those of its expression body, of its initializer,
    /// and of each <see langword="return"/> of its body or of its getter.
    /// </summary>
    public static IEnumerable<SyntaxNode> FindStrings(MemberDeclarationSyntax text)
    {
        foreach (var returned in FindReturned(text))
            foreach (var written in FindStrings(returned))
                yield return written;
    }

    /// <summary>
    /// The strings an expression shows as they're written (see <see cref="WrittenWording"/>). Each is a string
    /// (<see cref="LiteralExpressionSyntax"/>) or the text of an interpolated string
    /// (<see cref="InterpolatedStringTextSyntax"/>).
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
    /// A string found here as it reads: without the escapes of the source.
    /// </summary>
    public static string Read(SyntaxNode written)
    {
        var read = written is InterpolatedStringTextSyntax text
            ? MethodWording.ReadText(text)
            : ((LiteralExpressionSyntax)written).Token.ValueText;

        return read;
    }

    /// <summary>
    /// The expressions that an expression shows one of, or shows next to each other, when it's no string itself. Empty
    /// for any other expression, such as a call or the name of a variable.
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

    /// <summary>
    /// The expressions a property or method returns.
    /// </summary>
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
    /// The expressions a body returns: its expression, or that of each <see langword="return"/> in its block.
    /// </summary>
    private static IEnumerable<ExpressionSyntax> FindReturned(ArrowExpressionClauseSyntax? expressionBody, BlockSyntax? body)
    {
        if (expressionBody is not null)
            yield return expressionBody.Expression;

        if (body is null)
            yield break;

        // Not what a local function or a lambda returns, which goes to the text's own code and not to the user.
        foreach (var node in body.DescendantNodes(static node => node is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)))
            if (node is ReturnStatementSyntax { Expression: { } returned })
                yield return returned;
    }
}
