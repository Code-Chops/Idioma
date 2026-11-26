using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="InlineTextValuesAnalyzer"/>
public sealed partial class InlineTextValuesAnalyzer
{
    /// <summary>
    /// Tells a text among the values a wording shows. A text is wording, as a constant is, so another language may word
    /// it its own way.
    /// </summary>
    /// <remarks>
    /// A text is one of these:
    /// <list type="bullet">
    /// <item>A text of a generated class, such as <c>ShopLocalization.Checkout</c>.</item>
    /// <item>A property of this assembly whose getter gives a text and does nothing else, such as
    /// <c>static string Checkout => LocalizedText.Inline(en: "Checkout", nl: "Afrekenen");</c>. Its getter may give the
    /// text of another such property too.</item>
    /// </list>
    /// </remarks>
    /// <param name="compilation">The compilation, whose semantic models bind a getter in another file.</param>
    /// <param name="semanticModel">The semantic model of the file of the call.</param>
    /// <param name="text">The class <c>LocalizedText</c>, or <see langword="null"/> where the package isn't referenced.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
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
        /// The expression a property's getter gives and does nothing else with: its expression body, or the one statement
        /// of its getter's body that returns.
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
        /// The text inside what only passes it on: a conversion, parentheses, or a text that is <see langword="null"/>
        /// replaced by another wording, as in <c>LocalizedText.GetTextOfType&lt;Address&gt;() ?? "Address"</c>.
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
