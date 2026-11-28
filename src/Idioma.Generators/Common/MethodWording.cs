namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads the wording of a text that takes values as its method writes it (<c>Hello, {name}!</c>), for the doc comments
/// of the generated members.
/// </summary>
internal static class MethodWording
{
    /// <summary>
    /// The separator between the wordings a method chooses between.
    /// </summary>
    internal const string ChoiceSeparator = " / ";

    /// <summary>
    /// The constant or interpolated string a method returns, each value in braces as written. A choice with <c>? :</c>
    /// or a <c>switch</c> gives each wording, apart by <see cref="ChoiceSeparator"/>: <c>1 item / {count} items</c>.
    /// <see langword="null"/> for any other method.
    /// </summary>
    public static string? Read(MethodDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var expression = declaration.ExpressionBody?.Expression
                         ?? (declaration.Body is { Statements: [ReturnStatementSyntax { Expression: { } returned }] } ? returned : null);

        var wording = expression is null ? null : Read(expression, declaration, semanticModel, cancellationToken);
        return wording;
    }

    private static string? Read(ExpressionSyntax expression, MethodDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (ConstantWording.ReadString(expression) is { } written)
            return written;

        if (expression is ParenthesizedExpressionSyntax parenthesized)
            return Read(parenthesized.Expression, declaration, semanticModel, cancellationToken);

        // Asked of the compiler only where the syntax doesn't already show a parameter.
        if (!TakesValue(expression, declaration)
            && semanticModel.GetConstantValue(expression, cancellationToken) is { HasValue: true, Value: string constant })
        {
            return constant;
        }

        // A plural chooses its wording by a value, so the hover shows each wording it chooses between.
        if (FindChoices(expression) is { } choices)
        {
            var wordings = new List<string>();
            return AddChoices(choices, wordings, declaration, semanticModel, cancellationToken) ? String.Join(ChoiceSeparator, wordings) : null;
        }

        var wording = expression is InterpolatedStringExpressionSyntax interpolated ? ReadInterpolated(interpolated) : null;
        return wording;
    }

    private static string ReadInterpolated(InterpolatedStringExpressionSyntax interpolated)
    {
        var wording = new StringBuilder();

        foreach (var content in interpolated.Contents)
        {
            // A value between single braces, also in a raw string with more.
            if (content is InterpolatedStringTextSyntax text)
                wording.Append(ReadText(text));
            else if (content is InterpolationSyntax value)
                wording.Append('{').Append(value.Expression).Append(value.AlignmentClause).Append(value.FormatClause).Append('}');
        }

        return wording.ToString();
    }

    /// <summary>
    /// The branches of <c>? :</c> or the arms of a <c>switch</c> that don't throw, else <see langword="null"/>.
    /// </summary>
    private static IEnumerable<ExpressionSyntax>? FindChoices(ExpressionSyntax expression)
    {
        var choices = expression switch
        {
            ConditionalExpressionSyntax conditional => [conditional.WhenTrue, conditional.WhenFalse],
            SwitchExpressionSyntax @switch => @switch.Arms.Select(static arm => arm.Expression).Where(static arm => arm is not ThrowExpressionSyntax),
            _ => (IEnumerable<ExpressionSyntax>?)null,
        };

        return choices;
    }

    /// <summary>
    /// Adds each distinct wording a choice gives, nested choices included. <see langword="false"/> when one can't be
    /// read, so the hover never shows part of a text as all of it.
    /// </summary>
    // A literal or interpolated string is read as written without binding, because discovery reads every text on every
    // edit.
    private static bool AddChoices(IEnumerable<ExpressionSyntax> choices, List<string> wordings, MethodDeclarationSyntax declaration, SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var choice in choices)
        {
            var branch = choice;
            while (branch is ParenthesizedExpressionSyntax parenthesized)
                branch = parenthesized.Expression;

            if (FindChoices(branch) is { } inner)
            {
                if (!AddChoices(inner, wordings, declaration, semanticModel, cancellationToken))
                    return false;

                continue;
            }

            var wording = ConstantWording.ReadString(branch)
                          ?? (branch is InterpolatedStringExpressionSyntax interpolated
                              ? ReadInterpolated(interpolated)
                              : Read(branch, declaration, semanticModel, cancellationToken));

            if (wording is null)
                return false;

            if (!wordings.Contains(wording))
                wordings.Add(wording);
        }

        return wordings.Count > 0;
    }

    /// <summary>
    /// Whether the syntax alone shows that an expression depends on a parameter (<c>$"Hello, {name}!"</c>).
    /// <see langword="false"/> wherever only the compiler can tell.
    /// </summary>
    // Saves binding, which costs more than everything else read of a text.
    internal static bool TakesValue(ExpressionSyntax expression, MethodDeclarationSyntax declaration)
    {
        HashSet<string>? names = null;

        foreach (var parameter in declaration.ParameterList.Parameters)
        {
            var name = parameter.Identifier.ValueText;

            // Skips a parameter named like its type (Color Color), whose name may stand for the type.
            if (name.Length is 0 || parameter.Type is null || String.Equals(GetRightmostName(parameter.Type), name, StringComparison.Ordinal))
                continue;

            if (!(names ??= new HashSet<string>(StringComparer.Ordinal)).Add(name))
                return false;
        }

        if (names is null)
            return false;

        // A name the expression declares anew, as a lambda or a pattern does, stands for something else.
        foreach (var token in expression.DescendantTokens())
            if (token.IsKind(SyntaxKind.IdentifierToken) && token.Parent is not IdentifierNameSyntax)
                names.Remove(token.ValueText);

        var takesValue = names.Count > 0 && TakesValue(expression, names);
        return takesValue;
    }

    /// <summary>
    /// Whether a part of an expression's value is a parameter.
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
    /// <c>Color</c> for <c>Shop.Color?</c>, and <see langword="null"/> for <c>int</c> or a tuple.
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
        // The token keeps a doubled brace as written, though it unescapes everything else. A raw string doesn't double it.
        var writesBraceTwice = text.Parent is not InterpolatedStringExpressionSyntax interpolated
                               || (!interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedSingleLineRawStringStartToken)
                                   && !interpolated.StringStartToken.IsKind(SyntaxKind.InterpolatedMultiLineRawStringStartToken));

        var read = writesBraceTwice ? text.TextToken.ValueText.Replace("{{", "{").Replace("}}", "}") : text.TextToken.ValueText;
        return read;
    }
}
