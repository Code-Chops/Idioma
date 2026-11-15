namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads the wording of a text that takes values, as its method writes it: <c>Hello, {name}!</c> for
/// <c>=> $"Hello, {name}!"</c>. For the doc comments of the generated members, which show the wording of every
/// language.
/// </summary>
internal static class MethodWording
{
    /// <summary>
    /// The wording a method returns when it returns one expression that is a constant or an interpolated string, each
    /// value of which is shown as it is written, in braces. <see langword="null"/> for any other method, such as one
    /// that chooses between wordings.
    /// </summary>
    /// <param name="declaration">The method.</param>
    /// <param name="semanticModel">The semantic model of the tree the method lies in.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static string? Read(MethodDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var expression = declaration.ExpressionBody?.Expression
                         ?? (declaration.Body is { Statements: [ReturnStatementSyntax { Expression: { } returned }] } ? returned : null);

        if (expression is null)
            return null;

        if (semanticModel.GetConstantValue(expression, cancellationToken) is { HasValue: true, Value: string constant })
            return constant;

        if (expression is not InterpolatedStringExpressionSyntax interpolated)
            return null;

        // A raw string writes a brace that is text once, and any other string twice: the token of the text holds it as
        // it is written, where it holds every other escape as the character it stands for.
        var writesBraceTwice = !interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedSingleLineRawStringStartToken)
                               && !interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken);
        var wording = new StringBuilder();

        foreach (var content in interpolated.Contents)
        {
            // The text as it reads, and a value between single braces, however many the string is written with
            // ($$"""...{{name}}...""").
            if (content is InterpolatedStringTextSyntax text)
                wording.Append(writesBraceTwice ? text.TextToken.ValueText.Replace("{{", "{").Replace("}}", "}") : text.TextToken.ValueText);
            else if (content is InterpolationSyntax value)
                wording.Append('{').Append(value.Expression).Append(value.AlignmentClause).Append(value.FormatClause).Append('}');
        }

        return wording.ToString();
    }
}
