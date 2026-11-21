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
/// implements the contract. A helper of a set with the text's name, such as a private overload, stays as it is. A set
/// that uses the value as a string, as in <c>button.Length</c>, stops compiling, which marks what to change by hand.
/// </para>
/// <para>
/// A call that passes a value, such as a customer's name, still compiles, because a string converts to a text. A call
/// that passes wording written in the source becomes build error <c>LOC500</c>, because the text would show that
/// wording in one language.
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
            || FindContractText(text, compilation) is not { } contractText)
        {
            return null;
        }

        var parameters = new Dictionary<ParameterSyntax, ParameterSyntax>();

        foreach (var set in CodeFixSupport.GetClasses(compilation.Assembly.GlobalNamespace))
        {
            if (!set.AllInterfaces.Contains(contractText.ContainingType, SymbolEqualityComparer.Default))
                continue;

            foreach (var method in set.GetMembers().OfType<IMethodSymbol>())
            {
                if (!IsText(method, set, contractText))
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
    /// The method of the contract that a text of a generated class calls, such as <c>IShopLocalization.Greeting</c> for
    /// <c>ShopLocalization.Greeting</c>, when this assembly generates it. A contract of another assembly doesn't change
    /// with the sets here.
    /// </summary>
    private static IMethodSymbol? FindContractText(IMethodSymbol text, Compilation compilation)
    {
        if (text.ContainingType is not { ContainingType: null } texts
            || !SymbolEqualityComparer.Default.Equals(texts.ContainingAssembly, compilation.Assembly))
        {
            return null;
        }

        foreach (var contract in texts.ContainingNamespace.GetTypeMembers($"I{texts.Name}"))
        {
            if (!PackageNames.IsContract(contract))
                continue;

            foreach (var member in contract.GetMembers(text.Name))
                if (member is IMethodSymbol method && method.Parameters.Length == text.Parameters.Length)
                    return method;
        }

        return null;
    }

    /// <summary>
    /// Whether a method of a set words the text: it implements the contract's method, explicitly or as the method the
    /// set implements it through, or it overrides that method. A helper with the text's name and as many parameters,
    /// such as a private overload, doesn't.
    /// </summary>
    private static bool IsText(IMethodSymbol method, INamedTypeSymbol set, IMethodSymbol contractText)
    {
        if (!method.ExplicitInterfaceImplementations.IsEmpty)
            return method.ExplicitInterfaceImplementations.Contains(contractText, SymbolEqualityComparer.Default);

        // Asked of the set, and not told by the name. A set that derives from another implements the text through the
        // method of that set, which its own method overrides.
        var implementation = set.FindImplementationForInterfaceMember(contractText);

        for (var current = method; current is not null; current = current.OverriddenMethod)
            if (SymbolEqualityComparer.Default.Equals(current, implementation))
                return true;

        return false;
    }
}
