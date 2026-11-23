using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a set that derives from a set of another language (<c>LOC203</c>). It implements the contract instead, which
/// requires every text: <c>class ShopLocalizationNl : ShopLocalizationEn</c> becomes
/// <c>class ShopLocalizationNl : ILocalizationSet, IShopLocalization</c>. The compiler then names each text the set
/// lacks (<c>CS0535</c>).
/// </summary>
/// <remarks>
/// <para>
/// The texts of the set no longer override anything, so they lose <c>override</c>, and a member that hid one loses
/// <c>new</c>. An override of a member of <see cref="object"/>, such as <c>ToString()</c>, stays. What still reads the
/// class it derived from, such as <c>base.Color</c>, stops compiling, which shows what to change.
/// </para>
/// <para>
/// A region that derives from another region of its language also gets a fix that derives from the set of its language,
/// where one is further up: <c>class ShopLocalizationEnUs : ShopLocalizationEnGb</c> becomes
/// <c>class ShopLocalizationEnUs : ShopLocalizationEn</c>. Its texts then fall back to that language.
/// </para>
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

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Implement '{String.Join("' and '", contracts.Select(static contract => contract.Name))}' instead",
                createChangedSolution: cancellationToken => ImplementAsync(document.Project.Solution, type, baseSet, [localizationSet, .. contracts], compilation, cancellationToken),
                equivalenceKey: "ImplementContractInstead"),
            context.Diagnostics);
    }

    /// <summary>
    /// The nearest set further up than the one a set derives from that serves the set's language, such as
    /// <c>ShopLocalizationEn</c> for <c>ShopLocalizationEnUs : ShopLocalizationEnGb</c>, or <see langword="null"/>
    /// when there's none.
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

    /// <summary>
    /// Names another set in place of the one the set derives from, in the part of the class that lists it.
    /// </summary>
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
    /// Lists <c>ILocalizationSet</c> and the contracts in place of the set the class derives from, each one the class
    /// doesn't list itself, and takes <c>override</c> and <c>new</c> off its members where they referred to that set.
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
                changed = changed.WithBaseList(ReplaceFirst(changed.BaseList!, names));
            }

            if (changed != part)
                replacements.Add(part, changed);
        }

        return await CodeFixSupport.ReplaceNodesAsync(solution, replacements.Keys, original => replacements[original], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether a part of a class lists the set it derives from, which stands first in its base list.
    /// </summary>
    private static bool ListsBaseSet(TypeDeclarationSyntax part, SemanticModel partModel, INamedTypeSymbol baseSet, CancellationToken cancellationToken)
    {
        var listsBaseSet = part.BaseList is { Types.Count: > 0 } baseList
                           && SymbolEqualityComparer.Default.Equals(partModel.GetTypeInfo(baseList.Types[0].Type, cancellationToken).Type, baseSet);

        return listsBaseSet;
    }

    /// <summary>
    /// The base list with names in place of its first entry, which takes over what stood around that entry. Without
    /// names, the entry goes.
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

        // The other entries with the comma before each.
        items.AddRange(types.GetWithSeparators().Skip(1));

        var result = baseList.WithTypes(SyntaxFactory.SeparatedList<BaseTypeSyntax>(items));
        return result;
    }

    /// <summary>
    /// A member without <c>override</c> and <c>sealed</c> where it overrode a member of the set the class derived from,
    /// or of a class further up than <see cref="object"/>, and without <c>new</c> where it hid one.
    /// </summary>
    private static MemberDeclarationSyntax Detach(MemberDeclarationSyntax original, MemberDeclarationSyntax rewritten, INamedTypeSymbol baseSet,
        SemanticModel partModel, CancellationToken cancellationToken)
    {
        var symbol = partModel.GetDeclaredSymbol(original, cancellationToken);

        ISymbol? overridden = symbol switch
        {
            IPropertySymbol property => property.OverriddenProperty,
            IMethodSymbol method => method.OverriddenMethod,
            IEventSymbol @event => @event.OverriddenEvent,
            _ => null,
        };

        if (overridden is { ContainingType.SpecialType: not SpecialType.System_Object })
            return RemoveModifiers(rewritten, static modifier => modifier.Kind() is SyntaxKind.OverrideKeyword or SyntaxKind.SealedKeyword);

        if (symbol is not null && rewritten.Modifiers.Any(SyntaxKind.NewKeyword) && IsDeclaredAbove(symbol.Name, baseSet))
            return RemoveModifiers(rewritten, static modifier => modifier.IsKind(SyntaxKind.NewKeyword));

        return rewritten;
    }

    /// <summary>
    /// Whether a set or a class it derives from, other than <see cref="object"/>, has a member of a name.
    /// </summary>
    private static bool IsDeclaredAbove(string name, INamedTypeSymbol baseSet)
    {
        for (var ancestor = baseSet; ancestor is { SpecialType: not SpecialType.System_Object }; ancestor = ancestor.BaseType)
            if (!ancestor.GetMembers(name).IsEmpty)
                return true;

        return false;
    }

    /// <summary>
    /// A member without some of its modifiers. What stood before the first one it loses, such as a line break and its
    /// indentation, moves to what then comes first.
    /// </summary>
    private static MemberDeclarationSyntax RemoveModifiers(MemberDeclarationSyntax member, Func<SyntaxToken, bool> remove)
    {
        var kept = new List<SyntaxToken>();
        SyntaxTriviaList? carried = null;

        foreach (var modifier in member.Modifiers)
        {
            if (remove(modifier))
            {
                if (kept.Count is 0)
                    carried ??= modifier.LeadingTrivia;
            }
            else if (carried is { } leading)
            {
                kept.Add(modifier.WithLeadingTrivia(leading));
                carried = null;
            }
            else
            {
                kept.Add(modifier);
            }
        }

        var result = member.WithModifiers(SyntaxFactory.TokenList(kept));

        // No modifier left: the token after the attributes takes it, such as the type of a property.
        if (kept.Count is 0 && carried is { } rest)
        {
            var next = result.AttributeLists.Count > 0 ? result.AttributeLists[^1].GetLastToken().GetNextToken() : result.GetFirstToken();
            result = result.ReplaceToken(next, next.WithLeadingTrivia(rest));
        }

        return result;
    }
}
