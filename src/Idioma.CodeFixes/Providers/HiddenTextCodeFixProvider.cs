using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a member of a set that hides a text of a class it derives from, instead of overriding it (<c>LOC302</c>). It
/// overrides the text, as in <c>public override string Color => "Colour";</c>, or it lists the contract again, as in
/// <c>class ShopLocalizationEnGb : ShopLocalizationEn, IShopLocalization</c>.
/// </summary>
/// <remarks>
/// <para>
/// To override, the fix puts <c>override</c> in the place of <c>new</c>. Where the text isn't virtual yet, it makes the
/// text virtual in the class that declares it: it adds <c>virtual</c>, or removes <c>sealed</c> from an override. So
/// this fix can change two files.
/// </para>
/// <para>
/// There's no fix to override a text that the member can't override:
/// </para>
/// <list type="bullet">
/// <item>A text of another project or assembly that isn't virtual, such as a library's. The fix doesn't change it.</item>
/// <item>A text implemented explicitly, as in <c>string IShopLocalization.Color</c>, which can't be virtual.</item>
/// <item>A text that a class in between hides too. An override would override that class's member, which has a
/// diagnostic of its own.</item>
/// <item>A partial member, or a property with a setter that the text lacks.</item>
/// </list>
/// <para>
/// Listing the contract again is always offered. The contract then calls the set's own member. The contract is named
/// by its name alone where the file can name it so, and else by its full name.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(HiddenTextCodeFixProvider))]
[Shared]
public sealed class HiddenTextCodeFixProvider : CodeFixProvider
{
    private const string OverrideKey = "OverrideHiddenText";
    private const string ListContractKey = "ListContractAgain";

