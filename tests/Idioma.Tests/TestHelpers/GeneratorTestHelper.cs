using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using CodeChops.Idioma.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace CodeChops.Idioma.Tests.TestHelpers;

/// <summary>
/// Runs the generators over source text, against the framework and this package only.
/// </summary>
internal static class GeneratorTestHelper
{
    internal static CSharpParseOptions ParseOptions { get; } = new(LanguageVersion.Latest);

    internal static ImmutableArray<MetadataReference> References { get; } = CreateReferences();

    /// <summary>
    /// Runs one generator over <paramref name="source"/>.
    /// </summary>
    internal static GeneratorDriverRunResult Run(IIncrementalGenerator generator, string source, params MetadataReference[] references)
    {
        var driver = CreateDriver(generator);

        return driver.RunGenerators(CreateCompilation(references, source)).GetRunResult();
    }

    /// <summary>
    /// Runs one generator over several files of one compilation: a global using in a file of its own, say.
    /// </summary>
    internal static GeneratorDriverRunResult RunOverFiles(IIncrementalGenerator generator, params string[] sources)
    {
        return CreateDriver(generator).RunGenerators(CreateCompilation([], sources)).GetRunResult();
    }

    /// <summary>
    /// Runs one generator over <paramref name="source"/> compiled as an application rather than a library.
    /// </summary>
    internal static GeneratorDriverRunResult RunAsApplication(IIncrementalGenerator generator, string source, params MetadataReference[] references)
    {
        var compilation = CreateCompilation(references, source).WithOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        return CreateDriver(generator).RunGenerators(compilation).GetRunResult();
    }

    /// <summary>
    /// Runs one generator over <paramref name="source"/> with further files the build passes to generators as they are,
    /// as it passes Razor markup.
    /// </summary>
    internal static GeneratorDriverRunResult RunWithAdditionalFiles(IIncrementalGenerator generator, string source, params (string Path, string Text)[] files)
    {
        var driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: [.. files.Select(static file => (AdditionalText)new InMemoryAdditionalText(file.Path, file.Text))],
            parseOptions: ParseOptions);

