using System.Globalization;
using CodeChops.Idioma;
using Microsoft.Extensions.Localization;

namespace Benchmarks;

/// <summary>
/// The work each benchmark times: one text shown once, as a render shows it, by each of five approaches.
/// </summary>
internal sealed class Scenarios(IStringLocalizer<Shop> localizer)
{
    // A field rather than a constant, so that no approach formats its value at compile time.
    private readonly string name = "Ada";

    /// <summary>
    /// The cultures the benchmarks run for: the default language, the second one, a region whose sets and resx file
    /// reword one text, and German, which the app doesn't have.
    /// </summary>
    // German is the UI culture as a desktop app on a German system has it, or a Blazor app that doesn't make its default
    // language the UI culture of such a user (docs/blazor.md). Every approach shows a German user English. A set's
    // method then runs in English, which switches the culture for the call.
    public static IReadOnlyList<string> Cultures { get; } = ["en", "nl", "en-GB", "de"];

    // IStringLocalizer comes first, because Microsoft recommends it, so every other approach is compared with it. An
    // inline text is written in the call, as markup writes it on every render. A text written as an attribute is found
    // by its member first, and has no text with a value.

    public string LocalizerCheckout() => localizer["Checkout"].Value;
    public string IdiomaSetCheckout() => ShopLocalization.Checkout;
    public string IdiomaInlineCheckout() => InlineTexts.Checkout;
    public string IdiomaAttributeCheckout() => LocalizableText.GetTextByMember<ShopLabels>(nameof(ShopLabels.Checkout))!;

    public string LocalizerCart() => localizer["Cart"].Value;
    public string IdiomaSetCart() => ShopLocalization.Cart;
    public string IdiomaInlineCart() => InlineTexts.Cart;
    public string IdiomaAttributeCart() => LocalizableText.GetTextByMember<ShopLabels>(nameof(ShopLabels.Cart))!;

    public string LocalizerGreeting() => localizer["Greeting", this.name].Value;
    public string IdiomaSetGreeting() => ShopLocalization.Greeting(this.name);
    public string IdiomaInlineGreeting() => InlineTexts.Greeting(this.name);

    /// <summary>
    /// Every scenario by text and approach, for a runner without BenchmarkDotNet.
    /// </summary>
    public IReadOnlyList<Scenario> All =>
    [
        new("Checkout", "IStringLocalizer", this.LocalizerCheckout),
        new("Checkout", "Idioma set", this.IdiomaSetCheckout),
        new("Checkout", "Idioma inline", this.IdiomaInlineCheckout),
        new("Checkout", "Idioma attribute", this.IdiomaAttributeCheckout),
        new("Cart", "IStringLocalizer", this.LocalizerCart),
        new("Cart", "Idioma set", this.IdiomaSetCart),
        new("Cart", "Idioma inline", this.IdiomaInlineCart),
        new("Cart", "Idioma attribute", this.IdiomaAttributeCart),
        new("Greeting(name)", "IStringLocalizer", this.LocalizerGreeting),
        new("Greeting(name)", "Idioma set", this.IdiomaSetGreeting),
        new("Greeting(name)", "Idioma inline", this.IdiomaInlineGreeting),
    ];

    /// <summary>
    /// Throws when an approach shows other wording than expected for the current culture, so that no approach is timed
    /// doing less work than the others.
    /// </summary>
    public void Verify()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        var (checkout, cart, greeting) = culture switch
        {
            "nl" => ("Afrekenen", "Winkelwagen", "Hallo, Ada!"),
            "en-GB" => ("Checkout", "Basket", "Hello, Ada!"),
            _ => ("Checkout", "Cart", "Hello, Ada!"),
        };

        foreach (var scenario in this.All)
        {
            var expected = scenario.Text switch
            {
                "Checkout" => checkout,
                "Cart" => cart,
                _ => greeting,
            };

            if (scenario.Show() is var wording && wording != expected)
                throw new InvalidOperationException($"{scenario.Approach} shows '{wording}' for {scenario.Text} in {culture}, where '{expected}' was expected.");
        }
    }
}

internal sealed record Scenario(string Text, string Approach, Func<string> Show);

internal static class Culture
{
    /// <summary>
    /// Makes a culture the one every thread shows texts and formats values for, as a user's language does.
    /// </summary>
    public static void Use(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
