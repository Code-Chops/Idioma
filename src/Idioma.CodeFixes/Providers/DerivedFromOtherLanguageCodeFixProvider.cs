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
/// The texts of the set no longer override anything, so <c>override</c> becomes <c>virtual</c>, and a member that hid
/// one loses <c>new</c>. A class that derives from the set can then still override its texts. A sealed text, or a text
/// of a sealed set, loses <c>override</c> without becoming virtual. An override of a member of <see cref="object"/>,
/// such as <c>ToString()</c>, stays, also where a set overrode it before. What still reads the class it derived from,
/// such as <c>base.Color</c>, stops compiling, which shows what to change.
/// </para>
/// <para>
/// A set of an enum lists <c>ILocalizationSet&lt;TEnum&gt;</c> in place of <c>ILocalizationSet</c>, for each enum the
/// set it derived from lists.
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

        // A set of an enum lists ILocalizationSet<TEnum> for each enum the set it derived from lists, as every set of
        // its contract does (LOC205).
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
    /// A member without <c>override</c> where it overrode a member that the set the class derived from, or a class
    /// further up than <see cref="object"/>, declared first, and without <c>new</c> where it hid one. Such a member
    /// stays <c>virtual</c>, so that a class deriving from this one can still override it, unless it was sealed, or
    /// the class is.
    /// </summary>
    private static MemberDeclarationSyntax Detach(MemberDeclarationSyntax original, MemberDeclarationSyntax rewritten, INamedTypeSymbol baseSet,
        SemanticModel partModel, CancellationToken cancellationToken)
    {
        var symbol = partModel.GetDeclaredSymbol(original, cancellationToken);

        // The member that started the chain of overrides: ToString() of an override of an override is object's.
        if (symbol is not null && FindFirstDeclaration(symbol) is { ContainingType.SpecialType: not SpecialType.System_Object })
        {
            var staysVirtual = symbol is { IsSealed: false, IsAbstract: false, ContainingType.IsSealed: false };

            return staysVirtual
                ? ReplaceModifier(rewritten, SyntaxKind.OverrideKeyword, SyntaxKind.VirtualKeyword)
                : RemoveModifiers(rewritten, static modifier => modifier.Kind() is SyntaxKind.OverrideKeyword or SyntaxKind.SealedKeyword);
        }

        if (symbol is not null && rewritten.Modifiers.Any(SyntaxKind.NewKeyword) && IsDeclaredAbove(symbol.Name, baseSet))
            return RemoveModifiers(rewritten, static modifier => modifier.IsKind(SyntaxKind.NewKeyword));

        return rewritten;
    }

    /// <summary>
    /// The member an override overrides at the start of its chain, or <see langword="null"/> for a member that
    /// overrides nothing.
    /// </summary>
    private static ISymbol? FindFirstDeclaration(ISymbol member)
    {
        ISymbol? first = null;

        for (var current = Overridden(member); current is not null; current = Overridden(current))
            first = current;

        return first;

        static ISymbol? Overridden(ISymbol member) => member switch
        {
            IPropertySymbol property => property.OverriddenProperty,
            IMethodSymbol method => method.OverriddenMethod,
            IEventSymbol @event => @event.OverriddenEvent,
            _ => null,
        };
    }

    /// <summary>
    /// A member with one modifier in place of another, which keeps what stood around it.
    /// </summary>
    private static MemberDeclarationSyntax ReplaceModifier(MemberDeclarationSyntax member, SyntaxKind replaced, SyntaxKind replacement)
    {
        var index = member.Modifiers.IndexOf(replaced);
        if (index < 0)
            return member;

        var old = member.Modifiers[index];
        var result = member.WithModifiers(member.Modifiers.Replace(old, SyntaxFactory.Token(old.LeadingTrivia, replacement, old.TrailingTrivia)));

        return result;
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
