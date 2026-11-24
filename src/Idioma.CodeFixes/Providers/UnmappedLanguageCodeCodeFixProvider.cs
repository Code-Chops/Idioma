using System.Composition;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a set whose <c>LocalizationLanguageCode</c> implements nothing (<c>LOC206</c>). It lists
/// <c>ILocalizationSet</c> again, at the end of the base list, as in
/// <c>class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet</c>. The property then implements the
/// interface, so the set serves the language it returns.
/// </summary>
/// <remarks>
/// The interface is written by its name alone where the file can name it so, and else by its full name. A partial
/// class gets it in the part the diagnostic is on when that part has a base list, and else in the first part that has
/// one.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnmappedLanguageCodeCodeFixProvider))]
[Shared]
public sealed class UnmappedLanguageCodeCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = [LocalizationRegistrationPart.UnmappedLanguageCodeDiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;

        // The fix is decided here, so that none is offered that would change nothing: a class that lists the interface
        // already, or has no base list, gets no fix.
        if (await CodeFixSupport.FindDeclarationAsync(context).ConfigureAwait(false) is not { } declaration
            || await CodeFixSupport.FindTypeAsync(document, declaration, context.CancellationToken).ConfigureAwait(false) is not var (type, semanticModel)
            || type.Interfaces.Any(PackageNames.IsLocalizationSet))
        {
            return;
        }

        var parts = await CodeFixSupport.GetPartsAsync(type, context.CancellationToken).ConfigureAwait(false);
        var part = declaration.BaseList is not null ? declaration : parts.FirstOrDefault(static part => part.BaseList is not null);

        if (part?.BaseList is not { } baseList)
            return;

        var partModel = semanticModel.Compilation.GetSemanticModel(part.SyntaxTree);
        var added = SyntaxFactory.SimpleBaseType(GetSetInterface(partModel, baseList.SpanStart));
        var solution = document.Project.Solution;

        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"List '{SupportedLanguages.SetInterface}' on '{declaration.Identifier.ValueText}'",
                    createChangedSolution: cancellationToken => AddAsync(solution, part, added, cancellationToken),
                    equivalenceKey: "ListLocalizationSet"),
                diagnostic);
        }
    }

    /// <summary>
    /// Adds <c>ILocalizationSet</c> at the end of the base list of one part of the class.
    /// </summary>
    private static Task<Solution> AddAsync(Solution solution, TypeDeclarationSyntax part, BaseTypeSyntax added, CancellationToken cancellationToken)
    {
        return CodeFixSupport.ReplaceNodesAsync(
            solution,
            [part],
            original => CodeFixSupport.InsertBaseType(original, original.BaseList!.Types.Count, added),
            cancellationToken);
    }

    /// <summary>
    /// <c>ILocalizationSet</c> as the code at a position can name it: by its name alone where that binds to the
    /// interface, and else by its full name.
    /// </summary>
    private static TypeSyntax GetSetInterface(SemanticModel semanticModel, int position)
    {
        var simpleName = SyntaxFactory.IdentifierName(SupportedLanguages.SetInterface);

        var bindsToSetInterface = semanticModel.GetSpeculativeTypeInfo(position, simpleName, SpeculativeBindingOption.BindAsTypeOrNamespace).Type is INamedTypeSymbol bound
            && PackageNames.IsLocalizationSet(bound);

        return bindsToSetInterface ? simpleName : SyntaxFactory.ParseTypeName(PackageNames.LocalizationSet);
    }
}
