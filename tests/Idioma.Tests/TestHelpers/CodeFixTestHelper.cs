using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeChops.Idioma.Tests.TestHelpers;

/// <summary>
/// Offers a generator's or an analyzer's diagnostics to a code fix, as an IDE does, and applies what it suggests.
/// </summary>
internal static class CodeFixTestHelper
{
    /// <summary>
    /// Runs <paramref name="generator"/> over <paramref name="sources"/>, each a file of one project, and offers its
    /// diagnostics to <paramref name="provider"/>. Returns every suggested fix by its title, with the files as the fix
    /// leaves them, in the order given.
    /// </summary>
    internal static async Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(CodeFixProvider provider,
        IIncrementalGenerator generator, params string[] sources)
    {
        var fixes = await FixWithAddedFilesAsync(provider, generator, sources);
        return [.. fixes.Select(static fix => (fix.Title, fix.Sources))];
    }

    /// <summary>
    /// Runs <paramref name="generator"/> over <paramref name="sources"/> as <see cref="FixAsync(CodeFixProvider, IIncrementalGenerator, string[])"/>
    /// does, and also returns each file a fix adds, by its path and with its text.
    /// </summary>
    internal static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources, ImmutableArray<(string Path, string Text)> Added)>> FixWithAddedFilesAsync(
        CodeFixProvider provider, IIncrementalGenerator generator, params string[] sources)
    {
        return FixWithAddedFilesAsync(provider, generator, [], sources);
    }

    /// <summary>
    /// Runs <paramref name="generator"/> as <see cref="FixWithAddedFilesAsync(CodeFixProvider, IIncrementalGenerator, string[])"/>
    /// does, over files compiled against further references, such as a library whose contract lacks a language.
    /// </summary>
    internal static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources, ImmutableArray<(string Path, string Text)> Added)>> FixWithAddedFilesAsync(
        CodeFixProvider provider, IIncrementalGenerator generator, MetadataReference[] references, params string[] sources)
    {
        var result = FixAsync(provider, Generate(generator), sources, references);
        return result;
    }

    /// <summary>
    /// Runs <paramref name="generator"/> over <paramref name="sources"/>, and applies the fix of
    /// <paramref name="provider"/> to every diagnostic of the solution at once, as "Fix all in solution" does in an IDE.
    /// Returns the files as the fix leaves them, in the order given, and each file it adds, by its path and with its
    /// text.
    /// </summary>
    internal static Task<(ImmutableArray<string> Sources, ImmutableArray<(string Path, string Text)> Added)> FixAllWithAddedFilesAsync(
        CodeFixProvider provider, IIncrementalGenerator generator, MetadataReference[] references, params string[] sources)
    {
        return FixAllAsync(provider, Generate(generator), references, sources);
    }

    /// <summary>
    /// Runs <paramref name="generator"/> as <see cref="FixAsync(CodeFixProvider, IIncrementalGenerator, string[])"/>
    /// does, but the workspace the fix sees holds the generated code too, as an IDE's does. For a fix that reads the
    /// generated contract of a set.
    /// </summary>
    internal static async Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixWithGeneratedCodeAsync(CodeFixProvider provider,
        IIncrementalGenerator generator, params string[] sources)
    {
        var fixes = await FixAsync(provider, Generate(generator), sources, withGeneratedCode: true);
        return [.. fixes.Select(static fix => (fix.Title, fix.Sources))];
    }

    private static Func<Compilation, Task<ImmutableArray<Diagnostic>>> Generate(IIncrementalGenerator generator)
    {
        return compilation => Task.FromResult(CSharpGeneratorDriver
            .Create(generators: [generator.AsSourceGenerator()], parseOptions: GeneratorTestHelper.ParseOptions)
            .RunGenerators(compilation)
            .GetRunResult()
            .Diagnostics);
    }

    /// <summary>
    /// Runs <paramref name="analyzer"/> over <paramref name="sources"/>, each a file of one project, without the
    /// generators, and offers its diagnostics to <paramref name="provider"/>. Returns every suggested fix by its title,
    /// with the files as the fix leaves them, in the order given.
    /// </summary>
    internal static async Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(CodeFixProvider provider,
        DiagnosticAnalyzer analyzer, params string[] sources)
    {
        var fixes = await FixAsync(provider, compilation => compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(), sources);
        return [.. fixes.Select(static fix => (fix.Title, fix.Sources))];
    }

    /// <summary>
    /// Runs every generator as a build does, then <paramref name="analyzer"/>, over <paramref name="sources"/>, each a
    /// file of one project, and offers the analyzer's diagnostics to <paramref name="provider"/>. For an analyzer that
    /// reads generated code, such as the factory <c>LocalizedText.Inline</c>. The workspace the fix sees has no
    /// generated code. Returns every suggested fix by its title, with the files as the fix leaves them, in the order
    /// given.
    /// </summary>
    internal static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAfterGeneratorsAsync(CodeFixProvider provider,
        DiagnosticAnalyzer analyzer, params string[] sources)
    {
        return FixAfterGeneratorsAsync(provider, analyzer, [], sources);
    }

    /// <summary>
    /// Runs every generator, then <paramref name="analyzer"/>, as <see cref="FixAfterGeneratorsAsync(CodeFixProvider, DiagnosticAnalyzer, string[])"/>
    /// does, over files compiled against further references, such as a library whose contract the fix reads.
    /// </summary>
    internal static async Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAfterGeneratorsAsync(CodeFixProvider provider,
        DiagnosticAnalyzer analyzer, MetadataReference[] references, params string[] sources)
    {
        var fixes = await FixAsync(provider, AnalyzeAfterGenerators(analyzer), sources, references);
        return [.. fixes.Select(static fix => (fix.Title, fix.Sources))];
    }

    /// <summary>
    /// Runs every generator, then <paramref name="analyzer"/>, as <see cref="FixAfterGeneratorsAsync(CodeFixProvider, DiagnosticAnalyzer, string[])"/>
    /// does, but the workspace the fix sees holds the generated code too, as an IDE's does. For a fix that reads the
    /// generated contract of a set.
    /// </summary>
    internal static Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixWithGeneratedCodeAsync(CodeFixProvider provider,
        DiagnosticAnalyzer analyzer, params string[] sources)
    {
        return FixWithGeneratedCodeAsync(provider, analyzer, [], sources);
    }

    /// <summary>
    /// Runs every generator, then <paramref name="analyzer"/>, as <see cref="FixWithGeneratedCodeAsync(CodeFixProvider, DiagnosticAnalyzer, string[])"/>
    /// does, over files compiled against further references, such as a project of the same solution, which an IDE
    /// references by its compilation.
    /// </summary>
    internal static async Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixWithGeneratedCodeAsync(CodeFixProvider provider,
        DiagnosticAnalyzer analyzer, MetadataReference[] references, params string[] sources)
    {
        var fixes = await FixAsync(provider, AnalyzeAfterGenerators(analyzer), sources, references, withGeneratedCode: true);
        return [.. fixes.Select(static fix => (fix.Title, fix.Sources))];
    }

    /// <summary>
    /// Runs every generator, then <paramref name="analyzer"/>, over <paramref name="sources"/>, and applies the fix of
    /// <paramref name="provider"/> to every diagnostic of the solution at once, as "Fix all in solution" does in an IDE.
    /// The workspace the fix sees holds the generated code too, as an IDE's does. Returns the files as the fix leaves
    /// them, in the order given.
    /// </summary>
    internal static async Task<ImmutableArray<string>> FixAllAfterGeneratorsAsync(CodeFixProvider provider, DiagnosticAnalyzer analyzer, params string[] sources)
    {
        var (fixedSources, _) = await FixAllAsync(provider, AnalyzeAfterGenerators(analyzer), [], sources);
        return fixedSources;
    }

    /// <summary>
    /// Runs <paramref name="generator"/> over <paramref name="sources"/>, each a file of one project, and applies the
    /// fix of <paramref name="provider"/> to every diagnostic of a scope at once: of the file at
    /// <paramref name="fromFile"/>, of its project, or of the solution. Returns the files as the fix leaves them, in
    /// the order given.
    /// </summary>
    internal static async Task<ImmutableArray<string>> FixAllAsync(CodeFixProvider provider, IIncrementalGenerator generator, FixAllScope scope, int fromFile,
        params string[] sources)
    {
        var (projects, _) = await FixAllAsync(provider, Generate(generator), scope, (0, fromFile), [], [new TestProject(DefaultProjectName, sources)]);
        return projects[0];
    }

    /// <summary>
    /// Runs every generator, then <paramref name="analyzer"/>, over <paramref name="sources"/>, each a file of one
    /// project, and applies the fix of <paramref name="provider"/> to every diagnostic of a scope at once: of the file
    /// at <paramref name="fromFile"/>, of its project, or of the solution. Returns the files as the fix leaves them, in
    /// the order given.
    /// </summary>
    internal static async Task<ImmutableArray<string>> FixAllAfterGeneratorsAsync(CodeFixProvider provider, DiagnosticAnalyzer analyzer, FixAllScope scope, int fromFile,
        params string[] sources)
    {
        var (projects, _) = await FixAllAsync(provider, AnalyzeAfterGenerators(analyzer), scope, (0, fromFile), [], [new TestProject(DefaultProjectName, sources)]);
        return projects[0];
    }

    /// <summary>
    /// Runs <paramref name="generator"/> over each of <paramref name="projects"/>, which form one solution, and applies
    /// the fix of <paramref name="provider"/> to every diagnostic of a scope at once, as an IDE does when "Fix all" is
    /// chosen in the file at <paramref name="from"/>: of that file, of its project, or of the solution. Returns the
    /// files of each project as the fix leaves them, in the order given.
    /// </summary>
    internal static async Task<ImmutableArray<ImmutableArray<string>>> FixAllAsync(CodeFixProvider provider, IIncrementalGenerator generator, FixAllScope scope,
        (int Project, int File) from, params TestProject[] projects)
    {
        var (fixedProjects, _) = await FixAllAsync(provider, Generate(generator), scope, from, [], projects);
        return fixedProjects;
    }

    /// <summary>
    /// Runs every generator, then <paramref name="analyzer"/>, over each of <paramref name="projects"/>, which form one
    /// solution, and applies the fix of <paramref name="provider"/> to every diagnostic of a scope at once, as
    /// <see cref="FixAllAsync(CodeFixProvider, IIncrementalGenerator, FixAllScope, ValueTuple{int, int}, TestProject[])"/>
    /// does.
    /// </summary>
    internal static async Task<ImmutableArray<ImmutableArray<string>>> FixAllAfterGeneratorsAsync(CodeFixProvider provider, DiagnosticAnalyzer analyzer,
        FixAllScope scope, (int Project, int File) from, params TestProject[] projects)
    {
        var (fixedProjects, _) = await FixAllAsync(provider, AnalyzeAfterGenerators(analyzer), scope, from, [], projects);
        return fixedProjects;
    }

    /// <summary>
    /// "Fix all" in the solution of one project, started from the first diagnostic that has a fix.
    /// </summary>
    private static async Task<(ImmutableArray<string> Sources, ImmutableArray<(string Path, string Text)> Added)> FixAllAsync(
        CodeFixProvider provider, Func<Compilation, Task<ImmutableArray<Diagnostic>>> diagnose, MetadataReference[] references, string[] sources)
    {
        var (projects, added) = await FixAllAsync(provider, diagnose, FixAllScope.Solution, from: null, references, [new TestProject(DefaultProjectName, sources)]);
        return (projects[0], added);
    }

    /// <summary>
    /// "Fix all" in a scope, started from the first diagnostic that has a fix in the file at <paramref name="from"/>,
    /// or in any file when that is <see langword="null"/>.
    /// </summary>
    private static async Task<(ImmutableArray<ImmutableArray<string>> Projects, ImmutableArray<(string Path, string Text)> Added)> FixAllAsync(
        CodeFixProvider provider, Func<Compilation, Task<ImmutableArray<Diagnostic>>> diagnose, FixAllScope scope, (int Project, int File)? from,
        MetadataReference[] references, TestProject[] projects)
    {
        using var workspace = new AdhocWorkspace();
        var (solution, created) = CreateSolution(workspace, projects, references);
        var diagnostics = new Dictionary<ProjectId, ImmutableArray<Diagnostic>>();

        // In the order given, so that a project sees the generated code of each project it references.
        foreach (var (projectId, _) in created)
        {
            var project = solution.GetProject(projectId)!;
            var compilation = (await project.GetCompilationAsync())!;

            diagnostics.Add(projectId, [.. (await diagnose(compilation)).Where(diagnostic => provider.FixableDiagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))]);
            solution = AddGeneratedCode(project, compilation).Solution;
        }

        var before = solution.Projects.SelectMany(static project => project.DocumentIds).ToHashSet();

        // The fix all applies is the one with the key of the first fix offered, as in an IDE. A diagnostic may have
        // none, as a placeholder in a raw string has.
        var fromDocument = from is var (fromProject, fromFile) ? created[fromProject].DocumentIds[fromFile] : null;
        var actions = new List<CodeAction>();
        Document? document = null;

        foreach (var (projectId, _) in created)
        {
            foreach (var diagnostic in diagnostics[projectId])
            {
                var candidate = FindDocument(solution.GetProject(projectId)!, diagnostic);

                if (actions.Count > 0 || (fromDocument is not null && candidate.Id != fromDocument))
                    continue;

                await provider.RegisterCodeFixesAsync(new CodeFixContext(candidate, diagnostic.Location.SourceSpan, [diagnostic], (action, _) => actions.Add(action), CancellationToken.None));
                document = candidate;
            }
        }

        if (document is null || actions.Count is 0)
            throw new InvalidOperationException("No diagnostic has a fix where \"Fix all\" starts, so an IDE offers none there.");

        var diagnosticProvider = new FixedDiagnostics(diagnostics);
        var key = actions[0].EquivalenceKey;

        var context = scope is FixAllScope.Project
            ? new FixAllContext(document.Project, provider, scope, key, provider.FixableDiagnosticIds, diagnosticProvider, CancellationToken.None)
            : new FixAllContext(document, provider, scope, key, provider.FixableDiagnosticIds, diagnosticProvider, CancellationToken.None);

        var fix = await provider.GetFixAllProvider()!.GetFixAsync(context);

        var operations = await fix!.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        var fixedProjects = ImmutableArray.CreateBuilder<ImmutableArray<string>>();
        foreach (var (_, documentIds) in created)
        {
            var texts = ImmutableArray.CreateBuilder<string>();
            foreach (var documentId in documentIds)
                texts.Add((await changed.GetDocument(documentId)!.GetTextAsync()).ToString());

            fixedProjects.Add(texts.ToImmutable());
        }

        var added = ImmutableArray.CreateBuilder<(string, string)>();
        foreach (var addedDocument in changed.Projects.SelectMany(static project => project.Documents).Where(document => !before.Contains(document.Id)))
            added.Add((addedDocument.FilePath ?? addedDocument.Name, (await addedDocument.GetTextAsync()).ToString()));

        return (fixedProjects.ToImmutable(), added.ToImmutable());
    }

    /// <summary>
    /// The project with what the generators write for it as files of its own. An IDE's workspace holds the generated
    /// code, so a fix there reads the generated contract of a set.
    /// </summary>
    private static Project AddGeneratedCode(Project project, Compilation compilation)
    {
        GeneratorTestHelper.CreateFullDriver(GeneratorTestHelper.ParseOptions).RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

        foreach (var tree in updated.SyntaxTrees.Where(tree => !compilation.ContainsSyntaxTree(tree)))
            project = project.AddDocument(Path.GetFileName(tree.FilePath), tree.GetText(), filePath: tree.FilePath).Project;

        return project;
    }

    /// <summary>
    /// The document a diagnostic lies in, found by its path, as an IDE finds it.
    /// </summary>
    private static Document FindDocument(Project project, Diagnostic diagnostic)
    {
        var path = diagnostic.Location.GetLineSpan().Path;
        return project.Documents.Single(document => String.Equals(document.FilePath, path, StringComparison.Ordinal));
    }

    private static Func<Compilation, Task<ImmutableArray<Diagnostic>>> AnalyzeAfterGenerators(DiagnosticAnalyzer analyzer)
    {
        return compilation =>
        {
            GeneratorTestHelper.CreateFullDriver(GeneratorTestHelper.ParseOptions).RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

            return updated.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
        };
    }

    private static async Task<ImmutableArray<(string Title, ImmutableArray<string> Sources, ImmutableArray<(string Path, string Text)> Added)>> FixAsync(
        CodeFixProvider provider, Func<Compilation, Task<ImmutableArray<Diagnostic>>> diagnose, string[] sources, MetadataReference[]? references = null,
        bool withGeneratedCode = false)
    {
        using var workspace = new AdhocWorkspace();
        var (created, projects) = CreateSolution(workspace, [new TestProject(DefaultProjectName, sources)], references ?? []);
        var (projectId, documentIds) = projects[0];
        var project = created.GetProject(projectId)!;

        var compilation = await project.GetCompilationAsync();
        var diagnostics = await diagnose(compilation!);

        if (withGeneratedCode)
            project = AddGeneratedCode(project, compilation!);

        var before = project.DocumentIds.ToHashSet();

        var actions = new List<CodeAction>();
        foreach (var diagnostic in diagnostics.Where(diagnostic => provider.FixableDiagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal)))
        {
            var path = diagnostic.Location.GetLineSpan().Path;
            var document = project.Documents.Single(document => String.Equals(document.FilePath, path, StringComparison.Ordinal));
            var context = new CodeFixContext(document, diagnostic.Location.SourceSpan, [diagnostic], (action, _) => actions.Add(action), CancellationToken.None);

            await provider.RegisterCodeFixesAsync(context);
        }

        var fixes = ImmutableArray.CreateBuilder<(string, ImmutableArray<string>, ImmutableArray<(string, string)>)>();
        foreach (var action in actions)
        {
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

            var texts = ImmutableArray.CreateBuilder<string>();
            foreach (var documentId in documentIds)
                texts.Add((await solution.GetDocument(documentId)!.GetTextAsync()).ToString());

            var added = ImmutableArray.CreateBuilder<(string, string)>();
            foreach (var document in solution.GetProject(project.Id)!.Documents.Where(document => !before.Contains(document.Id)))
                added.Add((document.FilePath ?? document.Name, (await document.GetTextAsync()).ToString()));

            fixes.Add((action.Title, texts.ToImmutable(), added.ToImmutable()));
        }

        return fixes.ToImmutable();
    }

    /// <summary>
    /// The name of the project of a test that has one.
    /// </summary>
    private const string DefaultProjectName = "TestAssembly";

    /// <summary>
    /// A solution of the given projects, with the files of each. A project references the projects it names by their
    /// compilation, as an IDE does, so each of them stands before it.
    /// </summary>
    private static (Solution Solution, List<(ProjectId ProjectId, List<DocumentId> DocumentIds)> Projects) CreateSolution(AdhocWorkspace workspace,
        TestProject[] projects, MetadataReference[] references)
    {
        var solution = workspace.CurrentSolution;
        var created = new List<(ProjectId ProjectId, List<DocumentId> DocumentIds)>();
        var projectIds = new Dictionary<string, ProjectId>(StringComparer.Ordinal);

        foreach (var project in projects)
        {
            var projectId = ProjectId.CreateNewId();

            solution = solution.AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                name: project.Name,
                assemblyName: project.Name,
                LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                parseOptions: GeneratorTestHelper.ParseOptions,
                projectReferences: project.References.Select(name => new ProjectReference(projectIds[name])),
                metadataReferences: [.. GeneratorTestHelper.References, .. references]));

            // A file path each: a generator's diagnostic names its file by path, which is how an IDE finds the document.
            var documentIds = new List<DocumentId>();
            foreach (var source in project.Sources)
            {
                var name = $"File{documentIds.Count}.cs";
                var documentId = DocumentId.CreateNewId(projectId);

                solution = solution.AddDocument(documentId, name, source, filePath: $"/{project.Name}/{name}");
                documentIds.Add(documentId);
            }

            projectIds.Add(project.Name, projectId);
            created.Add((projectId, documentIds));
        }

        return (solution, created);
    }

    /// <summary>
    /// Hands "Fix all" the diagnostics reported for each project, as an IDE hands it those of the document or the
    /// project it asks for.
    /// </summary>
    private sealed class FixedDiagnostics(IReadOnlyDictionary<ProjectId, ImmutableArray<Diagnostic>> diagnostics) : FixAllContext.DiagnosticProvider
    {
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            return Task.FromResult(Of(document.Project).Where(diagnostic => diagnostic.Location.IsInSource
                                                                            && String.Equals(diagnostic.Location.GetLineSpan().Path, document.FilePath, StringComparison.Ordinal)));
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return Task.FromResult(Of(project).Where(static diagnostic => !diagnostic.Location.IsInSource));
        }

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return Task.FromResult(Of(project));
        }

        private IEnumerable<Diagnostic> Of(Project project) => diagnostics.TryGetValue(project.Id, out var found) ? found : [];
    }
}

/// <summary>
/// A project of the solution a code fix test builds: its name, which is its assembly's too, its files, and the names of
/// the projects it references.
/// </summary>
internal sealed class TestProject(string name, string[] sources, params string[] references)
{
    public string Name { get; } = name;

    public string[] Sources { get; } = sources;

    public string[] References { get; } = references;
}
