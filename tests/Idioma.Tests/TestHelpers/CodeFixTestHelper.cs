using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeChops.Idioma.Tests.TestHelpers;

/// <summary>
/// Offers a generator's diagnostics to a code fix, as an IDE does, and applies what it suggests.
/// </summary>
internal static class CodeFixTestHelper
{
    /// <summary>
    /// Runs <paramref name="generator"/> over <paramref name="sources"/>, each a file of one project, and offers its
    /// diagnostics to <paramref name="provider"/>. Returns every suggested fix by its title, with the files as the fix
    /// leaves them, in the order given.
    /// </summary>
    internal static async Task<ImmutableArray<(string Title, ImmutableArray<string> Sources)>> FixAsync(CodeFixProvider provider, IIncrementalGenerator generator, params string[] sources)
    {
        using var workspace = new AdhocWorkspace();

        var project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            name: "TestAssembly",
            assemblyName: "TestAssembly",
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            parseOptions: GeneratorTestHelper.ParseOptions,
            metadataReferences: GeneratorTestHelper.References));

        // A file path each: a generator's diagnostic names its file by path, which is how an IDE finds the document.
        var documentIds = new List<DocumentId>();
        foreach (var source in sources)
        {
            var name = $"File{documentIds.Count}.cs";
            var document = project.AddDocument(name, source, filePath: $"/TestAssembly/{name}");

            project = document.Project;
            documentIds.Add(document.Id);
        }

        var compilation = await project.GetCompilationAsync();
        var diagnostics = CSharpGeneratorDriver
            .Create(generators: [generator.AsSourceGenerator()], parseOptions: GeneratorTestHelper.ParseOptions)
            .RunGenerators(compilation!)
            .GetRunResult()
            .Diagnostics;

        var actions = new List<CodeAction>();
        foreach (var diagnostic in diagnostics.Where(diagnostic => provider.FixableDiagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal)))
        {
            var path = diagnostic.Location.GetLineSpan().Path;
            var document = project.Documents.Single(document => String.Equals(document.FilePath, path, StringComparison.Ordinal));
            var context = new CodeFixContext(document, diagnostic.Location.SourceSpan, [diagnostic], (action, _) => actions.Add(action), CancellationToken.None);

            await provider.RegisterCodeFixesAsync(context);
        }

        var fixes = ImmutableArray.CreateBuilder<(string, ImmutableArray<string>)>();
        foreach (var action in actions)
        {
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

            var texts = ImmutableArray.CreateBuilder<string>();
            foreach (var documentId in documentIds)
                texts.Add((await solution.GetDocument(documentId)!.GetTextAsync()).ToString());

            fixes.Add((action.Title, texts.ToImmutable()));
        }

        return fixes.ToImmutable();
    }
}
