using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a set whose <c>ILocalizationSet&lt;TEnum&gt;</c> doesn't match the enums of its contract (<c>LOC205</c>). It
/// lists <c>ILocalizationSet&lt;OrderStatus&gt;</c> in place of <c>ILocalizationSet</c>, as in
/// <c>class OrderStatusLocalizationNl : ILocalizationSet&lt;OrderStatus&gt;, IOrderStatusLocalization</c>, and it removes
/// the interface of an enum the contract has no texts of.
/// </summary>
/// <remarks>
/// <para>
/// The fix makes the whole set match, whichever diagnostic it's asked for, as <see cref="SetOfEnumAnalyzer.Plan"/>
/// lays it out:
/// </para>
/// <list type="bullet">
/// <item>A missing enum takes the place of <c>ILocalizationSet</c>, or else of the first wrong enum, or else goes in
/// front of the contract. A set of two enums gets one interface for each.</item>
/// <item>Every other wrong enum goes. Where the set would then implement <c>ILocalizationSet</c> no more, its first
/// wrong enum becomes <c>ILocalizationSet</c>.</item>
/// </list>
/// <para>
/// <c>ILocalizationSet</c> keeps the way it's written, such as by its full name. The enum is named by its name alone
/// where the file can name it so, and else by its full name.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SetOfEnumCodeFixProvider))]
[Shared]
public sealed class SetOfEnumCodeFixProvider : CodeFixProvider
{
    private const string EquivalenceKey = "MatchSetOfEnum";

    /// <summary>
    /// Applies the fix to every diagnostic at once. Each class changes once, though it may have a diagnostic for a
    /// missing enum and one for each wrong one.
    /// </summary>
    private static readonly FixAllProvider FixAll = new ReplaceNodesFixAllProvider<TypeDeclarationSyntax>(
        "Match the enums of every set to its contract",
        EquivalenceKey,
        static (root, semanticModel, diagnostic, cancellationToken) => Find(root, semanticModel, diagnostic, cancellationToken)?.Changes);

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [SetOfEnumAnalyzer.SetOfEnumDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => FixAll;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null)
            return;

        var solution = document.Project.Solution;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (Find(root, semanticModel, diagnostic, context.CancellationToken) is not { } fixing)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    fixing.Title,
                    createChangedSolution: cancellationToken => CodeFixSupport.ReplaceNodesAsync(solution, fixing.Changes.Keys, node => fixing.Changes[node], cancellationToken),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// The parts of a set whose base lists change, each with what it becomes, and the title of the fix.
    /// </summary>
    private sealed record Fixing(Dictionary<TypeDeclarationSyntax, TypeDeclarationSyntax> Changes, string Title);

    /// <summary>
    /// What one entry of a base list becomes: the types in its place, none to remove it.
    /// </summary>
    private readonly record struct Operation(int Index, SimpleBaseTypeSyntax[] Replacement);

    /// <summary>
    /// What the fix of a diagnostic changes. <see langword="null"/> when the code changed since the diagnostic was
    /// reported, or when no part of the set has a base list to change.
    /// </summary>
    private static Fixing? Find(SyntaxNode root, SemanticModel semanticModel, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaration
            || semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not { } set
            || SetOfEnumAnalyzer.Plan(set, semanticModel.Compilation) is not { } plan)
        {
            return null;
        }

        var compilation = semanticModel.Compilation;
        var parts = set.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<TypeDeclarationSyntax>()
            .Where(static part => part.BaseList is not null)
            .ToArray();

        var changes = new Dictionary<TypeDeclarationSyntax, TypeDeclarationSyntax>();
        var removed = new List<string>();
        var added = new List<string>();
        var placed = plan.Missing.IsEmpty;

