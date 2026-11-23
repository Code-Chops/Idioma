using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a text of a set that names a parameter differently than its contract does (<c>LOC301</c>). It names every
/// parameter of the method as the contract does, in one of two ways.
/// </summary>
/// <remarks>
/// <para>
/// When the set has the contract's names in another order, only the names in the parameter list change. The body stays
/// as it is, so each use reads the parameter of its name, as its author meant:
/// <c>Invited(string guest, string host) => $"{host} heeft {guest} uitgenodigd"</c> becomes
/// <c>Invited(string host, string guest) => $"{host} heeft {guest} uitgenodigd"</c>. A rename would keep the swap,
/// because it renames the uses too. A <c>&lt;param&gt;</c> doc comment stays as it is, because it describes a parameter
/// by its name.
/// </para>
/// <para>
/// When each name that differs is one the contract doesn't have, as <c>aantal</c> for <c>count</c>, the parameter is
/// renamed along with its uses and its doc comments. The method then words the same values as before.
/// </para>
/// <para>
/// There's no fix when:
/// </para>
/// <list type="bullet">
/// <item>the names mix both cases, as <c>(guest, gast)</c> for <c>(host, guest)</c>, because a use of <c>guest</c>
/// could mean either parameter.</item>
/// <item>a new name already stands in the method, such as a local variable or a field it reads, because the renamed
/// parameter would clash with it or hide it.</item>
/// <item>the method is partial, because the part that holds its body would need the same names.</item>
/// </list>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ParameterNamesCodeFixProvider))]
[Shared]
public sealed class ParameterNamesCodeFixProvider : CodeFixProvider
{
    private const string EquivalenceKey = "NameParametersAsContract";

    private static readonly FixAllProvider FixAll = new ReplaceNodesFixAllProvider<MethodDeclarationSyntax>("Name the parameters of every text as its contract does", EquivalenceKey, Fix);

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
            if (Name(root, semanticModel, diagnostic, context.CancellationToken) is not var (method, named, title))
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    createChangedDocument: _ => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(method, named))),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// The method a diagnostic sits on, with what it becomes. <see langword="null"/> when there's no fix (see the
    /// remarks of this class).
    /// </summary>
    private static Dictionary<MethodDeclarationSyntax, MethodDeclarationSyntax>? Fix(SyntaxNode root, SemanticModel semanticModel, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var fix = Name(root, semanticModel, diagnostic, cancellationToken) is var (method, named, _)
            ? new Dictionary<MethodDeclarationSyntax, MethodDeclarationSyntax> { [method] = named }
            : null;

        return fix;
    }

    /// <summary>
    /// The method a diagnostic sits on, with its parameters named as the contract names them, and the title of that
    /// fix. <see langword="null"/> when there's no fix (see the remarks of this class).
    /// </summary>
    private static (MethodDeclarationSyntax Method, MethodDeclarationSyntax Named, string Title)? Name(SyntaxNode root, SemanticModel semanticModel, Diagnostic diagnostic,
        CancellationToken cancellationToken)
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
            // Only among parameters of one type. Moving a name leaves the body as it is, so each use then reads the
            // parameter of its name. Where the types differ, the body was written for the positions, and the wording
            // would show each value in the place of another.
            var movedTypes = parameters
                .Where((_, index) => !String.Equals(names[index], expected[index], StringComparison.Ordinal))
                .Select(static parameter => parameter.Type?.ToString())
                .Distinct(StringComparer.Ordinal);

            if (movedTypes.Count() > 1)
                return null;

            for (var i = 0; i < parameters.Count; i++)
                renamed.Add(parameters[i].Identifier, expected[i]);

            return (method, Rename(method, renamed), "Put the parameter names in the contract's order");
        }

        // Each parameter whose name differs, with the name of the contract.
        var parametersToRename = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);

        for (var i = 0; i < parameters.Count; i++)
        {
            if (String.Equals(names[i], expected[i], StringComparison.Ordinal))
                continue;

            // A name the contract gives another parameter: a use of it could mean either one.
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

        return (method, Rename(method, renamed), title);
    }

    /// <summary>
    /// Whether a set has the names of the contract, each once, in another order.
    /// </summary>
    private static bool IsOrderOf(string[] names, string[] expected)
    {
        var isOrder = names.OrderBy(static name => name, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(static name => name, StringComparer.Ordinal), StringComparer.Ordinal);
        return isOrder;
    }

    /// <summary>
    /// Whether a name stands in a method already, as the name of a local variable, a parameter, or a member it reads.
    /// The name of an element or attribute of a doc comment, such as <c>&lt;param name="..."&gt;</c>, doesn't count.
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
    /// The parameter of a method that an identifier in it reads, names in <c>nameof</c>, or names in a
    /// <c>&lt;param&gt;</c> or <c>&lt;paramref&gt;</c> doc comment, if any.
    /// </summary>
    private static ISymbol? FindParameter(IdentifierNameSyntax identifier, MethodDeclarationSyntax method, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        // A doc comment by the name it gives, as the compiler reads it.
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
    /// A method with identifiers renamed, each keeping what stood around it. A keyword becomes a verbatim identifier,
    /// as in <c>@event</c>, except in a doc comment, which names it as it is.
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
