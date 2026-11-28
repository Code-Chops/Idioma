using System.Composition;
using System.Text;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CodeChops.Idioma.CodeFixes;

/// <summary>
/// Fixes a contract without a set for one of the languages (<c>LOC201</c>, or <c>LOC700</c> for a library's contract).
/// It adds the set in a file of its own next to the diagnostic's file. A region of a language that has a set derives
/// from that set instead.
/// </summary>
/// <remarks>
/// The new set has no texts, so the compiler names each missing one and a translation is never filled in with another
/// language's wording. The fix isn't offered when a type or file of that name exists already.
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

    // Its own, because the batch fixer merges changes to documents and this fix adds documents.
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
    /// The solution with each set in a file of its own, skipping a set whose file is taken.
    /// </summary>
    private static async Task<Solution> AddAsync(Solution solution, IReadOnlyList<NewSet> sets, CancellationToken cancellationToken)
    {
        var importsPackage = new Dictionary<ProjectId, bool>();
        var compilations = new Dictionary<ProjectId, Compilation?>();

        foreach (var set in sets)
        {
            if (solution.GetProject(set.Document.Project.Id) is not { } project || IsTaken(project, set))
                continue;

            if (!importsPackage.TryGetValue(project.Id, out var imports))
            {
                imports = !await HasGlobalUsingOfPackageAsync(set.Document.Project, cancellationToken).ConfigureAwait(false);
                importsPackage.Add(project.Id, imports);
            }

            // The compilation the diagnostic was read from, without the sets added here.
            if (!compilations.TryGetValue(project.Id, out var compilation))
            {
                compilation = await set.Document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                compilations.Add(project.Id, compilation);
            }

            var (declaration, named) = compilation is null ? (set.Declaration, ImmutableSortedSet.Create<string>(StringComparer.Ordinal)) : set.NameTypes(compilation, cancellationToken);

            var newLine = (await set.Document.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString().Contains("\r\n") ? "\r\n" : "\n";
            var usings = imports && declaration.Contains(SupportedLanguages.SetInterface) ? named.Add(PackageNames.Namespace) : named;
            var text = Write(set.Namespace, declaration, usings, newLine);

            solution = solution.AddDocument(DocumentId.CreateNewId(project.Id), set.FileName, text, set.Document.Folders, set.FilePath);
        }

        return solution;
    }

    private static bool IsTaken(Project project, NewSet set)
    {
        var isTaken = project.Documents.Any(other => set.FilePath is not null
            ? String.Equals(other.FilePath, set.FilePath, StringComparison.OrdinalIgnoreCase)
            : other.FilePath is null && String.Equals(other.Name, set.FileName, StringComparison.OrdinalIgnoreCase));

        return isTaken;
    }

    /// <summary>
    /// The new set's file: its usings, its namespace, and the set without texts.
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
    /// A namespace with an <c>@</c> in front of each keyword part.
    /// </summary>
    private static string Escape(string @namespace)
    {
        var escaped = String.Join(".", @namespace.Split('.').Select(static part => SyntaxFacts.GetKeywordKind(part) is SyntaxKind.None ? part : $"@{part}"));
        return escaped;
    }

    /// <summary>
    /// Whether a global using of the project imports the package's namespace.
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