    /// <summary>
    /// Applies the fix the IDE asks for to every diagnostic at once. Each member and each class changes once, because
    /// two sets can hide one text, and one set can hide two texts of a contract.
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
                : null));

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

    /// <summary>
    /// A member of a set that hides a text, with the contract of that text and the member that implements it.
    /// </summary>
    private sealed record Hiding(MemberDeclarationSyntax Declaration, ISymbol Member, INamedTypeSymbol Set, INamedTypeSymbol Contract, ISymbol Implementation);

    /// <summary>
    /// The member a diagnostic sits on the name of, with the text it hides. <see langword="null"/> when the code changed
    /// since the diagnostic was reported.
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
    /// What overriding the hidden text changes, with the title of that fix. <see langword="null"/> when the member can't
    /// override it (see the remarks of this class).
    /// </summary>
    private static (string Title, Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax> Changes)? Override(Hiding hiding,
        Compilation compilation, CancellationToken cancellationToken)
    {
        var (declaration, member, set, _, implementation) = hiding;

        if (FindOverridden(member, set) is not { IsStatic: false } overridden
            || !LeadsTo(overridden, implementation)
            || IsPartial(declaration)
            || member is IPropertySymbol { SetMethod: not null } && overridden is IPropertySymbol { SetMethod: null })
        {
            return null;
        }

        var name = $"{overridden.ContainingType.Name}.{member.Name}";
        var changes = new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>
        {
            [declaration] = declaration.WithModifiers(WithOverride(declaration.Modifiers)),
        };

        if (overridden is { IsVirtual: true } or { IsAbstract: true } or { IsOverride: true, IsSealed: false })
            return ($"Override '{name}'", changes);

        // Made virtual where it's declared, which has to be a file of this project.
        if (overridden.DeclaringSyntaxReferences.Length is not 1
            || !compilation.ContainsSyntaxTree(overridden.DeclaringSyntaxReferences[0].SyntaxTree)
            || overridden.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is not MemberDeclarationSyntax text
            || text is not (PropertyDeclarationSyntax or MethodDeclarationSyntax)
            || IsPartial(text))
        {
            return null;
        }

        changes[text] = text.WithModifiers(overridden.IsOverride
            ? Remove(text.Modifiers, SyntaxKind.SealedKeyword)
            : Insert(text.Modifiers, SyntaxKind.VirtualKeyword));

        return ($"Make '{name}' virtual and override it", changes);
    }

    /// <summary>
    /// The member that an override of <paramref name="member"/> would override: the first of its name and parameters in
    /// the classes its set derives from.
    /// </summary>
    private static ISymbol? FindOverridden(ISymbol member, INamedTypeSymbol set)
    {
        for (var type = set.BaseType; type is not null; type = type.BaseType)
        {
            foreach (var candidate in type.GetMembers(member.Name))
                if (HiddenTextAnalyzer.Hides(member, candidate))
                    return candidate;
        }

        return null;
    }

    /// <summary>
    /// Whether a member implements the text, or overrides the member that does, so that an override of it words the
    /// text.
    /// </summary>
    private static bool LeadsTo(ISymbol overridden, ISymbol implementation)
    {
        for (var member = overridden; member is not null; member = GetOverridden(member))
        {
            if (SymbolEqualityComparer.Default.Equals(member, implementation))
                return true;
        }

        return false;
    }

    private static ISymbol? GetOverridden(ISymbol member)
    {
        var overridden = member switch
        {
            IPropertySymbol property => property.OverriddenProperty,
            IMethodSymbol method => (ISymbol?)method.OverriddenMethod,
            _ => null,
        };

        return overridden;
    }

    private static bool IsPartial(MemberDeclarationSyntax declaration)
    {
        return declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    /// <summary>
    /// The modifiers of the hiding member with <c>override</c> in the place of <c>new</c>, or else after its
    /// accessibility. A <c>virtual</c> goes, because an override is virtual itself.
    /// </summary>
    private static SyntaxTokenList WithOverride(SyntaxTokenList modifiers)
    {
        var index = modifiers.IndexOf(SyntaxKind.NewKeyword);

        var withOverride = index >= 0
            ? modifiers.Replace(modifiers[index], SyntaxFactory.Token(modifiers[index].LeadingTrivia, SyntaxKind.OverrideKeyword, modifiers[index].TrailingTrivia))
            : Insert(modifiers, SyntaxKind.OverrideKeyword);

        return Remove(withOverride, SyntaxKind.VirtualKeyword);
    }

    /// <summary>
    /// The modifiers with a keyword after the last accessibility keyword, such as <c>public</c>, or else first.
    /// </summary>
    private static SyntaxTokenList Insert(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        var index = 0;

        for (var i = 0; i < modifiers.Count; i++)
        {
            if (SyntaxFacts.IsAccessibilityModifier(modifiers[i].Kind()))
                index = i + 1;
        }

        var keyword = SyntaxFactory.Token(kind).WithTrailingTrivia(SyntaxFactory.Space);

        // First: the keyword takes over what stood before the modifiers, such as their indentation.
        if (index is 0 && modifiers.Count > 0)
        {
            keyword = keyword.WithLeadingTrivia(modifiers[0].LeadingTrivia);
            modifiers = modifiers.Replace(modifiers[0], modifiers[0].WithLeadingTrivia());
        }

        return modifiers.Insert(index, keyword);
    }

    /// <summary>
    /// The modifiers without a keyword. What stood before it, such as the indentation of a first modifier, goes to the
    /// modifier after it.
    /// </summary>
    private static SyntaxTokenList Remove(SyntaxTokenList modifiers, SyntaxKind kind)
    {
        var index = modifiers.IndexOf(kind);

        if (index < 0)
            return modifiers;

        var removed = modifiers[index];
        modifiers = modifiers.RemoveAt(index);

        if (index < modifiers.Count && removed.LeadingTrivia.Count > 0)
            modifiers = modifiers.Replace(modifiers[index], modifiers[index].WithLeadingTrivia(removed.LeadingTrivia.AddRange(modifiers[index].LeadingTrivia)));

        return modifiers;
    }

    /// <summary>
    /// What listing the contract again changes: the end of the base list of the part of the set that holds the member,
    /// or else of the first part that has one. <see langword="null"/> when no part has a base list.
    /// </summary>
    private static Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>? ListContract(Hiding hiding, Compilation compilation,
        CancellationToken cancellationToken)
    {
        var part = hiding.Declaration.Parent as TypeDeclarationSyntax;

        if (part?.BaseList is null)
        {
            part = hiding.Set.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(cancellationToken))
                .OfType<TypeDeclarationSyntax>()
                .FirstOrDefault(static declaration => declaration.BaseList is not null);
        }

        if (part?.BaseList is not { } baseList)
            return null;

        var contract = NameContract(hiding.Contract, compilation.GetSemanticModel(part.SyntaxTree), baseList.SpanStart);
        var changes = new Dictionary<MemberDeclarationSyntax, MemberDeclarationSyntax>
        {
            [part] = CodeFixSupport.InsertBaseType(part, baseList.Types.Count, SyntaxFactory.SimpleBaseType(contract)),
        };

        return changes;
    }

    /// <summary>
    /// The contract as the code at a position can name it: by its name alone where that binds to the contract, and else
    /// by its full name.
    /// </summary>
    private static TypeSyntax NameContract(INamedTypeSymbol contract, SemanticModel semanticModel, int position)
    {
        var simpleName = SyntaxFactory.IdentifierName(contract.Name);

        var bindsToContract = SymbolEqualityComparer.Default.Equals(
            semanticModel.GetSpeculativeTypeInfo(position, simpleName, SpeculativeBindingOption.BindAsTypeOrNamespace).Type,
            contract);

        return bindsToContract ? simpleName : SyntaxFactory.ParseTypeName(contract.ToDisplayString());
    }

    /// <summary>
    /// Applies to every diagnostic the fix whose key the IDE asks for: overriding the texts, or listing the contracts
    /// again.
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
