using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// The texts of generated classes that attributes name as a resource, as .NET's validation and display attributes do:
/// <c>[Required(ErrorMessageResourceName = nameof(ShopLocalization.NameRequired), ErrorMessageResourceType = typeof(ShopLocalization))]</c>,
/// or <c>[Display(Name = nameof(ShopLocalization.Name), ResourceType = typeof(ShopLocalization))]</c>. Such an attribute
/// reads the text at run time as a static property, by its name.
/// </summary>
/// <remarks>
/// <para>
/// A text named as an error message (<c>ErrorMessageResourceName</c>) is a format: the attribute fills in <c>{0}</c>
/// with the name of the field, so <c>"Enter {0}"</c> shows <c>"Enter Name"</c>. That placeholder is no mistake
/// (<c>LOC403</c>). A display name (<c>Name</c>, <c>ShortName</c>, <c>Description</c>, <c>Prompt</c>,
/// <c>GroupName</c>) is shown as it's written.
/// </para>
/// <para>
/// Read by the arguments' names, so any attribute that follows .NET's convention counts. The name of the text may be
/// written as <see langword="nameof"/> or as a string.
/// </para>
/// </remarks>
internal sealed class ResourceNamedTexts
{
    /// <summary>
    /// Per compilation, what its attributes name, read the first time it's asked for.
    /// </summary>
    // An analyzer asks only for a text with a placeholder, which few have, so most compilations are never read.
    private static readonly ConditionalWeakTable<Compilation, ResourceNamedTexts> PerCompilation = new();

    private static readonly string[] DisplayNameArguments = ["Name", "ShortName", "Description", "Prompt", "GroupName"];

    private readonly Compilation _compilation;
    private Names? _names;

    private ResourceNamedTexts(Compilation compilation)
    {
        this._compilation = compilation;
    }

    public static ResourceNamedTexts Of(Compilation compilation)
    {
        return PerCompilation.GetValue(compilation, static compilation => new ResourceNamedTexts(compilation));
    }

    /// <summary>
    /// Whether an attribute names a text of a generated class as its error message, which it formats.
    /// </summary>
    public bool IsErrorMessage(INamedTypeSymbol generatedClass, string text, CancellationToken cancellationToken)
    {
        var isErrorMessage = this.Read(cancellationToken).ErrorMessages.TryGetValue(generatedClass, out var texts) && texts.Contains(text);
        return isErrorMessage;
    }

    /// <summary>
    /// Whether an attribute names a text of a generated class, as an error message or as a display name.
    /// </summary>
    public bool IsNamed(INamedTypeSymbol generatedClass, string text, CancellationToken cancellationToken)
    {
        var isNamed = this.Read(cancellationToken).All.TryGetValue(generatedClass, out var texts) && texts.Contains(text);
        return isNamed;
    }

    /// <summary>
    /// The generated class whose text a member of a set implements, as an attribute names it: <c>ShopLocalization</c>
    /// for a text of <c>IShopLocalization</c>. <see langword="null"/> for a contract written by hand, which has none.
    /// </summary>
    public static INamedTypeSymbol? FindGeneratedClass(ISymbol contractText)
    {
        var generatedClass = contractText.ContainingType is { Name: ['I', _, ..] } contract
            ? contract.ContainingNamespace.GetTypeMembers(contract.Name.Substring(1)).FirstOrDefault(static type => type is { IsStatic: true, Arity: 0 })
            : null;

        return generatedClass;
    }

    private Names Read(CancellationToken cancellationToken)
    {
        // Two threads may each read it, and one of them is kept.
        if (Volatile.Read(ref this._names) is { } names)
            return names;

        Interlocked.CompareExchange(ref this._names, ReadNames(this._compilation, cancellationToken), comparand: null);
        return this._names!;
    }

    private static Names ReadNames(Compilation compilation, CancellationToken cancellationToken)
    {
        var all = new Dictionary<INamedTypeSymbol, HashSet<string>>(SymbolEqualityComparer.Default);
        var errorMessages = new Dictionary<INamedTypeSymbol, HashSet<string>>(SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            SemanticModel? model = null;

            // An attribute stands on a declaration or a parameter, so a body need not be walked.
            foreach (var attribute in tree.GetRoot(cancellationToken)
                         .DescendantNodes(static node => node is not (BlockSyntax or ArrowExpressionClauseSyntax or EqualsValueClauseSyntax))
                         .OfType<AttributeSyntax>())
            {
                if (attribute.ArgumentList is not { Arguments.Count: > 1 } arguments)
                    continue;

                // By the names of its arguments first, which binds nothing: nearly every attribute has none of them.
                TypeSyntax? errorMessageType = null;
                TypeSyntax? resourceType = null;
                ExpressionSyntax? errorMessageName = null;
                List<ExpressionSyntax>? displayNames = null;

                foreach (var argument in arguments.Arguments)
                {
                    switch (argument.NameEquals?.Name.Identifier.ValueText)
                    {
                        case "ErrorMessageResourceType" when argument.Expression is TypeOfExpressionSyntax typeOf:
                            errorMessageType = typeOf.Type;
                            break;
                        case "ResourceType" when argument.Expression is TypeOfExpressionSyntax typeOf:
                            resourceType = typeOf.Type;
                            break;
                        case "ErrorMessageResourceName":
                            errorMessageName = argument.Expression;
                            break;
                        case { } name when DisplayNameArguments.Contains(name, StringComparer.Ordinal):
                            (displayNames ??= []).Add(argument.Expression);
                            break;
                    }
                }

                if (errorMessageType is not null && errorMessageName is not null)
                {
                    model ??= compilation.GetSemanticModel(tree);
                    Add(errorMessageType, errorMessageName, model, errorMessages, all, cancellationToken);
                }

                if (resourceType is not null && displayNames is not null)
                {
                    model ??= compilation.GetSemanticModel(tree);

                    foreach (var displayName in displayNames)
                        Add(resourceType, displayName, model, errorMessages: null, all, cancellationToken);
                }
            }
        }

        return new Names(all, errorMessages);

        static void Add(TypeSyntax type, ExpressionSyntax name, SemanticModel model, Dictionary<INamedTypeSymbol, HashSet<string>>? errorMessages,
            Dictionary<INamedTypeSymbol, HashSet<string>> all, CancellationToken cancellationToken)
        {
            // A string, or nameof, which the compiler gives as a string too.
            if (model.GetTypeInfo(type, cancellationToken).Type is not INamedTypeSymbol named
                || model.GetConstantValue(name, cancellationToken) is not { HasValue: true, Value: string text })
            {
                return;
            }

            AddTo(all, named, text);

            if (errorMessages is not null)
                AddTo(errorMessages, named, text);
        }

        static void AddTo(Dictionary<INamedTypeSymbol, HashSet<string>> texts, INamedTypeSymbol type, string text)
        {
            if (!texts.TryGetValue(type, out var names))
                texts.Add(type, names = new HashSet<string>(StringComparer.Ordinal));

            names.Add(text);
        }
    }

    /// <summary>
    /// Per generated class, the names of the texts attributes name, and of those named as an error message.
    /// </summary>
    private sealed class Names(Dictionary<INamedTypeSymbol, HashSet<string>> all, Dictionary<INamedTypeSymbol, HashSet<string>> errorMessages)
    {
        public Dictionary<INamedTypeSymbol, HashSet<string>> All { get; } = all;

        public Dictionary<INamedTypeSymbol, HashSet<string>> ErrorMessages { get; } = errorMessages;
    }
}
