using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a member of a set that hides a text of a base class instead of overriding it (<c>LOC302</c>). It overrides
/// the text, or it lists the contract again, as in
/// <c>class ShopLocalizationEnGb : ShopLocalizationEn, IShopLocalization</c>.
/// </summary>
/// <remarks>
/// <para>
/// Overriding replaces <see langword="new"/> with <see langword="override"/>. Where the text isn't virtual yet,
/// the fix also makes it virtual where it's declared, so it can change two files.
/// </para>
/// <para>There's no override fix for:</para>
/// <list type="bullet">
/// <item>a non-virtual text of another project or assembly.</item>
/// <item>an explicitly implemented text, which can't be virtual.</item>
/// <item>a text that a class in between hides too, which has its own diagnostic.</item>
/// <item>a partial member, or a property with a setter that the text lacks.</item>
/// </list>
/// <para>Listing the contract again is always offered.</para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(HiddenTextCodeFixProvider))]
[Shared]
public sealed class HiddenTextCodeFixProvider : CodeFixProvider
{
    private const string OverrideKey = "OverrideHiddenText";
    private const string ListContractKey = "ListContractAgain";

    /// <summary>
    /// Applies the chosen fix to every diagnostic at once. Each member and class changes once, because several
    /// diagnostics can share them.
    /// </summary>
    private static readonly FixAllProvider FixAll = new FixAllByKey(
        overriding: new ReplaceNodesFixAllProvider<MemberDeclarationSyntax>(
            "Override the hidden texts",
            OverrideKey,
            static (root, semanticModel, diagnostic, cancellationToken) => Find(root, semanticModel, diagnostic, cancellationToken) is { } hiding
                ? Override(hiding, semanticModel.Compilation, cancellationToken)?.Changes
                : null),
        listing: new ReplaceNodesFixAllProvider<MemberDeclarationSyntax>(
            "List the contracts again",
            ListContractKey,
            static (root, semanticModel, diagnostic, cancellationToken) => Find(root, semanticModel, diagnostic, cancellationToken) is { } hiding
                ? ListContract(hiding, semanticModel.Compilation, cancellationToken)
                : null,
            merge: ListBoth));

    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [HiddenTextAnalyzer.HiddenTextDiagnosticId];

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
            if (Find(root, semanticModel, diagnostic, context.CancellationToken) is not { } hiding)
                continue;

            if (Override(hiding, semanticModel.Compilation, context.CancellationToken) is { } overriding)
                Register(context, diagnostic, overriding.Title, OverrideKey, solution, overriding.Changes);

