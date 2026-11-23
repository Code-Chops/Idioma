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
    /// reads generated code, such as the factory <c>LocalizableText.Inline</c>. The workspace the fix sees has no
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

    private static async Task<(ImmutableArray<string> Sources, ImmutableArray<(string Path, string Text)> Added)> FixAllAsync(
        CodeFixProvider provider, Func<Compilation, Task<ImmutableArray<Diagnostic>>> diagnose, MetadataReference[] references, string[] sources)
    {
        using var workspace = new AdhocWorkspace();
        var (project, documentIds) = CreateProject(workspace, sources, references);
        var compilation = (await project.GetCompilationAsync())!;

        var diagnostics = (await diagnose(compilation))
            .Where(diagnostic => provider.FixableDiagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .ToImmutableArray();

        project = AddGeneratedCode(project, compilation);
        var before = project.DocumentIds.ToHashSet();

        // The fix all applies is the one with the key of the first fix offered, as in an IDE. A diagnostic may have
        // none, as a placeholder in a raw string has.
        var document = FindDocument(project, diagnostics.First());
        var actions = new List<CodeAction>();

        foreach (var diagnostic in diagnostics)
        {
            document = FindDocument(project, diagnostic);
            await provider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic.Location.SourceSpan, [diagnostic], (action, _) => actions.Add(action), CancellationToken.None));

            if (actions.Count > 0)
                break;
        }

        var context = new FixAllContext(document, provider, FixAllScope.Solution, actions[0].EquivalenceKey, provider.FixableDiagnosticIds,
            new FixedDiagnostics(diagnostics), CancellationToken.None);
        var fix = await provider.GetFixAllProvider()!.GetFixAsync(context);

        var operations = await fix!.GetOperationsAsync(CancellationToken.None);
        var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        var texts = ImmutableArray.CreateBuilder<string>();
        foreach (var documentId in documentIds)
            texts.Add((await solution.GetDocument(documentId)!.GetTextAsync()).ToString());

        var added = ImmutableArray.CreateBuilder<(string, string)>();
        foreach (var addedDocument in solution.GetProject(project.Id)!.Documents.Where(document => !before.Contains(document.Id)))
            added.Add((addedDocument.FilePath ?? addedDocument.Name, (await addedDocument.GetTextAsync()).ToString()));

        return (texts.ToImmutable(), added.ToImmutable());
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
        var (project, documentIds) = CreateProject(workspace, sources, references ?? []);

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

    private static (Project Project, List<DocumentId> DocumentIds) CreateProject(AdhocWorkspace workspace, string[] sources, MetadataReference[] references)
    {
        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            name: "TestAssembly",
            assemblyName: "TestAssembly",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            parseOptions: GeneratorTestHelper.ParseOptions,
            metadataReferences: [.. GeneratorTestHelper.References, .. references]));

        // A file path each: a generator's diagnostic names its file by path, which is how an IDE finds the document.
        var documentIds = new List<DocumentId>();
        foreach (var source in sources)
        {
            var name = $"File{documentIds.Count}.cs";
            var document = project.AddDocument(name, source, filePath: $"/TestAssembly/{name}");

            project = document.Project;
            documentIds.Add(document.Id);
        }

        return (project, documentIds);
    }

    /// <summary>
    /// Hands "Fix all" the diagnostics an analyzer reported, as an IDE hands it those it has.
    /// </summary>
    private sealed class FixedDiagnostics(ImmutableArray<Diagnostic> diagnostics) : FixAllContext.DiagnosticProvider
    {
        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            return Task.FromResult(diagnostics.Where(diagnostic => String.Equals(diagnostic.Location.GetLineSpan().Path, document.FilePath, StringComparison.Ordinal)));
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<Diagnostic>());
        }

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
        }
    }
}
