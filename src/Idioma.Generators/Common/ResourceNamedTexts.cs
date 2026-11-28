using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Text;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// The texts of generated classes that attributes name as a resource, as .NET's validation and display attributes do:
/// <c>[Display(Name = nameof(ShopLocalization.Name), ResourceType = typeof(ShopLocalization))]</c>.
/// </summary>
/// <remarks>
/// <para>
/// A text named as an error message is a format whose <c>{0}</c> the attribute fills in, so it's no mistake
/// (<c>LOC403</c>). A display name is shown as written.
/// </para>
/// <para>
/// Each file is searched once and kept with its syntax tree, so an edit searches only the file it changes.
/// </para>
/// </remarks>
internal sealed class ResourceNamedTexts
{
    private static readonly ConditionalWeakTable<Compilation, ResourceNamedTexts> PerCompilation = new();

    // Spans and names only, so this doesn't keep the file's syntax alive.
    private static readonly ConditionalWeakTable<SyntaxTree, NamingAttributes> PerTree = new();

    private static readonly string[] DisplayNameArguments = ["Name", "ShortName", "Description", "Prompt", "GroupName"];

    private readonly Compilation _compilation;
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
    /// <c>ShopLocalization</c> for a text of <c>IShopLocalization</c>, or <see langword="null"/> for a contract written
    /// by hand.
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
                // Filtered by name first, which binds nothing. A name only the compiler knows may be any.
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

        foreach (var name in names)
            if (model.GetConstantValue(name, cancellationToken) is { HasValue: true, Value: string named } && String.Equals(named, text, StringComparison.Ordinal))
                return true;

        return false;
    }

    private static NamingAttributes GetNamingAttributes(SyntaxTree tree, CancellationToken cancellationToken)
    {
        if (PerTree.TryGetValue(tree, out var known))
            return known;

        var found = ReadNamingAttributes(tree, cancellationToken);

        // Another thread may have added the same in the meantime.
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
    /// The arguments of an attribute that name a text, read by name without binding. <see langword="null"/> without
    /// them.
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
    /// A string, or the last name in <c>nameof(...)</c>. <see langword="null"/> where only the compiler can tell.
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

    private sealed record ResourceArguments(
        TypeSyntax? ErrorMessageType,
        ExpressionSyntax? ErrorMessageName,
        TypeSyntax? ResourceType,
        ImmutableArray<ExpressionSyntax> DisplayNames);

    private sealed class NamingAttributes(ImmutableArray<NamingAttribute> entries)
    {
        public ImmutableArray<NamingAttribute> Entries { get; } = entries;
    }

    /// <summary>
    /// An attribute that may name a text, with the text's name if the syntax gives it.
    /// </summary>
    private readonly record struct NamingAttribute(TextSpan Span, bool IsErrorMessage, string? Text);
}
