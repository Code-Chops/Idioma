using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a member of an enum without <c>[Localized]</c> next to members that have it (<c>LOC501</c>). It marks the
/// member with its wording in the default language: <c>Lost</c> becomes <c>[Localized(en: "Lost")] Lost</c>. The
/// compiler then names each language the attribute lacks (<c>CS7036</c>).
/// </summary>
/// <remarks>
/// The wording is the member's name, which is what users see now, so the fix changes nothing they see. The attribute is
/// written as the enum's other members write it.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnmarkedEnumMemberCodeFixProvider))]
[Shared]
public sealed class UnmarkedEnumMemberCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [LocalizedMemberAnalyzer.UnmarkedEnumMemberDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        // The diagnostic sits on the name of the member.
        if (root?.FindToken(context.Span.Start).Parent is not EnumMemberDeclarationSyntax { Parent: EnumDeclarationSyntax declaration } member
            || semanticModel is null
            || SupportedLanguages.ReadCodes(semanticModel.Compilation.Assembly) is not [var defaultLanguage, ..]
            || FindMarked(declaration, semanticModel, context.CancellationToken) is not { } marked)
        {
            return;
        }

        var parameter = SupportedLanguages.ToParameterName(defaultLanguage);
        var name = member.Identifier.ValueText;
        var attribute = $"[{marked.Attribute.Name}({parameter}: \"{name}\")]";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Mark it {attribute}",
                createChangedDocument: _ => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(member, Mark(member, marked, parameter)))),
                equivalenceKey: "MarkEnumMember"),
            context.Diagnostics);
    }

    /// <summary>
    /// The first <c>[Localized]</c> on a member of an enum, with its list, or <see langword="null"/>.
    /// </summary>
    private static (AttributeSyntax Attribute, AttributeListSyntax List)? FindMarked(EnumDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        foreach (var list in declaration.Members.SelectMany(static member => member.AttributeLists))
        {
            foreach (var attribute in list.Attributes)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(attribute, cancellationToken);
                var type = (symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault())?.ContainingType;

                if (LocalizedMembers.IsGeneratedAttribute(type, semanticModel.Compilation.Assembly))
                    return (attribute, list);
            }
        }

        return null;
    }

    /// <summary>
    /// The member with <c>[Localized(en: "Lost")]</c> before its other attributes, on a line of its own where the
    /// attribute it copies stands on one.
    /// </summary>
    private static EnumMemberDeclarationSyntax Mark(EnumMemberDeclarationSyntax member, (AttributeSyntax Attribute, AttributeListSyntax List) marked, string parameter)
    {
        var wording = SyntaxFactory.AttributeArgument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(member.Identifier.ValueText)))
            .WithNameColon(SyntaxFactory.NameColon(SyntaxFactory.IdentifierName(parameter), SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(SyntaxFactory.Space)));

        var attribute = SyntaxFactory.Attribute(marked.Attribute.Name.WithoutTrivia(), SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(wording)));

        // The line break after the copied attribute, and the indentation before the member's first token.
        var ownLine = marked.List.GetTrailingTrivia().FirstOrDefault(static trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));
        var leading = member.GetLeadingTrivia();
        var indentation = leading.Reverse().TakeWhile(static trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia)).Reverse();

        // Without elastic trivia, which the IDE would reformat.
        var list = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
            .NormalizeWhitespace()
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(ownLine.IsKind(SyntaxKind.EndOfLineTrivia) ? ownLine : SyntaxFactory.Space);

        var first = member.GetFirstToken();
        var rest = member.ReplaceToken(first, first.WithLeadingTrivia(ownLine.IsKind(SyntaxKind.EndOfLineTrivia) ? indentation : Enumerable.Empty<SyntaxTrivia>()));

        var result = rest.WithAttributeLists(rest.AttributeLists.Insert(0, list));
        return result;
    }
}