        return driver.RunGenerators(CreateCompilation([], source)).GetRunResult();
    }

    /// <summary>
    /// Runs one generator over <paramref name="source"/> with properties of the project, each under the key a generator
    /// reads it by (<c>build_property.AndroidApplication</c>): what the build passes for the MSBuild properties a
    /// package makes visible to the compiler.
    /// </summary>
    internal static GeneratorDriverRunResult RunWithBuildProperties(IIncrementalGenerator generator, string source, MetadataReference[] references, params (string Key, string Value)[] properties)
    {
        var driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            parseOptions: ParseOptions,
            optionsProvider: new BuildProperties(properties));

        return driver.RunGenerators(CreateCompilation(references, source)).GetRunResult();
    }

    /// <summary>
    /// Runs one generator, then runs the same driver over a fresh parse of <paramref name="source"/> next to an
    /// unchanged file: the IDE edit that surfaces stale cached symbols.
    /// </summary>
    internal static GeneratorDriverRunResult RunAcrossEdit(IIncrementalGenerator generator, string source)
    {
        var compilation = CreateCompilation([], source, "namespace TestApp { public static class Anchor; }");
        var driver = CreateDriver(generator).RunGenerators(compilation);
        var edited = compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.First(), CSharpSyntaxTree.ParseText(source, ParseOptions));

        return driver.RunGenerators(edited).GetRunResult();
    }

    /// <summary>
    /// Runs one generator, edits a file unrelated to <paramref name="source"/>, and runs the same driver again. Returns
    /// why each output step ran the second time; every reason being <c>Cached</c> keeps an IDE from re-emitting source
    /// on every keystroke.
    /// </summary>
    internal static ImmutableArray<IncrementalStepRunReason> GetOutputReasonsAfterUnrelatedEdit(IIncrementalGenerator generator, string source)
    {
        var unrelated = CSharpSyntaxTree.ParseText("namespace Unrelated; internal sealed class Noise { public int A => 1; }", ParseOptions);
        var compilation = CreateCompilation([], source).AddSyntaxTrees(unrelated);
        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(
            unrelated,
            CSharpSyntaxTree.ParseText("namespace Unrelated; internal sealed class Noise { public int A => 1; public int B => 2; }", ParseOptions)));

        return
        [
            .. driver.GetRunResult().Results.Single().TrackedOutputSteps
                .SelectMany(static step => step.Value)
                .SelectMany(static step => step.Outputs)
                .Select(static output => output.Reason),
        ];
    }

    /// <summary>
    /// Runs one generator, edits the file of <paramref name="source"/> itself without moving or changing what it
    /// declares (a comment at its end), and runs the same driver again. Returns that second run, and the syntax tree
    /// the file has after the edit.
    /// </summary>
    internal static (GeneratorRunResult Run, SyntaxTree Edited) RunAcrossEditInSameFile(IIncrementalGenerator generator, string source)
    {
        var compilation = CreateCompilation([], source);
        var edited = CSharpSyntaxTree.ParseText(source + "\n// An edit that moves nothing.", ParseOptions);
        var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation);
        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.Single(), edited));

        return (driver.GetRunResult().Results.Single(), edited);
    }

    /// <summary>
    /// Why each output step of a run that wrote source ran: every reason being <c>Cached</c> means that the run wrote
    /// no source again, whatever it reported.
    /// </summary>
    internal static ImmutableArray<IncrementalStepRunReason> GetSourceOutputReasons(GeneratorRunResult run)
    {
        // An output step gives a pair: the sources it wrote, and the diagnostics it reported.
        return
        [
            .. run.TrackedOutputSteps
                .SelectMany(static step => step.Value)
                .SelectMany(static step => step.Outputs)
                .Where(static output => output.Value is ITuple { Length: 2 } written && written[0] is IEnumerable sources && sources.Cast<object>().Any())
                .Select(static output => output.Reason),
        ];
    }

    /// <summary>
    /// Runs the generators as a build does and returns every error of the resulting compilation, so that a test can pin
    /// that what they write compiles.
    /// </summary>
    internal static ImmutableArray<Diagnostic> CompileWithGenerators(string source, params MetadataReference[] references)
    {
        return
        [
            .. RunAllGenerators(source, references).Diagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error),
        ];
    }

    /// <summary>
    /// Runs the generators as a build does and returns what they report, each diagnostic as the build takes it: one that
    /// a <c>#pragma warning disable</c> or a <c>[SuppressMessage]</c> in the source silences comes back suppressed.
    /// </summary>
    internal static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(string source, params MetadataReference[] references)
    {
        CreateFullDriver(ParseOptions).RunGeneratorsAndUpdateCompilation(CreateCompilation(references, source), out _, out var diagnostics);

        return diagnostics;
    }

    /// <summary>
    /// Runs the generators as a build does that writes a documentation file, and returns the warnings about doc comments
    /// that name something that does not exist.
    /// </summary>
    internal static ImmutableArray<Diagnostic> GetUnresolvedDocReferences(string source)
    {
        var parseOptions = ParseOptions.WithDocumentationMode(DocumentationMode.Diagnose);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CreateFullDriver(parseOptions).RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

        return [.. updated.GetDiagnostics().Where(static diagnostic => diagnostic.Id is "CS1574" or "CS1584" or "CS1570")];
    }

    /// <summary>
    /// Runs the generators as a build does, then the analyzer, over <paramref name="source"/> and any further files by
    /// path: a file named as Razor's output, say.
    /// </summary>
    internal static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, params (string Path, string Text)[] files)
    {
        return AnalyzeAsync(new OneLanguageTextAnalyzer(), source, [], files);
    }

    /// <summary>
    /// Runs the generators as a build does, then <paramref name="analyzer"/>, over <paramref name="source"/>, compiled
    /// against further references, and any further files by path.
    /// </summary>
    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(DiagnosticAnalyzer analyzer, string source, MetadataReference[] references, params (string Path, string Text)[] files)
    {
        var trees = files.Select(static file => CSharpSyntaxTree.ParseText(file.Text, ParseOptions, file.Path));
        var compilation = CreateCompilation(references, source).AddSyntaxTrees(trees);

        CreateFullDriver(ParseOptions).RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

        return await updated
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>
    /// Compiles <paramref name="source"/>, with the generators run over it, into a library another compilation can
    /// reference: what the build of an assembly referencing it reads the languages from.
    /// </summary>
    internal static MetadataReference CompileLibraryWithGenerators(string assemblyName, string source, params MetadataReference[] references)
    {
        return MetadataReference.CreateFromImage(EmitLibraryWithGenerators(assemblyName, source, references));
    }

    /// <summary>
    /// Compiles <paramref name="source"/>, with the generators run over it, into the image of a library to load and
    /// run.
    /// </summary>
    internal static byte[] EmitLibraryWithGenerators(string assemblyName, string source, params MetadataReference[] references)
    {
        return EmitWithGenerators(assemblyName, source, OutputKind.DynamicallyLinkedLibrary, references);
    }

    /// <summary>
    /// Compiles <paramref name="source"/>, with the generators run over it, into an application another compilation
    /// can reference, as a Blazor Web App's server references its client. The source needs an entry point.
    /// </summary>
    internal static MetadataReference CompileApplicationWithGenerators(string assemblyName, string source, params MetadataReference[] references)
    {
        return MetadataReference.CreateFromImage(EmitWithGenerators(assemblyName, source, OutputKind.ConsoleApplication, references));
    }

    private static byte[] EmitWithGenerators(string assemblyName, string source, OutputKind outputKind, MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, ParseOptions)],
            [.. References, .. references],
            new CSharpCompilationOptions(outputKind));

        CreateFullDriver(ParseOptions).RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

        return EmitImage(updated);
    }

    /// <summary>
    /// Compiles <paramref name="source"/> into a library another compilation can reference, as an assembly of its own.
    /// </summary>
    internal static MetadataReference CompileLibrary(string assemblyName, string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, ParseOptions)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return Emit(compilation);
    }

    private static MetadataReference Emit(Compilation compilation)
    {
        return MetadataReference.CreateFromImage(EmitImage(compilation));
    }

    private static byte[] EmitImage(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        emitted.Success.ShouldBeTrue(String.Join(Environment.NewLine, emitted.Diagnostics));

        return stream.ToArray();
    }

    private static GeneratorDriver CreateDriver(IIncrementalGenerator generator)
    {
        return CSharpGeneratorDriver.Create(generators: [generator.AsSourceGenerator()], parseOptions: ParseOptions);
    }

    private static GeneratorDriver CreateFullDriver(CSharpParseOptions parseOptions)
    {
        return CSharpGeneratorDriver.Create(
            generators:
            [
                new LocalizableTextFactoryGenerator().AsSourceGenerator(),
                new TranslationSetsGenerator().AsSourceGenerator(),
                new TranslationsRegistrationGenerator().AsSourceGenerator(),
            ],
            parseOptions: parseOptions);
    }

    /// <summary>
    /// The generators' own errors and those of the resulting compilation together.
    /// </summary>
    private static (Compilation Compilation, ImmutableArray<Diagnostic> Diagnostics) RunAllGenerators(string source, MetadataReference[] references)
    {
        CreateFullDriver(ParseOptions).RunGeneratorsAndUpdateCompilation(CreateCompilation(references, source), out var updated, out var diagnostics);

        return (updated, [.. diagnostics, .. updated.GetDiagnostics()]);
    }

    private static CSharpCompilation CreateCompilation(MetadataReference[] references, params string[] sources)
    {
        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: sources.Select(static source => CSharpSyntaxTree.ParseText(source, ParseOptions)),
            references: [.. References, .. references],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// A file the build passes to generators as it is.
    /// </summary>
    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        private SourceText Text { get; } = SourceText.From(text);

        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => this.Text;
    }

    /// <summary>
    /// The properties of a project as the compiler's options hand them to a generator: the same for every file.
    /// </summary>
    private sealed class BuildProperties((string Key, string Value)[] properties) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(properties);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => this.GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => this.GlobalOptions;

        private sealed class Options((string Key, string Value)[] properties) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                foreach (var property in properties)
                {
                    if (String.Equals(property.Key, key, StringComparison.Ordinal))
                    {
                        value = property.Value;
                        return true;
                    }
                }

                value = "";
                return false;
            }
        }
    }

    /// <summary>
    /// The framework the tests run on, and this package.
    /// </summary>
    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(static path => Path.GetFileName(path) is var name && (name.StartsWith("System.", StringComparison.Ordinal) || name is "mscorlib.dll" or "netstandard.dll"));

        return
        [
            .. platform.Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path)),
            MetadataReference.CreateFromFile(typeof(ITranslations).Assembly.Location),
        ];
    }
}
