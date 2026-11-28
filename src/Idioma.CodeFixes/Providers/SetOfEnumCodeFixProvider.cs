using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a set whose <c>ILocalizationSet&lt;TEnum&gt;</c> doesn't match the enums of its contract (<c>LOC206</c>). It
/// lists each missing enum's interface and removes the interface of an enum the contract has no texts of.
/// </summary>
/// <remarks>
/// The fix makes the whole set match, whichever diagnostic it's asked for, as <see cref="SetOfEnumAnalyzer.Plan"/> lays
/// it out. A missing enum takes the place of <c>ILocalizationSet</c>, else of the first wrong enum, else goes in front of
/// the contract.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SetOfEnumCodeFixProvider))]
[Shared]
public sealed class SetOfEnumCodeFixProvider : CodeFixProvider
{
    private const string EquivalenceKey = "MatchSetOfEnum";

    /// <summary>
    /// Changes each class once, though it may have several diagnostics.
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

    private sealed record Fixing(Dictionary<TypeDeclarationSyntax, TypeDeclarationSyntax> Changes, string Title);

    /// <summary>
    /// The types that take the place of one base list entry. None removes it.
    /// </summary>
    private readonly record struct Operation(int Index, SimpleBaseTypeSyntax[] Replacement);

    /// <summary>
    /// What the fix of a diagnostic changes, or <see langword="null"/> when there's nothing to change.
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
        var parts = CodeFixSupport.GetParts(set, cancellationToken)
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

                // An alias names ILocalizationSet itself, so LS<OrderStatus> wouldn't compile after using LS = ...;.
                var setInterface = types[i].Type is IdentifierNameSyntax identifier && model.GetAliasInfo(identifier, cancellationToken) is not null
                    ? NameSetInterface(model, types[i].SpanStart)
                    : WithoutTypeArguments(types[i].Type);
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

        // With nothing to replace, a missing enum goes in front of the contract, or else at the end of the first base list.
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
    /// A part with its base list changed from the last entry to the first, so each earlier index holds.
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
    /// A part without one base list entry, keeping the outer trivia. A part whose only entry goes loses its base list.
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
    /// The index of the first contract of an enum in a part's base list, or -1.
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
    /// The set interface as an entry writes it, without its type argument.
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
    /// <c>ILocalizationSet&lt;TEnum&gt;</c>, naming the enum by its name alone where that binds, else by its full name.
    /// </summary>
    private static SimpleBaseTypeSyntax SetOfEnum(string setInterface, INamedTypeSymbol @enum, SemanticModel semanticModel, int position)
    {
        var enumName = CodeFixSupport.NameAt(semanticModel, position, @enum);

        // Parsed, so that the new entry carries no elastic trivia for the IDE to reformat.
        return SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName($"{setInterface}<{enumName}>"));
    }

    /// <summary>
    /// <c>ILocalizationSet</c> by its name alone where that binds, else by its full name.
    /// </summary>
    private static string NameSetInterface(SemanticModel semanticModel, int position)
    {
        var bindsToSetInterface = CodeFixSupport.BindsAt(semanticModel, position, PackageNames.LocalizationSetName, PackageNames.IsLocalizationSet);
        return bindsToSetInterface ? PackageNames.LocalizationSetName : PackageNames.LocalizationSet;
    }

    private static string Join(IEnumerable<string> types)
    {
        var joined = String.Join(", ", types.Select(static type => $"'{type}'"));
        return joined;
    }
}
