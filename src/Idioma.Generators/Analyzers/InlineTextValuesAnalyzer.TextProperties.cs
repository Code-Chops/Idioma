using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="InlineTextValuesAnalyzer"/>
public sealed partial class InlineTextValuesAnalyzer
{
    /// <summary>
    /// Tells a text among the values a wording shows, because a text is wording and no value. A text is a text of a
    /// generated class, or a property of this assembly whose getter only gives a text or another such property.
    /// </summary>
    /// <param name="compilation">Binds a getter in another file.</param>
    /// <param name="text">The class <c>LocalizedText</c>, or <see langword="null"/> where the package isn't referenced.</param>
    private sealed class TextProperties(Compilation compilation, SemanticModel? semanticModel, INamedTypeSymbol? text, CancellationToken cancellationToken)
    {
        /// <summary>
        /// How many properties deep a getter that gives another property's text is followed.
        /// </summary>
        private const int MaxDepth = 4;

        private WordingReader Reader { get; } = new(compilation, semanticModel);

        public bool IsText(ISymbol value)
        {
            return this.IsText(value, MaxDepth);
        }

        private bool IsText(ISymbol value, int depth)
        {
            if (KeptTextAnalyzer.IsTextOfGeneratedClass(value))
                return true;

            if (text is null || depth is 0 || value is not IPropertySymbol { Type.SpecialType: SpecialType.System_String, Parameters.IsEmpty: true } property)
                return false;

            foreach (var reference in property.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax(cancellationToken) is not PropertyDeclarationSyntax declaration
                    || FindGiven(declaration) is not { } given
                    || this.Reader.FindModel(given.SyntaxTree)?.GetOperation(given, cancellationToken) is not { } operation)
                {
                    continue;
                }

                operation = Unwrap(operation);

                if (KeptTextAnalyzer.IsTextRead(operation, text))
                    return true;

                if (FindValue(operation) is { } other && this.IsText(other, depth - 1))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The expression a getter gives when that's all it does: an expression body or a lone return statement.
        /// </summary>
        private static ExpressionSyntax? FindGiven(PropertyDeclarationSyntax declaration)
        {
            if (declaration.ExpressionBody is { } body)
                return body.Expression;

            var getter = declaration.AccessorList?.Accessors.FirstOrDefault(static accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));

            var given = getter switch
            {
                { ExpressionBody: { } expressionBody } => expressionBody.Expression,
                { Body.Statements: [ReturnStatementSyntax { Expression: { } returned }] } => returned,
                _ => null,
            };

            return given;
        }

        /// <summary>
        /// The text inside a conversion, parentheses, or the left side of <c>??</c>.
        /// </summary>
        private static IOperation Unwrap(IOperation operation)
        {
            while (true)
            {
                switch (operation)
                {
                    case IConversionOperation conversion:
                        operation = conversion.Operand;
                        break;

                    case IParenthesizedOperation parenthesized:
                        operation = parenthesized.Operand;
                        break;

                    case ICoalesceOperation coalesce:
                        operation = coalesce.Value;
                        break;

                    default:
                        return operation;
                }
            }
        }
    }
}