        foreach (var part in parts)
        {
            var model = compilation.GetSemanticModel(part.SyntaxTree);
            var types = part.BaseList!.Types;
            var operations = new List<Operation>();

            for (var i = 0; i < types.Count; i++)
            {
                if (model.GetTypeInfo(types[i].Type, cancellationToken).Type is not INamedTypeSymbol listed)
                    continue;

                var setInterface = WithoutTypeArguments(types[i].Type);
                SimpleBaseTypeSyntax[]? replacement = null;

                if (!placed && SymbolEqualityComparer.Default.Equals(listed, plan.Replaced))
                {
                    replacement = [.. plan.Missing.Select(@enum => SetOfEnum(setInterface, @enum, model, types[i].SpanStart))];
                    placed = true;
                }
                else if (plan.Extras.Contains(listed, SymbolEqualityComparer.Default))
                {
                    replacement = plan.IsReplacedBySetInterface((INamedTypeSymbol)listed.TypeArguments[0])
                        ? [SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(setInterface))]
                        : [];
                }

                if (replacement is null)
                    continue;

                operations.Add(new Operation(i, replacement));
                removed.Add(types[i].Type.WithoutTrivia().ToString());
                added.AddRange(replacement.Select(static type => type.Type.ToString()));
            }

            if (operations.Count > 0)
                changes[part] = Apply(part, operations);
        }

        // Nothing for a missing enum to take the place of: in front of the contract, in the part that lists it, or else
        // at the end of the first base list. No part has changed then, as the set lists no wrong enum.
        if (!placed && parts.Length > 0)
        {
            var (part, index) = parts
                .Select(candidate => (Part: candidate, Index: IndexOfContract(candidate, compilation.GetSemanticModel(candidate.SyntaxTree), cancellationToken)))
                .FirstOrDefault(static candidate => candidate.Index >= 0);

            if (part is null)
                (part, index) = (parts[0], parts[0].BaseList!.Types.Count);

            var model = compilation.GetSemanticModel(part.SyntaxTree);
            var setInterface = NameSetInterface(model, part.BaseList!.SpanStart);
            var inserted = plan.Missing.Select(@enum => SetOfEnum(setInterface, @enum, model, part.BaseList.SpanStart)).ToArray();

            changes[part] = Apply(part, [new Operation(index, inserted)], insert: true);
            added.AddRange(inserted.Select(static type => type.Type.ToString()));
        }

        if (changes.Count is 0)
            return null;

        var title = (removed.Count, added.Count) switch
        {
            (0, _) => $"List {Join(added)}",
            (_, 0) => $"Remove {Join(removed)}",
            _ => $"Replace {Join(removed)} with {Join(added)}",
        };

        return new Fixing(changes, title);
    }

    /// <summary>
    /// A part with its base list changed, entry by entry, from the last to the first, so that the index of each earlier
    /// entry holds. An insertion puts its types in front of the entry at its index.
    /// </summary>
    private static TypeDeclarationSyntax Apply(TypeDeclarationSyntax part, IEnumerable<Operation> operations, bool insert = false)
    {
        var result = part;

        foreach (var (index, replacement) in operations.OrderByDescending(static operation => operation.Index))
        {
            if (insert)
            {
                for (var i = 0; i < replacement.Length; i++)
                    result = CodeFixSupport.InsertBaseType(result, index + i, replacement[i]);
            }
            else if (replacement.Length is 0)
            {
                result = Remove(result, index);
            }
            else
            {
                var types = result.BaseList!.Types;
                result = result.ReplaceNode(types[index], replacement[0].WithTriviaFrom(types[index]));

                for (var i = 1; i < replacement.Length; i++)
                    result = CodeFixSupport.InsertBaseType(result, index + i, replacement[i]);
            }
        }

        return result;
    }

    /// <summary>
    /// A part without the entry of its base list at an index, along with a comma. What stood before the first entry, or
    /// after the last, such as a comment, goes to the entry that takes its place. A part whose only entry goes loses
    /// its base list, as a part of a partial class does that lists nothing but a wrong enum.
    /// </summary>
    private static TypeDeclarationSyntax Remove(TypeDeclarationSyntax part, int index)
    {
        var baseList = part.BaseList!;
        var types = baseList.Types;

        if (types.Count is 1)
            return CodeFixSupport.RemoveBaseList(part);

        var removed = types[index];
        var remaining = types.RemoveAt(index);

        if (index == types.Count - 1)
            remaining = remaining.Replace(remaining[^1], remaining[^1].WithTrailingTrivia(removed.GetTrailingTrivia()));

        if (index is 0)
            remaining = remaining.Replace(remaining[0], remaining[0].WithLeadingTrivia(removed.GetLeadingTrivia()));

        return part.WithBaseList(baseList.WithTypes(remaining));
    }

    /// <summary>
    /// The index of the first contract of an enum in the base list of a part, or -1 when it lists none.
    /// </summary>
    private static int IndexOfContract(TypeDeclarationSyntax part, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var types = part.BaseList!.Types;

        for (var i = 0; i < types.Count; i++)
        {
            if (semanticModel.GetTypeInfo(types[i].Type, cancellationToken).Type is INamedTypeSymbol listed
                && PackageNames.GetContractEnums(listed).Length > 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The set interface as an entry writes it, without its type argument: <c>ILocalizationSet</c> for
    /// <c>ILocalizationSet&lt;PaymentStatus&gt;</c>, and <c>CodeChops.Idioma.ILocalizationSet</c> for that name.
    /// </summary>
    private static string WithoutTypeArguments(TypeSyntax type)
    {
        var written = type switch
        {
            GenericNameSyntax generic => generic.Identifier.ValueText,
            QualifiedNameSyntax { Right: GenericNameSyntax generic } qualified => $"{qualified.Left.WithoutTrivia()}.{generic.Identifier.ValueText}",
            AliasQualifiedNameSyntax { Name: GenericNameSyntax generic } aliased => $"{aliased.Alias.Identifier.ValueText}::{generic.Identifier.ValueText}",
            _ => type.WithoutTrivia().ToString(),
        };

        return written;
    }

    /// <summary>
    /// <c>ILocalizationSet&lt;TEnum&gt;</c>, with the set interface written as given and the enum as the code at a
    /// position can name it: by its name alone where that binds to the enum, and else by its full name.
    /// </summary>
    private static SimpleBaseTypeSyntax SetOfEnum(string setInterface, INamedTypeSymbol @enum, SemanticModel semanticModel, int position)
    {
        var simpleName = SyntaxFactory.IdentifierName(@enum.Name);

        var bindsToEnum = SymbolEqualityComparer.Default.Equals(
            semanticModel.GetSpeculativeTypeInfo(position, simpleName, SpeculativeBindingOption.BindAsTypeOrNamespace).Type,
            @enum);

        var enumName = bindsToEnum ? @enum.Name : @enum.ToDisplayString();

        // Parsed, so that the new entry carries no elastic trivia for the IDE to reformat.
        return SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName($"{setInterface}<{enumName}>"));
    }

    /// <summary>
    /// <c>ILocalizationSet</c> as the code at a position can name it: by its name alone where that binds to the
    /// interface, and else by its full name.
    /// </summary>
    private static string NameSetInterface(SemanticModel semanticModel, int position)
    {
        var simpleName = SyntaxFactory.IdentifierName(PackageNames.LocalizationSetName);

        var bindsToSetInterface = semanticModel.GetSpeculativeTypeInfo(position, simpleName, SpeculativeBindingOption.BindAsTypeOrNamespace).Type is INamedTypeSymbol bound
                                  && PackageNames.IsLocalizationSet(bound);

        return bindsToSetInterface ? PackageNames.LocalizationSetName : PackageNames.LocalizationSet;
    }

    private static string Join(IEnumerable<string> types)
    {
        var joined = String.Join(", ", types.Select(static type => $"'{type}'"));
        return joined;
    }
}
