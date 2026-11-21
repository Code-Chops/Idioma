using System.Text.RegularExpressions;

namespace Benchmarks;

/// <summary>
/// The download sizes of Idioma.Benchmarks.Size, from five publishes: the app without localization, and the app with
/// each approach, once with English only and once with Dutch too.
/// </summary>
/// <remarks>
/// An approach costs what its publish downloads beyond the app without localization, which shows the same 100 texts as
/// English strings. That counts everything the approach brings: its own assemblies, its texts and generated code in the
/// app's assembly, and what it keeps of .NET's own assemblies, which Blazor otherwise trims. A language costs what the
/// publish with Dutch downloads beyond the one without. With AOT, code also becomes native code in dotnet.native.wasm.
/// That file is several megabytes, and Brotli's result for it moves by kilobytes when a little of it changes, so it's
/// left out of the other rows, and its growth is given before and after compression.
/// </remarks>
internal static partial class SizeReport
{
    private const string AppPrefix = "Idioma.Benchmarks.Size.";
    private const string NativePrefix = "dotnet.native.";

    // Native code that grows by less is the noise of relinking, as with the interpreter.
    private const long NativeShown = 1024;

    // A file that grows by less isn't listed by name.
    private const long FileShown = 512;

    private static readonly (string Name, string Label)[] Approaches =
    [
        ("localizer", "resx and IStringLocalizer"),
        ("idioma", "Idioma"),
    ];

    /// <summary>
    /// Prints the tables of the publishes in <paramref name="root"/>, named <c>none</c>, <c>idioma-one</c>,
    /// <c>idioma-two</c>, <c>localizer-one</c> and <c>localizer-two</c>, each followed by <paramref name="suffix"/>.
    /// </summary>
    public static void Print(string root, string suffix, string label)
    {
        var none = Download.Of(Path.Combine(root, $"none{suffix}", "wwwroot"));
        var publishes = Approaches.Select(approach => (
                One: Download.Of(Path.Combine(root, $"{approach.Name}-one{suffix}", "wwwroot")),
                Two: Download.Of(Path.Combine(root, $"{approach.Name}-two{suffix}", "wwwroot"))))
            .ToArray();

        Console.WriteLine($"Download sizes, {label}, compressed with Brotli as a server sends them. Each approach is measured against the same app without localization, which shows the 100 texts as English strings:");
        Console.WriteLine();
        Console.WriteLine($"| What's downloaded | {string.Join(" | ", Approaches.Select(approach => approach.Label))} |");
        Console.WriteLine($"|---|{string.Concat(Approaches.Select(_ => "---:|"))}");

        Row("The approach, with 100 texts in the default language", publish => publish.One.Total - none.Total);
        Row("Of that, its own assemblies", publish => publish.One.Library - none.Library);
        Row("Of that, the app's assembly", publish => publish.One.App - none.App);
        Row("Of that, .NET's own assemblies", publish => publish.One.DotNet - none.DotNet);
        Row("Of that, the rest, such as dotnet.js", publish => publish.One.Rest - none.Rest);
        Row("Each extra language of 100 texts", publish => publish.Two.Total - publish.One.Total);

        if (publishes.Any(publish => Math.Abs(publish.One.Native - none.Native) >= NativeShown
                || Math.Abs(publish.Two.Native - publish.One.Native) >= NativeShown))
        {
            Row("The approach as native code in dotnet.native.wasm, before compression", publish => publish.One.Native - none.Native);
            Row("The approach as native code, compressed", publish => publish.One.NativeCompressed - none.NativeCompressed);
            Row("Each extra language as native code, before compression", publish => publish.Two.Native - publish.One.Native);
            Row("Each extra language as native code, compressed", publish => publish.Two.NativeCompressed - publish.One.NativeCompressed);
        }

        // The files behind the first row, so that a size can be traced to the assembly it comes from.
        var files = publishes
            .SelectMany(publish => publish.One.Files.Keys)
            .Concat(none.Files.Keys)
            .Distinct()
            .Where(file => publishes.Any(publish => Math.Abs(Growth(publish.One, file)) >= FileShown))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Console.WriteLine();
        Console.WriteLine($"The files that grow by {FileShown / 1024.0:F1} KB or more, with 100 texts in the default language:");
        Console.WriteLine();
        Console.WriteLine($"| File | {string.Join(" | ", Approaches.Select(approach => approach.Label))} |");
        Console.WriteLine($"|---|{string.Concat(Approaches.Select(_ => "---:|"))}");

        foreach (var file in files)
            Console.WriteLine($"| {file} | {string.Join(" | ", publishes.Select(publish => Kilobytes(Growth(publish.One, file))))} |");

        void Row(string what, Func<(Download One, Download Two), long> size) =>
            Console.WriteLine($"| {what} | {string.Join(" | ", publishes.Select(publish => Kilobytes(size(publish))))} |");

        long Growth(Download download, string file) => download.Files.GetValueOrDefault(file) - none.Files.GetValueOrDefault(file);
    }

    private static string Kilobytes(long bytes) => $"{bytes / 1024.0:F1} KB";

    /// <summary>
    /// What a publish downloads from its _framework folder, compressed, per file without its fingerprint.
    /// </summary>
    private sealed partial record Download(Dictionary<string, long> Files, long Native, long NativeCompressed)
    {
        public long Library => this.Sum(IsLibrary);
        public long App => this.Sum(file => !IsLibrary(file) && IsApp(file));
        public long DotNet => this.Sum(file => !IsLibrary(file) && !IsApp(file) && file.EndsWith(".wasm", StringComparison.Ordinal));
        public long Rest => this.Total - this.Library - this.App - this.DotNet;
        public long Total => this.Sum(_ => true);

        public static Download Of(string wwwroot)
        {
            var framework = Path.Combine(wwwroot, "_framework");
            var files = new Dictionary<string, long>(StringComparer.Ordinal);
            long native = 0, nativeCompressed = 0;

            foreach (var file in Directory.GetFiles(framework, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                var size = new FileInfo(file).Length;

                if (name.StartsWith(NativePrefix, StringComparison.Ordinal))
                {
                    if (name.EndsWith(".wasm", StringComparison.Ordinal))
                        native += size;
                    else if (name.EndsWith(".wasm.br", StringComparison.Ordinal))
                        nativeCompressed += size;

                    continue;
                }

                if (!name.EndsWith(".br", StringComparison.Ordinal))
                    continue;

                // A satellite assembly keeps the folder of its language, as in nl/Idioma.Benchmarks.Size.resources.wasm.
                var path = Path.GetRelativePath(framework, Path.Combine(Path.GetDirectoryName(file)!, Fingerprint().Replace(name[..^".br".Length], ".")));
                files[path] = files.GetValueOrDefault(path) + size;
            }

            return new Download(files, native, nativeCompressed);
        }

        private long Sum(Func<string, bool> belongs) => this.Files.Where(file => belongs(file.Key)).Sum(file => file.Value);

        private static bool IsLibrary(string file) =>
            file.StartsWith("CodeChops.Idioma.", StringComparison.Ordinal) || file.StartsWith("Microsoft.Extensions.Localization", StringComparison.Ordinal);

        // A satellite assembly sits in a folder of its language, and belongs to the app.
        private static bool IsApp(string file) => file.Contains(Path.DirectorySeparatorChar) || file.StartsWith(AppPrefix, StringComparison.Ordinal);

        // The fingerprint a publish puts before the extension, as in CodeChops.Idioma.abc123xyz0.wasm.
        [GeneratedRegex(@"\.[a-z0-9]{10}\.(?=[a-z]+$)")]
        private static partial Regex Fingerprint();
    }
}
