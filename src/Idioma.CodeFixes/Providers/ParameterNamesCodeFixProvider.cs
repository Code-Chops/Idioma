using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a text of a set that names a parameter differently than its contract does (<c>LOC401</c>).
/// </summary>
/// <remarks>
/// <para>
/// When the set has the contract's names in another order, only the parameter list changes, so each use in the body
/// reads the parameter of its name, as its author meant. A rename would keep the swap.
/// </para>
/// <para>
/// When each differing name is one the contract doesn't have, as <c>aantal</c> for <c>count</c>, the parameter is
/// renamed with its uses, doc comments and named arguments in the project.
/// </para>
/// <para>
/// There's no fix when:
/// </para>
/// <list type="bullet">
/// <item>the names mix both cases, because a use could mean either parameter.</item>
/// <item>a new name already stands in the method, because the parameter would clash with it or hide it.</item>
/// <item>the method is partial.</item>
/// </list>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ParameterNamesCodeFixProvider))]
[Shared]
public sealed class ParameterNamesCodeFixProvider : CodeFixProvider
{
    private const string EquivalenceKey = "NameParametersAsContract";

    private static readonly FixAllProvider FixAll = new ReplaceNodesFixAllProvider<SyntaxNode>("Name the parameters of every text as its contract does", EquivalenceKey, Fix);

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [TextParametersAnalyzer.ParameterNamesDifferDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => FixAll;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (Name(root, semanticModel, diagnostic, context.CancellationToken) is not var (method, named, title, findCalls))
                continue;

            // The named arguments are looked for once the fix is chosen, because that reads every file of the project,
            // and a light bulb asks for its fixes each time the caret lands on the line.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedSolution: cancellationToken =>
                    {
                        var nodes = new Dictionary<SyntaxNode, SyntaxNode>(findCalls(cancellationToken)) { [method] = named };
                        return CodeFixSupport.ReplaceNodesAsync(document.Project.Solution, nodes.Keys, node => nodes[node], cancellationToken);
                    },
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// The nodes to replace, or <see langword="null"/> when there's no fix.
    /// </summary>
    private static Dictionary<SyntaxNode, SyntaxNode>? Fix(SyntaxNode root, SemanticModel semanticModel, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var fix = Name(root, semanticModel, diagnostic, cancellationToken) is var (method, named, _, findCalls)
            ? new Dictionary<SyntaxNode, SyntaxNode>(findCalls(cancellationToken)) { [method] = named }
            : null;

        return fix;
    }

    /// <summary>
    /// The method with its parameters named as the contract names them, the fix's title, and what finds the renamed
    /// named arguments in the project. <see langword="null"/> when there's no fix.
    /// </summary>
    private static (MethodDeclarationSyntax Method, MethodDeclarationSyntax Named, string Title, Func<CancellationToken, Dictionary<SyntaxNode, SyntaxNode>> FindCalls)? Name(SyntaxNode root,
        SemanticModel semanticModel, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        // The diagnostic sits on the name of a parameter.
        if (!diagnostic.Properties.TryGetValue(TextParametersAnalyzer.ContractParameterNamesProperty, out var value)
            || value is null
            || root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not ParameterSyntax { Parent: ParameterListSyntax { Parent: MethodDeclarationSyntax method } }
            || method.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return null;
        }

        var expected = value.Split(',');
        var parameters = method.ParameterList.Parameters;
        var names = parameters.Select(static parameter => parameter.Identifier.ValueText).ToArray();

        // The method changed since the diagnostic was reported.
        if (names.Length != expected.Length || names.SequenceEqual(expected, StringComparer.Ordinal))
            return null;

        var renamed = new Dictionary<SyntaxToken, string>();

        if (IsOrderOf(names, expected))
        {
            // Only among parameters of one type. Where the types differ, the body was written for the positions.
            var movedTypes = parameters
                .Where((_, index) => !String.Equals(names[index], expected[index], StringComparison.Ordinal))
                .Select(static parameter => parameter.Type?.ToString())
                .Distinct(StringComparer.Ordinal);

            if (movedTypes.Count() > 1)
                return null;

            for (var i = 0; i < parameters.Count; i++)
                renamed.Add(parameters[i].Identifier, expected[i]);

            return (method, Rename(method, renamed), "Put the parameter names in the contract's order", static _ => []);
        }

        var parametersToRename = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);

        for (var i = 0; i < parameters.Count; i++)
        {
            if (String.Equals(names[i], expected[i], StringComparison.Ordinal))
                continue;

            // A name the contract gives another parameter, so a use of it could mean either one.
            if (expected.Contains(names[i], StringComparer.Ordinal)
                || semanticModel.GetDeclaredSymbol(parameters[i], cancellationToken) is not { } parameter)
            {
                return null;
            }

            parametersToRename.Add(parameter, expected[i]);
            renamed.Add(parameters[i].Identifier, expected[i]);
        }

        if (UsesName(method, parametersToRename.Values))
            return null;

        foreach (var identifier in method.DescendantNodes(descendIntoTrivia: true).OfType<IdentifierNameSyntax>())
            if (FindParameter(identifier, method, semanticModel, cancellationToken) is { } parameter && parametersToRename.TryGetValue(parameter, out var name))
                renamed.Add(identifier.Identifier, name);

        var title = parametersToRename.Count is 1
            ? $"Rename '{parametersToRename.Keys.First().Name}' to '{parametersToRename.Values.First()}'"
            : "Rename the parameters as the contract names them";

        var compilation = semanticModel.Compilation;
        return (method, Rename(method, renamed), title, token => RenameNamedArguments(method, parametersToRename, compilation, token));
    }

