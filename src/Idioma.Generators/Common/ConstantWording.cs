namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads the wording of a text written as a constant, for the doc comments of the generated members.
/// </summary>
internal static class ConstantWording
{
    /// <summary>
    /// The property's wording when it's a constant, else <see langword="null"/>.
    /// </summary>
    public static string? Read(PropertyDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (FindExpression(declaration) is not { } expression)
            return null;

        if (ReadString(expression) is { } written)
            return written;

        if (semanticModel.GetConstantValue(expression, cancellationToken) is not { HasValue: true, Value: string wording })
            return null;

        return wording;
    }

    /// <summary>
    /// The wording of a string literal of any kind, else <see langword="null"/>.
    /// </summary>
    // Read from the token without binding, because the generators read every text on every edit.
    public static string? ReadString(ExpressionSyntax expression)
    {
        var written = expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;

        return written;
    }

    /// <summary>
    /// The expression body, initializer, or single returned expression of a getter.
    /// </summary>
    private static ExpressionSyntax? FindExpression(PropertyDeclarationSyntax declaration)
    {
        if ((declaration.ExpressionBody?.Expression ?? declaration.Initializer?.Value) is { } expression)
            return expression;

        if (declaration.AccessorList is null)
            return null;

        foreach (var accessor in declaration.AccessorList.Accessors)
        {
            if (!accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                continue;

            return accessor.ExpressionBody?.Expression
                   ?? (accessor.Body is { Statements: [ReturnStatementSyntax { Expression: { } returned }] } ? returned : null);
        }

        return null;
    }
}
