using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Benchmarks;

/// <summary>
/// The culture one page load runs every scenario for, from its query string: <c>?culture=nl</c>.
/// </summary>
internal sealed record BenchmarkRun(string Culture)
{
    public static BenchmarkRun Current { get; set; } = new("en");

    public static BenchmarkRun Parse(string uri)
    {
        var query = new Uri(uri).Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : "");

        return new BenchmarkRun(query.GetValueOrDefault("culture", "en"));
    }
}

/// <summary>
/// Runs what <see cref="BenchmarkRun.Current"/> asks once the page has rendered, and reports it to the runner.
/// </summary>
public sealed class BenchmarkPage : ComponentBase
{
    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    private string status = "Running";

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, this.status);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        string report;
        try
        {
            report = this.MeasureScenarios();
        }
        catch (Exception exception)
        {
            report = $"error\t{exception.ToString().ReplaceLineEndings(" | ")}";
        }

        // A report that fails is shown on the page. The runner then times out, and says to open the page to see why.
        try
        {
            await this.JS.InvokeVoidAsync("benchmarkReport", report);
            this.status = "Reported";
        }
        catch (JSException exception)
        {
            this.status = $"Not reported: {exception.Message}";
        }

        this.StateHasChanged();
    }

    /// <summary>
    /// Every scenario, after the cost of the loop and of calling it is taken off.
    /// </summary>
    private string MeasureScenarios()
    {
        var culture = BenchmarkRun.Current.Culture;
        var scenarios = new Scenarios(this.Services.GetRequiredService<IStringLocalizer<Shop>>());
        scenarios.Verify();

        var report = new StringBuilder();
        report.Append($"runtime\t{RuntimeInformation.FrameworkDescription}\n");

        var floor = Loop.NanosecondsPerCall(static () => "Checkout");
        // Numbers in the report are invariant, as the page runs in the culture it measures.
        report.Append(CultureInfo.InvariantCulture, $"floor\t{culture}\t{floor:F1}\n");

        foreach (var scenario in scenarios.All)
            report.Append(CultureInfo.InvariantCulture, $"scenario\t{culture}\t{scenario.Text}\t{scenario.Approach}\t{Loop.NanosecondsPerCall(scenario.Show) - floor:F0}\n");

        return report.ToString();
    }
}

/// <summary>
/// Times a call in a loop, as BenchmarkDotNet does where it can't run: rounds of at least 25 ms after a warm-up, and
/// the median of the rounds.
/// </summary>
internal static class Loop
{
    private static int sink;

    public static double NanosecondsPerCall(Func<string> show)
    {
        var calls = 16;
        while (Time(show, calls) < TimeSpan.FromMilliseconds(25))
            calls *= 2;

        // A warm-up, in which the runtime also compiles the hot loop where it can (the jiterpreter, without AOT).
        for (var round = 0; round < 3; round++)
            Time(show, calls);

        var rounds = new double[9];
        for (var round = 0; round < rounds.Length; round++)
            rounds[round] = Time(show, calls).TotalNanoseconds / calls;

        Array.Sort(rounds);
        return rounds[rounds.Length / 2];
    }

    private static TimeSpan Time(Func<string> show, int calls)
    {
        var length = 0;
        var start = Stopwatch.GetTimestamp();

        for (var call = 0; call < calls; call++)
            length += show().Length;

        var elapsed = Stopwatch.GetElapsedTime(start);

        // Kept, so that no call can be left out as unused.
        sink += length;
        return elapsed;
    }
}
