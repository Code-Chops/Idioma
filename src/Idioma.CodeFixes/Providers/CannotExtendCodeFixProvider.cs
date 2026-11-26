using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Rename;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a default-language set the generator can't use (<c>LOC200</c>) where one edit does: it names the class after
/// its language (<c>ShopLocalization</c> becomes <c>ShopLocalizationEn</c>, everywhere it is used), or removes the
/// <see langword="abstract"/> or <see langword="file"/> modifier.
/// </summary>
/// <remarks>
/// A nested or generic set gets no fix, because moving a class out of its container, or closing its type parameters, is
/// a decision about the surrounding code. Nor does an abstract set with abstract members, which would no longer
/// compile, or a set without a parameterless constructor, as only its author knows what the constructor can do without.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CannotExtendCodeFixProvider))]
[Shared]
public sealed class CannotExtendCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [LocalizationSetsPart.CannotExtendDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;

        // The fix is decided here, so that none is offered that would change nothing.
        if (await CodeFixSupport.FindDeclarationAsync(context).ConfigureAwait(false) is not { } declaration
            || await CodeFixSupport.FindTypeAsync(document, declaration, context.CancellationToken).ConfigureAwait(false) is not var (type, _))
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
            if (CreateFix(document.Project.Solution, declaration, type, diagnostic) is { } fix)
                context.RegisterCodeFix(fix, diagnostic);
    }

    /// <summary>
    /// The fix for what the generator reported, or <see langword="null"/> when no single edit resolves it. The shapes
    /// are told apart in the order the generator reports them, so the fix is for the problem the message names.
    /// </summary>
    private static CodeAction? CreateFix(Solution solution, TypeDeclarationSyntax declaration, INamedTypeSymbol type, Diagnostic diagnostic)
    {
        if (diagnostic.Properties.TryGetValue(LocalizationSetsPart.SuggestedNameProperty, out var suggestedName) && suggestedName is not null)
        {
            // Renamed since the diagnostic was reported.
            if (String.Equals(type.Name, suggestedName, StringComparison.Ordinal))
                return null;

            return CodeAction.Create(
                title: $"Rename to '{suggestedName}'",
                createChangedSolution: cancellationToken => Renamer.RenameSymbolAsync(solution, type, new SymbolRenameOptions(), suggestedName, cancellationToken),
                equivalenceKey: "Rename");
        }

        if (declaration.Parent is TypeDeclarationSyntax)
            return null;

        if (declaration.Modifiers.Any(SyntaxKind.FileKeyword))
            return RemoveModifier(solution, declaration, type, SyntaxKind.FileKeyword);

        if (declaration.TypeParameterList is not null)
            return null;

        // A class that isn't abstract can't hold an abstract member, nor inherit one it doesn't override, so such a set
        // gets no fix.
        if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) && !LeavesAbstractMember(type))
            return RemoveModifier(solution, declaration, type, SyntaxKind.AbstractKeyword);

        return null;
    }

    /// <summary>
    /// Whether a class has an abstract member without an implementation: one it declares, or one of a class it derives
    /// from that neither it nor a class in between overrides. Without <see langword="abstract"/>, such a class doesn't
    /// compile (<c>CS0534</c>).
    /// </summary>
    private static bool LeavesAbstractMember(INamedTypeSymbol type)
    {
        // What the class and the classes it derives from override, as each is reached from the class up.
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.IsAbstract && !overridden.Contains(member.OriginalDefinition))
                    return true;

                var overrides = member switch
                {
                    IMethodSymbol method => method.OverriddenMethod,
                    IPropertySymbol property => property.OverriddenProperty,
                    IEventSymbol @event => @event.OverriddenEvent,
                    _ => (ISymbol?)null,
                };

                if (overrides is not null)
                    overridden.Add(overrides.OriginalDefinition);
            }
        }

        return false;
    }

    private static CodeAction RemoveModifier(Solution solution, TypeDeclarationSyntax declaration, INamedTypeSymbol type, SyntaxKind modifier)
    {
        var keyword = SyntaxFacts.GetText(modifier);

        return CodeAction.Create(
            title: $"Remove '{keyword}' from '{declaration.Identifier.ValueText}'",
            createChangedSolution: cancellationToken => RemoveModifierAsync(solution, type, modifier, cancellationToken),
            equivalenceKey: $"Remove.{keyword}");
    }

    /// <summary>
    /// Removes a modifier from every part of the class that carries it, because each part of a partial class may repeat
    /// it. The part the diagnostic is on carries it, so the fix always changes that part.
    /// </summary>
    private static async Task<Solution> RemoveModifierAsync(Solution solution, INamedTypeSymbol type, SyntaxKind modifier, CancellationToken cancellationToken)
    {
        var parts = await CodeFixSupport.GetPartsAsync(type, cancellationToken).ConfigureAwait(false);

        return await CodeFixSupport.ReplaceNodesAsync(
            solution,
            parts.Where(part => part.Modifiers.Any(modifier)),
            part => WithoutModifier(part, modifier),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a modifier, and hands what stood before it, such as a comment or the indentation, to the token that
    /// takes its place.
    /// </summary>
    private static TypeDeclarationSyntax WithoutModifier(TypeDeclarationSyntax declaration, SyntaxKind modifier)
    {
        var index = declaration.Modifiers.IndexOf(modifier);
        var removed = declaration.Modifiers[index];
        var modifiers = declaration.Modifiers.RemoveAt(index);

        if (index < modifiers.Count)
            return declaration.WithModifiers(modifiers.Replace(modifiers[index], modifiers[index].WithLeadingTrivia(removed.LeadingTrivia)));

        return declaration
            .WithModifiers(modifiers)
            .WithKeyword(declaration.Keyword.WithLeadingTrivia(removed.LeadingTrivia));
    }
}
