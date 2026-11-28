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
/// A nested or generic set gets no fix, because that's a decision about the surrounding code. Nor does an abstract set
/// with abstract members, or a set without a parameterless constructor.
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
    /// The fix, or <see langword="null"/> when no single edit resolves it. Checks run in the generator's order, so the
    /// fix is for the problem the message names.
    /// </summary>
    private static CodeAction? CreateFix(Solution solution, TypeDeclarationSyntax declaration, INamedTypeSymbol type, Diagnostic diagnostic)
    {
        if (diagnostic.Properties.TryGetValue(LocalizationSetsPart.SuggestedNameProperty, out var suggestedName) && suggestedName is not null)
        {
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

        if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) && !LeavesAbstractMember(type))
            return RemoveModifier(solution, declaration, type, SyntaxKind.AbstractKeyword);

        return null;
    }

    /// <summary>
    /// Whether a class declares or inherits an abstract member it doesn't override, so it can't drop
    /// <see langword="abstract"/> (<c>CS0534</c>).
    /// </summary>
    private static bool LeavesAbstractMember(INamedTypeSymbol type)
    {
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.IsAbstract && !overridden.Contains(member.OriginalDefinition))
                    return true;

                if (Overrides.GetOverridden(member) is { } overrides)
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
    /// Removes a modifier from every part of the class, because each part of a partial class may repeat it.
    /// </summary>
    private static async Task<Solution> RemoveModifierAsync(Solution solution, INamedTypeSymbol type, SyntaxKind modifier, CancellationToken cancellationToken)
    {
        var parts = await CodeFixSupport.GetPartsAsync(type, cancellationToken).ConfigureAwait(false);

        return await CodeFixSupport.ReplaceNodesAsync(
            solution,
            parts.Where(part => part.Modifiers.Any(modifier)),
            part => CodeFixSupport.RemoveModifier(part, modifier),
            cancellationToken).ConfigureAwait(false);
    }
}
