namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads the wording of a text that is written as a constant: <c>=> "Submit"</c>, a getter returning a literal or a
/// <see langword="const"/>, or an initializer such as <c>{ get; } = "Submit";</c>.
/// </summary>
// One reader for the generator, which records the placeholders of a default wording, and for the analyzer, which holds
// the wording of every other language to them: wording one of them read and the other did not would be held to no
// placeholders at all.
internal static class ConstantWording
{
    /// <summary>
    /// The property's wording when it is a constant, else <see langword="null"/>.
    /// </summary>
    /// <param name="declaration">The property.</param>
    /// <param name="semanticModel">The semantic model of the tree the property lies in.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static string? Read(PropertyDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (FindExpression(declaration) is not { } expression
            || semanticModel.GetConstantValue(expression, cancellationToken) is not { HasValue: true, Value: string wording })
        {
            return null;
        }

        return wording;
    }

    /// <summary>
    /// The expression a property returns, when it returns one expression: its expression body, its initializer, or what
    /// its getter returns as its only statement.
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
