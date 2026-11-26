using System.Composition;
using System.Text;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a contract without a set for one of the languages: a contract of this assembly (<c>LOC201</c>), or one of a
/// library it references (<c>LOC700</c>). It adds the set the message names, in a file of its own next to the file the
/// diagnostic is in: <c>ShopLocalizationDe.cs</c> with
/// <c>public class ShopLocalizationDe : ILocalizationSet, IShopLocalization</c>. A region of a language that has a set
/// derives from that set instead, as in <c>public class ShopLocalizationEnGb : ShopLocalizationEn</c>.
/// </summary>
/// <remarks>
/// <para>
/// The new set has no texts. The compiler then names each text the set lacks (<c>CS0535</c>), and the IDE implements
/// them, so a translation is never filled in with the wording of another language. The fix isn't offered when a type of
/// that name exists, or a file of that name lies next to the diagnostic, because that class then needs changing, not
/// adding.
/// </para>
/// <para>
/// A set of a library's contract gets the namespace of the file the diagnostic is in. A file without a namespace gives
/// it the project's default namespace. The set names the contract by its name alone, with a <see langword="using"/> of
/// its namespace where the file needs one: <c>using Shop;</c> and <c>IShopLocalization</c>. The full name could mean
/// another type there, as <c>Shop.IShopLocalization</c> means <c>Contoso.Shop.IShopLocalization</c> inside
/// <c>namespace Contoso.Shop</c>. The fix checks with the compiler that the name it writes means the contract, and
/// writes the full name only where the name alone doesn't.
/// </para>
/// <para>
/// "Fix all" adds every missing set at once, one file each.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingLanguageCodeFixProvider))]
[Shared]
public sealed partial class MissingLanguageCodeFixProvider : CodeFixProvider
{
    // One key for every set, so that "Fix all" adds them all.
    private const string EquivalenceKey = "AddSet";

    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
    [
        LocalizationSetsPart.MissingLanguageDiagnosticId,
        LocalizationSetsPart.LibraryMissingLanguageDiagnosticId,
    ];

    // Its own: the batch fixer merges changes to documents, and this fix adds documents.
    public override FixAllProvider GetFixAllProvider() => AddSetsFixAllProvider.Instance;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var compilation = await document.Project.GetCompilationAsync(context.CancellationToken).ConfigureAwait(false);

        if (compilation is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (await NewSet.ReadAsync(document, compilation, diagnostic, context.CancellationToken).ConfigureAwait(false) is not { } set)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Add '{set.Name}'",
                    createChangedSolution: cancellationToken => AddAsync(document.Project.Solution, [set], cancellationToken),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }
    }

    /// <summary>
    /// The solution with each set in a file of its own. A set whose file another set of the same call takes already is
    /// left out.
    /// </summary>
    private static async Task<Solution> AddAsync(Solution solution, IReadOnlyList<NewSet> sets, CancellationToken cancellationToken)
    {
        var importsPackage = new Dictionary<ProjectId, bool>();

        foreach (var set in sets)
        {
            if (solution.GetProject(set.Document.Project.Id) is not { } project || IsTaken(project, set))
                continue;

            if (!importsPackage.TryGetValue(project.Id, out var imports))
            {
                imports = !await HasGlobalUsingOfPackageAsync(set.Document.Project, cancellationToken).ConfigureAwait(false);
                importsPackage.Add(project.Id, imports);
            }

            var newLine = (await set.Document.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString().Contains("\r\n") ? "\r\n" : "\n";
            var usings = imports && set.Declaration.Contains(SupportedLanguages.SetInterface) ? set.Usings.Add(PackageNames.Namespace) : set.Usings;
            var text = Write(set.Namespace, set.Declaration, usings, newLine);

            solution = solution.AddDocument(DocumentId.CreateNewId(project.Id), set.FileName, text, set.Document.Folders, set.FilePath);
        }

        return solution;
    }

    /// <summary>
    /// Whether a file of the set's name lies where the set's file would go.
    /// </summary>
    private static bool IsTaken(Project project, NewSet set)
    {
        var isTaken = project.Documents.Any(other => set.FilePath is not null
            ? String.Equals(other.FilePath, set.FilePath, StringComparison.OrdinalIgnoreCase)
            : other.FilePath is null && String.Equals(other.Name, set.FileName, StringComparison.OrdinalIgnoreCase));

        return isTaken;
    }

    /// <summary>
    /// The file of the new set: a <see langword="using"/> of each namespace in <paramref name="usings"/>, such as the
    /// package's where the set lists <c>ILocalizationSet</c> and no global using imports it, the set's namespace, and
    /// the set without texts.
    /// </summary>
    private static string Write(string @namespace, string declaration, ImmutableSortedSet<string> usings, string newLine)
    {
        var text = new StringBuilder();

        foreach (var imported in usings)
            text.Append($"using {imported};").Append(newLine);

        if (usings.Count > 0)
            text.Append(newLine);

        if (@namespace.Length > 0)
            text.Append($"namespace {Escape(@namespace)};").Append(newLine).Append(newLine);

        text.Append(declaration).Append(newLine)
            .Append('{').Append(newLine)
            .Append('}').Append(newLine);

        return text.ToString();
    }

    /// <summary>
    /// A namespace as code writes it: with an <c>@</c> in front of each part that is a keyword, as in
    /// <c>Shop.@event</c>.
    /// </summary>
    private static string Escape(string @namespace)
    {
        var escaped = String.Join(".", @namespace.Split('.').Select(static part => SyntaxFacts.GetKeywordKind(part) is SyntaxKind.None ? part : $"@{part}"));
        return escaped;
    }

    /// <summary>
    /// Whether a global using of the project imports the package's namespace, so that a new file needs no using of its
    /// own.
    /// </summary>
    private static async Task<bool> HasGlobalUsingOfPackageAsync(Project project, CancellationToken cancellationToken)
    {
        foreach (var document in project.Documents)
        {
            if (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is not CompilationUnitSyntax root)
                continue;

            foreach (var directive in root.Usings)
            {
                if (directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && directive.StaticKeyword.IsKind(SyntaxKind.None) && directive.Alias is null
                    && String.Equals(directive.Name?.ToString(), PackageNames.Namespace, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Adds every missing set of a document, a project, or the solution at once.
    /// </summary>
    private sealed class AddSetsFixAllProvider : FixAllProvider
    {
        public static AddSetsFixAllProvider Instance { get; } = new();

        public override IEnumerable<FixAllScope> GetSupportedFixAllScopes() => [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution];

        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            var cancellationToken = fixAllContext.CancellationToken;
            var sets = new List<NewSet>();

            foreach (var (project, diagnostics) in await CodeFixSupport.GetFixAllDiagnosticsAsync(fixAllContext).ConfigureAwait(false))
            {
                if (await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) is not { } compilation)
                    continue;

                foreach (var diagnostic in diagnostics)
                {
                    if (CodeFixSupport.FindDocument(project, diagnostic) is { } document
                        && await NewSet.ReadAsync(document, compilation, diagnostic, cancellationToken).ConfigureAwait(false) is { } set)
                    {
                        sets.Add(set);
                    }
                }
            }

            return sets.Count is 0
                ? null
                : CodeAction.Create("Add the missing sets", token => AddAsync(fixAllContext.Solution, sets, token), EquivalenceKey);
        }
    }
}