            if (ListContract(hiding, semanticModel.Compilation, context.CancellationToken) is { } listing)
                Register(context, diagnostic, $"List '{hiding.Contract.Name}' again on '{hiding.Set.Name}'", ListContractKey, solution, listing);
        }
    }

    private static void Register(CodeFixContext context, Diagnostic diagnostic, string title, string equivalenceKey, Solution solution,
        Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax> changes)
    {
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                createChangedSolution: cancellationToken => CodeFixSupport.ReplaceNodesAsync(solution, changes.Keys, node => changes[node], cancellationToken),
                equivalenceKey: equivalenceKey),
            diagnostic);
    }

    private sealed record Hiding(MemberDeclarationSyntax Declaration, ISymbol Member, INamedTypeSymbol Set, INamedTypeSymbol Contract, ISymbol Implementation);

    /// <summary>
    /// The member a diagnostic sits on, with the text it hides, or <see langword="null"/> when the code has changed.
    /// </summary>
    private static Hiding? Find(SyntaxNode root, SemanticModel semanticModel, Diagnostic diagnostic, CancellationToken cancellationToken)
    {
        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } declaration
            || declaration is not (PropertyDeclarationSyntax or MethodDeclarationSyntax)
            || semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not { ContainingType: { } set } member
            || !diagnostic.Properties.TryGetValue(HiddenTextAnalyzer.ContractProperty, out var contractName)
            || set.AllInterfaces.FirstOrDefault(contract => String.Equals(contract.ToDisplayString(), contractName, StringComparison.Ordinal)) is not { } contract)
        {
            return null;
        }

        foreach (var text in contract.GetMembers(member.Name))
        {
            if (set.FindImplementationForInterfaceMember(text) is { } implementation
                && !SymbolEqualityComparer.Default.Equals(implementation.ContainingType, set)
                && HiddenTextAnalyzer.Hides(member, implementation))
            {
                return new Hiding(declaration, member, set, contract, implementation);
            }
        }

        return null;
    }

    /// <summary>
    /// The changes and title of the override fix, or <see langword="null"/> when there's none (see the class remarks).
    /// </summary>
    private static (string Title, Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax> Changes)? Override(Hiding hiding,
        Compilation compilation, CancellationToken cancellationToken)
    {
        var (declaration, member, set, _, implementation) = hiding;

        // The member an override would override, as LOC302 names it.
        if (HiddenTextAnalyzer.FindHidden(member, set) is not { IsStatic: false } overridden
            || !Overrides.IsOrOverrides(overridden, implementation)
            || IsPartial(declaration)
            || member is IPropertySymbol { SetMethod: not null } && overridden is IPropertySymbol { SetMethod: null })
        {
            return null;
        }

        var name = $"{overridden.ContainingType.Name}.{member.Name}";
        var changes = new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>
        {
            [declaration] = WithOverride(declaration),
        };

        if (overridden is { IsVirtual: true } or { IsAbstract: true } or { IsOverride: true, IsSealed: false })
            return ($"Override '{name}'", changes);

        // Made virtual where it's declared, which must be in this project.
        if (overridden.DeclaringSyntaxReferences.Length is not 1
            || !compilation.ContainsSyntaxTree(overridden.DeclaringSyntaxReferences[0].SyntaxTree)
            || overridden.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is not MemberDeclarationSyntax text
            || text is not (PropertyDeclarationSyntax or MethodDeclarationSyntax)
            || IsPartial(text))
        {
            return null;
        }

        // A sealed override loses 'sealed', and any other member becomes virtual. The title says which, as LOC302 does.
        if (overridden.IsOverride)
        {
            changes[text] = CodeFixSupport.RemoveModifier(text, SyntaxKind.SealedKeyword);
            return ($"Remove 'sealed' from '{name}' and override it", changes);
        }

        changes[text] = CodeFixSupport.InsertModifier(text, SyntaxKind.VirtualKeyword);
        return ($"Make '{name}' virtual and override it", changes);
    }

    private static bool IsPartial(MemberDeclarationSyntax declaration)
    {
        return declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    /// <summary>
    /// Puts <c>override</c> in place of <c>new</c>, or after the accessibility. Drops <c>virtual</c>, because an
    /// override is virtual already.
    /// </summary>
    private static MemberDeclarationSyntax WithOverride(MemberDeclarationSyntax declaration)
    {
        var withOverride = declaration.Modifiers.Any(SyntaxKind.NewKeyword)
            ? CodeFixSupport.ReplaceModifier(declaration, SyntaxKind.NewKeyword, SyntaxKind.OverrideKeyword)
            : CodeFixSupport.InsertModifier(declaration, SyntaxKind.OverrideKeyword);

        return CodeFixSupport.RemoveModifier(withOverride, SyntaxKind.VirtualKeyword);
    }

    /// <summary>
    /// Appends the contract to the base list of the member's part, or else of the first part with a base list.
    /// </summary>
    private static Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>? ListContract(Hiding hiding, Compilation compilation,
        CancellationToken cancellationToken)
    {
        var part = hiding.Declaration.Parent as TypeDeclarationSyntax;

        if (part?.BaseList is null)
            part = CodeFixSupport.GetParts(hiding.Set, cancellationToken).FirstOrDefault(static declaration => declaration.BaseList is not null);

        if (part?.BaseList is not { } baseList)
            return null;

        // One name per part, so ListBoth compares names.
        var contract = SyntaxFactory.ParseTypeName(CodeFixSupport.NameAt(compilation.GetSemanticModel(part.SyntaxTree), baseList.SpanStart, hiding.Contract));
        var changes = new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>
        {
            [part] = CodeFixSupport.InsertBaseType(part, baseList.Types.Count, SyntaxFactory.SimpleBaseType(contract)),
        };

        return changes;
    }

    /// <summary>
    /// Merges two listings on one part by adding the later's missing contracts to the earlier. A contract has one name
    /// per part (<see cref="CodeFixSupport.NameAt"/>), so comparing names is enough.
    /// </summary>
    private static MemberDeclarationSyntax ListBoth(MemberDeclarationSyntax earlier, MemberDeclarationSyntax later)
    {
        if (earlier is not TypeDeclarationSyntax { BaseList: not null } listed || later is not TypeDeclarationSyntax { BaseList: { } laterList })
            return earlier;

        foreach (var entry in laterList.Types)
        {
            var name = entry.Type.WithoutTrivia();

            if (!listed.BaseList!.Types.Any(type => String.Equals(type.Type.WithoutTrivia().ToString(), name.ToString(), StringComparison.Ordinal)))
                listed = CodeFixSupport.InsertBaseType(listed, listed.BaseList.Types.Count, SyntaxFactory.SimpleBaseType(name));
        }

        return listed;
    }

    /// <summary>
    /// Picks the "Fix all" provider by the equivalence key the IDE asks for.
    /// </summary>
    private sealed class FixAllByKey(FixAllProvider overriding, FixAllProvider listing) : FixAllProvider
    {
        public override IEnumerable<FixAllScope> GetSupportedFixAllScopes() => overriding.GetSupportedFixAllScopes();

        public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            var provider = String.Equals(fixAllContext.CodeActionEquivalenceKey, ListContractKey, StringComparison.Ordinal) ? listing : overriding;
            return provider.GetFixAsync(fixAllContext);
        }
    }
}
