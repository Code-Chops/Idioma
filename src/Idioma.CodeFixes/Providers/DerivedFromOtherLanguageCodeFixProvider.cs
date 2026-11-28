using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a set that derives from a set of another language (<c>LOC204</c>) by implementing the contract instead:
/// <c>class ShopLocalizationNl : ShopLocalizationEn</c> becomes
/// <c>class ShopLocalizationNl : ILocalizationSet, IShopLocalization</c>. The compiler then names each missing text.
/// </summary>
/// <remarks>
/// <see langword="override"/> becomes <see langword="virtual"/> (or goes, where sealed) and <see langword="new"/> goes,
/// except on overrides of <see cref="object"/>'s members. A region that derives from another region also gets a fix
/// that derives from the set of its language further up, such as
/// <c>ShopLocalizationEn</c> for <c>ShopLocalizationEnUs : ShopLocalizationEnGb</c>.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DerivedFromOtherLanguageCodeFixProvider))]
[Shared]
public sealed class DerivedFromOtherLanguageCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [LocalizationSetsPart.DerivedFromOtherLanguageDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;

        if (await CodeFixSupport.FindDeclarationAsync(context).ConfigureAwait(false) is not { } declaration
            || await CodeFixSupport.FindTypeAsync(document, declaration, context.CancellationToken).ConfigureAwait(false) is not var (type, semanticModel)
            || type.BaseType is not { } baseSet)
        {
            return;
        }

        var compilation = semanticModel.Compilation;
        var parts = await CodeFixSupport.GetPartsAsync(type, context.CancellationToken).ConfigureAwait(false);

        // Both fixes change the part that lists the base set, so without one there's nothing to fix.
        if (!parts.Any(part => ListsBaseSet(part, compilation.GetSemanticModel(part.SyntaxTree), baseSet, context.CancellationToken)))
            return;

        if (FindSetOfOwnLanguage(type, baseSet, compilation, context.CancellationToken) is { } setOfOwnLanguage)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Derive from '{setOfOwnLanguage.Name}' instead",
                    createChangedSolution: cancellationToken => DeriveAsync(document.Project.Solution, type, baseSet, setOfOwnLanguage, compilation, cancellationToken),
                    equivalenceKey: "DeriveFromSetOfOwnLanguage"),
                context.Diagnostics);
        }

        var contracts = baseSet.AllInterfaces.Where(PackageNames.IsContract).ToImmutableArray();

        if (contracts.IsEmpty || compilation.GetTypeByMetadataName(PackageNames.LocalizationSet) is not { } localizationSet)
            return;

        // A set of an enum lists ILocalizationSet<TEnum> for each enum its base set lists (LOC206).
        var setsOfEnums = baseSet.AllInterfaces.Where(PackageNames.IsLocalizationSetOfEnum).ToImmutableArray();
        ImmutableArray<INamedTypeSymbol> setInterfaces = setsOfEnums.IsEmpty ? [localizationSet] : setsOfEnums;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Implement '{String.Join("' and '", contracts.Select(static contract => contract.Name))}' instead",
                createChangedSolution: cancellationToken => ImplementAsync(document.Project.Solution, type, baseSet, [.. setInterfaces, .. contracts], compilation, cancellationToken),
                equivalenceKey: "ImplementContractInstead"),
            context.Diagnostics);
    }

    /// <summary>
    /// The nearest ancestor of the base set that serves the set's language, or <see langword="null"/>.
    /// </summary>
    private static INamedTypeSymbol? FindSetOfOwnLanguage(INamedTypeSymbol type, INamedTypeSymbol baseSet, Compilation compilation, CancellationToken cancellationToken)
    {
        if (LocalizationDiscovery.ReadLanguage(type, compilation, cancellationToken) is not { } language)
            return null;

        for (var ancestor = baseSet.BaseType; ancestor is { SpecialType: not SpecialType.System_Object }; ancestor = ancestor.BaseType)
        {
            if (ancestor.AllInterfaces.Any(PackageNames.IsLocalizationSet)
                && LocalizationDiscovery.ReadLanguage(ancestor, compilation, cancellationToken) is { } ancestorLanguage
                && SupportedLanguages.Serves(ancestorLanguage, language))
            {
                return ancestor;
            }
        }

        return null;
    }

    private static async Task<Solution> DeriveAsync(Solution solution, INamedTypeSymbol type, INamedTypeSymbol baseSet, INamedTypeSymbol setOfOwnLanguage,
        Compilation compilation, CancellationToken cancellationToken)
    {
        foreach (var part in await CodeFixSupport.GetPartsAsync(type, cancellationToken).ConfigureAwait(false))
        {
            var partModel = compilation.GetSemanticModel(part.SyntaxTree);

            if (!ListsBaseSet(part, partModel, baseSet, cancellationToken))
                continue;

            var listed = part.BaseList!.Types[0];
            var name = SyntaxFactory.ParseTypeName(setOfOwnLanguage.ToMinimalDisplayString(partModel, listed.SpanStart)).WithTriviaFrom(listed.Type);

            return await CodeFixSupport.ReplaceNodesAsync(solution, [listed], original => original.WithType(name), cancellationToken).ConfigureAwait(false);
        }

        return solution;
    }

    /// <summary>
    /// Lists the interfaces the class lacks in place of the base set, and detaches its members from that set.
    /// </summary>
    private static async Task<Solution> ImplementAsync(Solution solution, INamedTypeSymbol type, INamedTypeSymbol baseSet, ImmutableArray<INamedTypeSymbol> interfaces,
        Compilation compilation, CancellationToken cancellationToken)
    {
        var added = interfaces.Where(@interface => !type.Interfaces.Contains(@interface, SymbolEqualityComparer.Default)).ToImmutableArray();
        var replacements = new Dictionary<TypeDeclarationSyntax, TypeDeclarationSyntax>();

        foreach (var part in await CodeFixSupport.GetPartsAsync(type, cancellationToken).ConfigureAwait(false))
        {
            var partModel = compilation.GetSemanticModel(part.SyntaxTree);
            var changed = part.ReplaceNodes(part.Members, (original, rewritten) => Detach(original, rewritten, baseSet, partModel, cancellationToken));

            if (ListsBaseSet(part, partModel, baseSet, cancellationToken))
            {
                var names = added.Select(@interface => @interface.ToMinimalDisplayString(partModel, part.BaseList!.SpanStart)).ToArray();

                // Where another part lists every interface already, a part that listed only the set loses its base list.
                changed = names.Length is 0 && changed.BaseList!.Types.Count is 1
                    ? CodeFixSupport.RemoveBaseList(changed)
                    : changed.WithBaseList(ReplaceFirst(changed.BaseList!, names));
            }

            if (changed != part)
                replacements.Add(part, changed);
        }

        return await CodeFixSupport.ReplaceNodesAsync(solution, replacements.Keys, original => replacements[original], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether a part lists the base set, which stands first in its base list.
    /// </summary>
    private static bool ListsBaseSet(TypeDeclarationSyntax part, SemanticModel partModel, INamedTypeSymbol baseSet, CancellationToken cancellationToken)
    {
        var listsBaseSet = part.BaseList is { Types.Count: > 0 } baseList
                           && SymbolEqualityComparer.Default.Equals(partModel.GetTypeInfo(baseList.Types[0].Type, cancellationToken).Type, baseSet);

        return listsBaseSet;
    }

    /// <summary>
    /// The base list with names in place of its first entry, keeping that entry's trivia.
    /// </summary>
    private static BaseListSyntax ReplaceFirst(BaseListSyntax baseList, string[] names)
    {
        var types = baseList.Types;
        var first = types[0];

        if (names.Length is 0)
            return baseList.WithTypes(types.RemoveAt(0));

        var comma = SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space);
        var items = new List<SyntaxNodeOrToken>();

        for (var i = 0; i < names.Length; i++)
        {
            BaseTypeSyntax entry = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(names[i]));

            if (i is 0)
            {
                entry = entry.WithLeadingTrivia(first.GetLeadingTrivia());
            }
            else
            {
                items.Add(comma);
            }

            if (i == names.Length - 1)
                entry = entry.WithTrailingTrivia(first.GetTrailingTrivia());

            items.Add(entry);
        }

        items.AddRange(types.GetWithSeparators().Skip(1));

        var result = baseList.WithTypes(SyntaxFactory.SeparatedList<BaseTypeSyntax>(items));
        return result;
    }

    /// <summary>
    /// Turns <c>override</c> of a base set's member into <c>virtual</c>, so derived classes can still override it, or
    /// drops it where sealed. Drops <c>new</c> where it hid one. Overrides of <see cref="object"/>'s members stay.
    /// </summary>
    private static MemberDeclarationSyntax Detach(MemberDeclarationSyntax original, MemberDeclarationSyntax rewritten, INamedTypeSymbol baseSet,
        SemanticModel partModel, CancellationToken cancellationToken)
    {
        var symbol = partModel.GetDeclaredSymbol(original, cancellationToken);

        if (symbol is not null && Overrides.FindFirstDeclaration(symbol) is { ContainingType.SpecialType: not SpecialType.System_Object })
        {
            var staysVirtual = symbol is { IsSealed: false, IsAbstract: false, ContainingType.IsSealed: false };

            return staysVirtual
                ? CodeFixSupport.ReplaceModifier(rewritten, SyntaxKind.OverrideKeyword, SyntaxKind.VirtualKeyword)
                : CodeFixSupport.RemoveModifiers(rewritten, static modifier => modifier.Kind() is SyntaxKind.OverrideKeyword or SyntaxKind.SealedKeyword);
        }

        if (symbol is not null && rewritten.Modifiers.Any(SyntaxKind.NewKeyword) && IsDeclaredAbove(symbol.Name, baseSet))
            return CodeFixSupport.RemoveModifier(rewritten, SyntaxKind.NewKeyword);

        return rewritten;
    }

    private static bool IsDeclaredAbove(string name, INamedTypeSymbol baseSet)
    {
        for (var ancestor = baseSet; ancestor is { SpecialType: not SpecialType.System_Object }; ancestor = ancestor.BaseType)
            if (!ancestor.GetMembers(name).IsEmpty)
                return true;

        return false;
    }
}