    /// <summary>
    /// Each named argument outside the method that names one of the parameters, with its new name.
    /// </summary>
    // Only a tree whose text holds an old name is bound. A call in another project gets CS1739 there.
    private static Dictionary<SyntaxNode, SyntaxNode> RenameNamedArguments(MethodDeclarationSyntax method, Dictionary<ISymbol, string> parameters, Compilation compilation,
        CancellationToken cancellationToken)
    {
        var renamed = new Dictionary<SyntaxNode, SyntaxNode>();
        var oldNames = parameters.Keys.Select(static parameter => parameter.Name).ToArray();

        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = tree.GetText(cancellationToken).ToString();

            if (!oldNames.Any(name => text.Contains(name)))
                continue;

            SemanticModel? model = null;

            foreach (var argument in tree.GetRoot(cancellationToken).DescendantNodes().OfType<NameColonSyntax>())
            {
                if (!oldNames.Contains(argument.Name.Identifier.ValueText, StringComparer.Ordinal)
                    || (argument.SyntaxTree == method.SyntaxTree && method.Span.Contains(argument.Span)))
                {
                    continue;
                }

                model ??= compilation.GetSemanticModel(tree);

                if (model.GetSymbolInfo(argument.Name, cancellationToken).Symbol is { } parameter && parameters.TryGetValue(parameter, out var name))
                    renamed.Add(argument.Name, argument.Name.WithIdentifier(SyntaxFactory.Identifier(argument.Name.Identifier.LeadingTrivia, name, argument.Name.Identifier.TrailingTrivia)));
            }
        }

        return renamed;
    }

    private static bool IsOrderOf(string[] names, string[] expected)
    {
        var isOrder = names.OrderBy(static name => name, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(static name => name, StringComparer.Ordinal), StringComparer.Ordinal);
        return isOrder;
    }

    /// <summary>
    /// Whether a name stands in a method already as an identifier, not counting doc comment element and attribute names.
    /// </summary>
    private static bool UsesName(MethodDeclarationSyntax method, IEnumerable<string> names)
    {
        var set = new HashSet<string>(names, StringComparer.Ordinal);

        var usesName = method
            .DescendantTokens(descendIntoTrivia: true)
            .Any(token => token.IsKind(SyntaxKind.IdentifierToken) && token.Parent is not XmlNameSyntax && set.Contains(token.ValueText));

        return usesName;
    }

    /// <summary>
    /// The parameter an identifier refers to, also in <c>nameof</c> or a <c>&lt;param&gt;</c> or <c>&lt;paramref&gt;</c>.
    /// </summary>
    private static ISymbol? FindParameter(IdentifierNameSyntax identifier, MethodDeclarationSyntax method, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (identifier.Parent is XmlNameAttributeSyntax attribute)
        {
            var element = attribute.Parent switch
            {
                XmlElementStartTagSyntax start => start.Name.LocalName.ValueText,
                XmlEmptyElementSyntax empty => empty.Name.LocalName.ValueText,
                _ => null,
            };

            if (element is not ("param" or "paramref"))
                return null;

            var parameter = method.ParameterList.Parameters.FirstOrDefault(parameter => String.Equals(parameter.Identifier.ValueText, identifier.Identifier.ValueText, StringComparison.Ordinal));
            return parameter is null ? null : semanticModel.GetDeclaredSymbol(parameter, cancellationToken);
        }

        var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
        return symbol is IParameterSymbol ? symbol : null;
    }

    /// <summary>
    /// A method with identifiers renamed. A keyword becomes a verbatim identifier, except in a doc comment.
    /// </summary>
    private static MethodDeclarationSyntax Rename(MethodDeclarationSyntax method, Dictionary<SyntaxToken, string> renamed)
    {
        var result = method.ReplaceTokens(renamed.Keys, (original, _) =>
        {
            var name = renamed[original];

            var identifier = SyntaxFacts.GetKeywordKind(name) is SyntaxKind.None || original.Parent?.Parent is XmlNameAttributeSyntax
                ? SyntaxFactory.Identifier(original.LeadingTrivia, name, original.TrailingTrivia)
                : SyntaxFactory.VerbatimIdentifier(original.LeadingTrivia, $"@{name}", name, original.TrailingTrivia);

            return identifier;
        });

        return result;
    }
}
