using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Operations;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a text passed as a <see cref="string"/> value to a text of a set (<c>LOC303</c>). It gives the parameter the
/// type <c>LocalizableText</c> in every set of this assembly: <c>PressToPay(string button)</c> becomes
/// <c>PressToPay(LocalizableText button)</c>. The text then stays a text, and shows in the language of the wording
/// around it.
/// </summary>
/// <remarks>
/// <para>
/// Every set changes at once, also one that overrides the text or implements it explicitly, so that each still
/// implements the contract. A call that passes a string still compiles, because a string converts to a text. A set
/// that uses the value as a string, as in <c>button.Length</c>, stops compiling, which marks what to change by hand.
/// </para>
/// <para>
/// There's no fix for a text of another assembly's contract, because a set can't change it.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TextAsStringCodeFixProvider))]
[Shared]
public sealed class TextAsStringCodeFixProvider : CodeFixProvider
{
    private const string EquivalenceKey = "MakeParameterLocalizableText";

    private static readonly FixAllProvider FixAll = new ReplaceNodesFixAllProvider<ParameterSyntax>("Make the parameters that take a text a LocalizableText", EquivalenceKey, Fix);

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [TextAsStringAnalyzer.TextAsStringDiagnosticId];

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
            if (Fix(root, semanticModel, diagnostic, context.CancellationToken) is not { } parameters)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Make '{parameters.Keys.First().Identifier.ValueText}' a LocalizableText in every set",
                    createChangedSolution: cancellationToken => CodeFixSupport.ReplaceNodesAsync(document.Project.Solution, parameters.Keys, parameter => parameters[parameter], cancellationToken),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// The parameter a diagnostic's text is passed to, as each set of this assembly declares it, with what it becomes.
    /// <see langword="null"/> when there's no fix (see the remarks of this class).
    /// </summary>
    private static Dictionary<ParameterSyntax, ParameterSyntax>? Fix(SyntaxNode root, SemanticModel semanticModel, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        var compilation = semanticModel.Compilation;

        // The diagnostic sits on the text, which is the value of an argument.
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true).FirstAncestorOrSelf<ArgumentSyntax>() is not { } argument
            || semanticModel.GetOperation(argument, cancellationToken) is not IArgumentOperation { Parameter: { } parameter, Parent: IInvocationOperation { TargetMethod: var text } }
            || compilation.GetTypeByMetadataName(PackageNames.LocalizableText) is not { } localizableText
            || FindContract(text, compilation) is not { } contract)
        {
            return null;
        }

        var parameters = new Dictionary<ParameterSyntax, ParameterSyntax>();

        foreach (var set in CodeFixSupport.GetClasses(compilation.Assembly.GlobalNamespace))
        {
            if (!set.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default))
                continue;

            foreach (var method in set.GetMembers().OfType<IMethodSymbol>())
            {
                if (!IsText(method, text, contract))
                    continue;

                foreach (var reference in method.DeclaringSyntaxReferences)
                {
                    if (reference.GetSyntax(cancellationToken) is not MethodDeclarationSyntax declaration
                        || declaration.ParameterList.Parameters[parameter.Ordinal] is not { Type: { } type } declared
                        || parameters.ContainsKey(declared))
                    {
                        continue;
                    }

                    // By the name the file of the set reaches the type with, and as nullable as the string was.
                    var name = localizableText.ToMinimalDisplayString(compilation.GetSemanticModel(declaration.SyntaxTree), type.SpanStart);
                    var written = type is NullableTypeSyntax ? $"{name}?" : name;

                    parameters.Add(declared, declared.WithType(SyntaxFactory.ParseTypeName(written).WithTriviaFrom(type)));
                }
            }
        }

        return parameters.Count is 0 ? null : parameters;
    }

    /// <summary>
    /// The contract of a text of a generated class, such as <c>IShopLocalization</c> for
    /// <c>ShopLocalization.Greeting</c>, when this assembly generates it. A contract of another assembly doesn't change
    /// with the sets here.
    /// </summary>
    private static INamedTypeSymbol? FindContract(IMethodSymbol text, Compilation compilation)
    {
        if (text.ContainingType is not { ContainingType: null } texts
            || !SymbolEqualityComparer.Default.Equals(texts.ContainingAssembly, compilation.Assembly))
        {
            return null;
        }

        foreach (var contract in texts.ContainingNamespace.GetTypeMembers($"I{texts.Name}"))
            if (PackageNames.IsContract(contract) && contract.GetMembers(text.Name).OfType<IMethodSymbol>().Any())
                return contract;

        return null;
    }

    /// <summary>
    /// Whether a method of a set words a text: it has the text's name and as many parameters, or implements the
    /// contract's method of that name explicitly.
    /// </summary>
    private static bool IsText(IMethodSymbol method, IMethodSymbol text, INamedTypeSymbol contract)
    {
        if (method.Parameters.Length != text.Parameters.Length)
            return false;

        var isText = String.Equals(method.Name, text.Name, StringComparison.Ordinal)
                     || method.ExplicitInterfaceImplementations.Any(implemented => String.Equals(implemented.Name, text.Name, StringComparison.Ordinal)
                                                                                   && SymbolEqualityComparer.Default.Equals(implemented.ContainingType, contract));

        return isText;
    }
}
