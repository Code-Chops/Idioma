namespace Benchmarks;

/// <summary>
/// The download sizes of Idioma.Benchmarks.Size, from two publishes: one with English only, and one with Dutch too.
/// </summary>
/// <remarks>
/// A language of 100 texts costs IStringLocalizer a satellite assembly, which is measured as it is. It costs Idioma a set
/// in the app's assembly, so it's what that assembly grows by. The rest of the download is left out, such as the boot
/// manifest, which lists the satellite assembly as well. With AOT, the set is native code in dotnet.native.wasm too. That
/// file is several megabytes, and Brotli's result for it moves by kilobytes when a little of it changes, so its growth is
/// given before compression.
/// </remarks>
internal static class SizeReport
{
    private const string App = "Idioma.Benchmarks.Size.";
    private const string Native = "dotnet.native.";

    public static void Print(string oneLanguage, string twoLanguages, string label)
    {
        var one = Framework(oneLanguage);
        var two = Framework(twoLanguages);

        var localization = Size(two, "Microsoft.Extensions.Localization", ".br");
        var satellite = Size(Directory.GetFiles(Path.Combine(twoLanguages, "_framework", "nl")), "", ".br");
        var idioma = Size(two, "CodeChops.Idioma.", ".br");
        var set = Size(two, App, ".wasm.br") - Size(one, App, ".wasm.br");
        var native = Size(two, Native, ".wasm") - Size(one, Native, ".wasm");

        Console.WriteLine($"Download sizes, {label}, compressed with Brotli as a server sends them:");
        Console.WriteLine();
        Console.WriteLine("| What's downloaded | Size |");
        Console.WriteLine("|---|---:|");
        Console.WriteLine($"| IStringLocalizer: Microsoft.Extensions.Localization and its abstractions | {Kilobytes(localization)} |");
        Console.WriteLine($"| IStringLocalizer: a language of 100 texts, as a satellite assembly | {Kilobytes(satellite)} |");
        Console.WriteLine($"| Idioma: CodeChops.Idioma | {Kilobytes(idioma)} |");
        Console.WriteLine($"| Idioma: a language of 100 texts, as a set in the app's assembly | {Kilobytes(set)} |");

        if (native != 0)
            Console.WriteLine($"| Idioma: the same set as native code in dotnet.native.wasm, before compression | {Kilobytes(native)} |");
    }

    private static string[] Framework(string wwwroot) => Directory.GetFiles(Path.Combine(wwwroot, "_framework"));

    private static long Size(IEnumerable<string> files, string prefix, string suffix) =>
        files.Where(file => Path.GetFileName(file) is var name
                && name.StartsWith(prefix, StringComparison.Ordinal)
                && name.EndsWith(suffix, StringComparison.Ordinal))
            .Sum(file => new FileInfo(file).Length);

    private static string Kilobytes(long bytes) => $"{bytes / 1024.0:F1} KB";
}
