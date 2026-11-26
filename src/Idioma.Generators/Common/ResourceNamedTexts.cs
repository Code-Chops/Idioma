using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Text;

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
/// <para>
/// An IDE asks again on every edit, with a new compilation. Each file is searched once, for the attributes that may name
/// a text, and what it holds is kept with its syntax tree, which an edit of another file leaves as it was. So an edit
/// searches only the file it changes. Only the attributes that may name the text asked for are bound.
/// </para>
/// </remarks>
internal sealed class ResourceNamedTexts
{
    /// <summary>
    /// Per compilation, what was asked of it and the answer.
    /// </summary>
    private static readonly ConditionalWeakTable<Compilation, ResourceNamedTexts> PerCompilation = new();

    /// <summary>
    /// Per file, the attributes that may name a text, found the first time any compilation asks.
    /// </summary>
    // Spans and names only, so the syntax of the file isn't kept alive by this.
    private static readonly ConditionalWeakTable<SyntaxTree, NamingAttributes> PerTree = new();

    private static readonly string[] DisplayNameArguments = ["Name", "ShortName", "Description", "Prompt", "GroupName"];

    private readonly Compilation _compilation;
    // By the full name of the generated class, which no two types of a compilation share.
    private readonly ConcurrentDictionary<(string GeneratedClass, string Text, bool ErrorMessagesOnly), bool> _answers = new();

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
        return this.Names(generatedClass, text, errorMessagesOnly: true, cancellationToken);
    }

    /// <summary>
    /// Whether an attribute names a text of a generated class, as an error message or as a display name.
    /// </summary>
    public bool IsNamed(INamedTypeSymbol generatedClass, string text, CancellationToken cancellationToken)
    {
        return this.Names(generatedClass, text, errorMessagesOnly: false, cancellationToken);
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

    private bool Names(INamedTypeSymbol generatedClass, string text, bool errorMessagesOnly, CancellationToken cancellationToken)
    {
        var question = (generatedClass.ToDisplayString(), text, errorMessagesOnly);

        if (this._answers.TryGetValue(question, out var known))
            return known;

        var names = this.Find(generatedClass, text, errorMessagesOnly, cancellationToken);

        this._answers.TryAdd(question, names);

        return names;
    }

    private bool Find(INamedTypeSymbol generatedClass, string text, bool errorMessagesOnly, CancellationToken cancellationToken)
    {
        foreach (var tree in this._compilation.SyntaxTrees)
        {
            var attributes = GetNamingAttributes(tree, cancellationToken);

            if (attributes.Entries.IsEmpty)
                continue;

            SyntaxNode? root = null;
            SemanticModel? model = null;

            foreach (var entry in attributes.Entries)
            {
                // By the name it gives first, which binds nothing. A name the syntax doesn't give, such as a constant,
                // may be any.
                if ((errorMessagesOnly && !entry.IsErrorMessage) || (entry.Text is not null && !String.Equals(entry.Text, text, StringComparison.Ordinal)))
                    continue;

                root ??= tree.GetRoot(cancellationToken);

                if (root.FindNode(entry.Span).FirstAncestorOrSelf<AttributeSyntax>() is not { } attribute
                    || ReadArguments(attribute) is not { } arguments)
                {
                    continue;
                }

                model ??= this._compilation.GetSemanticModel(tree);

                var (type, names) = entry.IsErrorMessage
                    ? (arguments.ErrorMessageType, arguments.ErrorMessageName is { } errorMessageName ? [errorMessageName] : default(ImmutableArray<ExpressionSyntax>))
                    : (arguments.ResourceType, arguments.DisplayNames);

                if (type is not null && !names.IsDefaultOrEmpty && Binds(type, names, model, generatedClass, text, cancellationToken))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether an attribute's type argument binds to the generated class, and one of the names it gives to the text.
    /// </summary>
    private static bool Binds(TypeSyntax type, ImmutableArray<ExpressionSyntax> names, SemanticModel model, INamedTypeSymbol generatedClass, string text,
        CancellationToken cancellationToken)
    {
        if (!SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(type, cancellationToken).Type, generatedClass))
            return false;

        // A string, or nameof, which the compiler gives as a string too.
        foreach (var name in names)
            if (model.GetConstantValue(name, cancellationToken) is { HasValue: true, Value: string named } && String.Equals(named, text, StringComparison.Ordinal))
                return true;

        return false;
    }

    /// <summary>
    /// The attributes of a file that may name a text, read once per syntax tree.
    /// </summary>
    private static NamingAttributes GetNamingAttributes(SyntaxTree tree, CancellationToken cancellationToken)
    {
        if (PerTree.TryGetValue(tree, out var known))
            return known;

        var found = ReadNamingAttributes(tree, cancellationToken);

        // Another thread may have read it in the meantime, and gives the same.
        try
        {
            PerTree.Add(tree, found);
        }
        catch (ArgumentException)
        {
        }

        return found;
    }

    private static NamingAttributes ReadNamingAttributes(SyntaxTree tree, CancellationToken cancellationToken)
    {
        var entries = ImmutableArray.CreateBuilder<NamingAttribute>();

        // An attribute stands on a declaration or a parameter, so a body need not be walked.
        foreach (var attribute in tree.GetRoot(cancellationToken)
                     .DescendantNodes(static node => node is not (BlockSyntax or ArrowExpressionClauseSyntax or EqualsValueClauseSyntax))
                     .OfType<AttributeSyntax>())
        {
            if (ReadArguments(attribute) is not { } arguments)
                continue;

            if (arguments is { ErrorMessageType: not null, ErrorMessageName: { } errorMessageName })
                entries.Add(new NamingAttribute(attribute.Span, IsErrorMessage: true, ReadNamedText(errorMessageName)));

            if (arguments is { ResourceType: not null, DisplayNames.IsDefaultOrEmpty: false })
                foreach (var displayName in arguments.DisplayNames)
                    entries.Add(new NamingAttribute(attribute.Span, IsErrorMessage: false, ReadNamedText(displayName)));
        }

        return new NamingAttributes(entries.ToImmutable());
    }

    /// <summary>
    /// The arguments of an attribute that name a text, by their names, which binds nothing: nearly every attribute has
    /// none of them. <see langword="null"/> for an attribute without them.
    /// </summary>
    private static ResourceArguments? ReadArguments(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is not { Arguments.Count: > 1 } arguments)
            return null;

        TypeSyntax? errorMessageType = null;
        TypeSyntax? resourceType = null;
        ExpressionSyntax? errorMessageName = null;
        ImmutableArray<ExpressionSyntax>.Builder? displayNames = null;

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
                    (displayNames ??= ImmutableArray.CreateBuilder<ExpressionSyntax>()).Add(argument.Expression);
                    break;
            }
        }

        if ((errorMessageType is null || errorMessageName is null) && (resourceType is null || displayNames is null))
            return null;

        return new ResourceArguments(errorMessageType, errorMessageName, resourceType, displayNames?.ToImmutable() ?? default);
    }

    /// <summary>
    /// The name of a text as the syntax gives it: a string, or the last name in <c>nameof(...)</c>. <see langword="null"/>
    /// where only the compiler can tell, as for a constant.
    /// </summary>
    private static string? ReadNamedText(ExpressionSyntax name)
    {
        var text = name switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }, ArgumentList.Arguments: [{ Expression: var named }] } => named switch
            {
                MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                SimpleNameSyntax simple => simple.Identifier.ValueText,
                _ => null,
            },
            _ => null,
        };

        return text;
    }

    /// <summary>
    /// The arguments of an attribute that name a text: the type and the name of an error message, and the type and
    /// the names of a display name.
    /// </summary>
    private sealed record ResourceArguments(
        TypeSyntax? ErrorMessageType,
        ExpressionSyntax? ErrorMessageName,
        TypeSyntax? ResourceType,
        ImmutableArray<ExpressionSyntax> DisplayNames);

    /// <summary>
    /// The attributes of one file that may name a text.
    /// </summary>
    private sealed class NamingAttributes(ImmutableArray<NamingAttribute> entries)
    {
        public ImmutableArray<NamingAttribute> Entries { get; } = entries;
    }

    /// <summary>
    /// An attribute that may name a text: where it's written, whether it names an error message or a display name, and
    /// the name of the text as the syntax gives it, if it does.
    /// </summary>
    private readonly record struct NamingAttribute(TextSpan Span, bool IsErrorMessage, string? Text);
}
