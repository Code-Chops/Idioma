using System.Globalization;
using BenchmarkDotNet.Running;
using Benchmarks;

// The tables are written the same on every machine, whatever its culture.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

switch (args)
{
    // Checks that every approach shows the expected wording in every culture, without timing anything.
    case ["check"]:
        foreach (var culture in Scenarios.Cultures)
        {
            Culture.Use(culture);
            new Scenarios(Localizers.Create()).Verify();
            Console.WriteLine($"{culture}: every approach shows the expected wording.");
        }

        Culture.Use("nl");
        LabelLookup.Verify();
        Console.WriteLine("nl: every label shows the expected wording.");

        break;

    // The scenarios in Blazor WebAssembly, served to a headless Chrome, as tables.
    case ["wasm", var wwwroot, .. var options]:
        await WasmRunner.RunAsync(wwwroot, WasmRunner.Options.Parse(options));
        break;

    // The download sizes of Idioma.Benchmarks.Size, published without localization and with each approach, as a table.
    case ["sizes", var root, var suffix, var label]:
        SizeReport.Print(root, suffix, label);
        break;

    // BenchmarkDotNet, with its own options, such as --filter '*IdiomaSet*'. With --quick, one launch of fewer iterations
    // (Timing).
    default:
        if (args.Contains("--quick", StringComparer.Ordinal))
        {
            Environment.SetEnvironmentVariable(Timing.QuickVariable, "true");
            args = [.. args.Where(static arg => !String.Equals(arg, "--quick", StringComparison.Ordinal))];
        }

        BenchmarkSwitcher.FromAssembly(typeof(TextLookup).Assembly).Run(args);
        break;
}
