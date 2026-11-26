using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Benchmarks;

/// <summary>
/// One text shown once, as a render shows it, for a user of each of <see cref="Scenarios.Cultures"/>.
/// </summary>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByParams)]
[HideColumns("Median", "RatioSD", "Alloc Ratio")]
[Config(typeof(Timing))]
public class TextLookup
{
    private const string Checkout = "Checkout";
    private const string Cart = "Cart (en-GB rewords it)";
    private const string Greeting = "Greeting(name)";

    private Scenarios scenarios = null!;

    [Params("en", "nl", "en-GB", "de")]
    public string Culture { get; set; } = "en";

    [GlobalSetup]
    public void Setup()
    {
        Benchmarks.Culture.Use(this.Culture);
        this.scenarios = new Scenarios(Localizers.Create());
        this.scenarios.Verify();
    }

    // IStringLocalizer is the baseline of each category, as Microsoft recommends it, so the Ratio column compares with it.

    [Benchmark(Baseline = true), BenchmarkCategory(Checkout)]
    public string Localizer_Checkout() => this.scenarios.LocalizerCheckout();

    [Benchmark, BenchmarkCategory(Checkout)]
    public string IdiomaSet_Checkout() => this.scenarios.IdiomaSetCheckout();

    [Benchmark, BenchmarkCategory(Checkout)]
    public string IdiomaInline_Checkout() => this.scenarios.IdiomaInlineCheckout();

    [Benchmark, BenchmarkCategory(Checkout)]
    public string IdiomaAttribute_Checkout() => this.scenarios.IdiomaAttributeCheckout();

    [Benchmark(Baseline = true), BenchmarkCategory(Cart)]
    public string Localizer_Cart() => this.scenarios.LocalizerCart();

    [Benchmark, BenchmarkCategory(Cart)]
    public string IdiomaSet_Cart() => this.scenarios.IdiomaSetCart();

    [Benchmark, BenchmarkCategory(Cart)]
    public string IdiomaInline_Cart() => this.scenarios.IdiomaInlineCart();

    [Benchmark, BenchmarkCategory(Cart)]
    public string IdiomaAttribute_Cart() => this.scenarios.IdiomaAttributeCart();

    [Benchmark(Baseline = true), BenchmarkCategory(Greeting)]
    public string Localizer_Greeting() => this.scenarios.LocalizerGreeting();

    [Benchmark, BenchmarkCategory(Greeting)]
    public string IdiomaSet_Greeting() => this.scenarios.IdiomaSetGreeting();

    [Benchmark, BenchmarkCategory(Greeting)]
    public string IdiomaInline_Greeting() => this.scenarios.IdiomaInlineGreeting();
}

internal static class Localizers
{
    /// <summary>
    /// IStringLocalizer&lt;Shop&gt; as an app's container gives it.
    /// </summary>
    public static IStringLocalizer<Shop> Create() => Services().GetRequiredService<IStringLocalizer<Shop>>();

    /// <summary>
    /// An app's container, with the logging that the localizer factory needs.
    /// </summary>
    public static ServiceProvider Services() => new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
}
