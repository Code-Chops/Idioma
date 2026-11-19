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

        if (ConstantWording.ReadString(expression) is { } written)
            return written;

        // Asked of the compiler only where the syntax doesn't say it already: a wording that shows a value of the
        // method is no constant.
        if (!TakesValue(expression, declaration)
            && semanticModel.GetConstantValue(expression, cancellationToken) is { HasValue: true, Value: string constant })
        {
            return constant;
        }

        if (expression is not InterpolatedStringExpressionSyntax interpolated)
            return null;

        var wording = new StringBuilder();

        foreach (var content in interpolated.Contents)
        {
            // The text as it reads, and a value between single braces, however many the string is written with
            // ($$"""...{{name}}...""").
            if (content is InterpolatedStringTextSyntax text)
                wording.Append(ReadText(text));
            else if (content is InterpolationSyntax value)
                wording.Append('{').Append(value.Expression).Append(value.AlignmentClause).Append(value.FormatClause).Append('}');
        }

        return wording.ToString();
    }

    /// <summary>
    /// Whether the syntax of an expression says by itself that its value depends on a parameter of the method, so that
    /// it is no constant: <c>$"Hello, {name}!"</c>, or <c>count is 1 ? "1 item" : $"{count} items"</c>.
    /// <see langword="false"/> wherever only the compiler can tell.
    /// </summary>
    // Nearly every text that takes values is written this way, and the generators read each of them on every edit.
    // Binding one costs more than everything else that is read of it.
    internal static bool TakesValue(ExpressionSyntax expression, MethodDeclarationSyntax declaration)
    {
        HashSet<string>? names = null;

        foreach (var parameter in declaration.ParameterList.Parameters)
        {
            var name = parameter.Identifier.ValueText;

            // Not a name two parameters share, which does not compile. And not a parameter named like its type
            // (Color Color), whose name may stand for the type.
            if (name.Length is 0 || parameter.Type is null || String.Equals(GetRightmostName(parameter.Type), name, StringComparison.Ordinal))
                continue;

            if (!(names ??= new HashSet<string>(StringComparer.Ordinal)).Add(name))
                return false;
        }

        if (names is null)
            return false;

        // Not a name the expression declares anew, as a lambda parameter or a pattern does: there it stands for
        // something else, or does not compile.
        foreach (var token in expression.DescendantTokens())
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Parent is not IdentifierNameSyntax)
                names.Remove(token.ValueText);

        var takesValue = names.Count > 0 && TakesValue(expression, names);
        return takesValue;
    }

    /// <summary>
    /// Whether an expression is no constant because a part that its value is made of is a parameter. Each shape here is
    /// a constant only when every part named is one.
    /// </summary>
    private static bool TakesValue(ExpressionSyntax? expression, HashSet<string> parameters)
    {
        var takesValue = expression switch
        {
            IdentifierNameSyntax identifier => parameters.Contains(identifier.Identifier.ValueText),
            ParenthesizedExpressionSyntax parenthesized => TakesValue(parenthesized.Expression, parameters),
            InterpolatedStringExpressionSyntax interpolated => interpolated.Contents.Any(content => content is InterpolationSyntax value && TakesValue(value.Expression, parameters)),
            ConditionalExpressionSyntax conditional => TakesValue(conditional.Condition, parameters) || TakesValue(conditional.WhenTrue, parameters) || TakesValue(conditional.WhenFalse, parameters),
            BinaryExpressionSyntax binary when !binary.IsKind(SyntaxKind.IsExpression) && !binary.IsKind(SyntaxKind.AsExpression)
                => TakesValue(binary.Left, parameters) || TakesValue(binary.Right, parameters),
            IsPatternExpressionSyntax isPattern => TakesValue(isPattern.Expression, parameters),
            SwitchExpressionSyntax @switch => TakesValue(@switch.GoverningExpression, parameters),
            PrefixUnaryExpressionSyntax prefix => TakesValue(prefix.Operand, parameters),
            PostfixUnaryExpressionSyntax postfix => TakesValue(postfix.Operand, parameters),
            CastExpressionSyntax cast => TakesValue(cast.Expression, parameters),

            // A member of a value, or a call on one: name.Length, count.ToString().
            MemberAccessExpressionSyntax member => TakesValue(member.Expression, parameters),
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax called } => TakesValue(called.Expression, parameters),
            _ => false,
        };

        return takesValue;
    }

    /// <summary>
    /// The last name a type is written with: <c>Color</c> for <c>Shop.Color?</c>. <see langword="null"/> for a type
    /// written without one, such as <c>int</c> or a tuple.
    /// </summary>
    internal static string? GetRightmostName(TypeSyntax type)
    {
        var name = type switch
        {
            NullableTypeSyntax nullable => GetRightmostName(nullable.ElementType),
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => null,
        };

        return name;
    }

    /// <summary>
    /// A part of an interpolated string that is text, as it reads: without the escapes of the source.
    /// </summary>
    public static string ReadText(InterpolatedStringTextSyntax text)
    {
        // A raw string writes a brace that is text once, and any other string twice: the token of the text holds it as
        // it is written, where it holds every other escape as the character it stands for.
        var writesBraceTwice = text.Parent is not InterpolatedStringExpressionSyntax interpolated
                               || (!interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedSingleLineRawStringStartToken)
                                   && !interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken));

        var read = writesBraceTwice ? text.TextToken.ValueText.Replace("{{", "{").Replace("}}", "}") : text.TextToken.ValueText;
        return read;
    }
}
