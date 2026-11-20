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
    /// reword one text, and a culture the app doesn't support, which gets English.
    /// </summary>
    public static IReadOnlyList<string> Cultures { get; } = ["en", "nl", "en-GB", "de"];

    // IStringLocalizer comes first, because Microsoft recommends it, so every other approach is compared with it. An
    // inline text comes twice: kept in a field, and written in markup, which calls it on every render. A text written as
    // an attribute is found by its member first, and has no text with a value.

    public string LocalizerCheckout() => localizer["Checkout"].Value;
    public string IdiomaSetCheckout() => ShopLocalization.Checkout.ToString();
    public string IdiomaInlineInFieldCheckout() => InlineTextsInField.Checkout.ToString();
    public string IdiomaInlineInMarkupCheckout() => InlineTextsInMarkup.Checkout.ToString();
    public string IdiomaAttributeCheckout() => LocalizableText.GetTextByMember<ShopLabels>(nameof(ShopLabels.Checkout))!.ToString();

    public string LocalizerCart() => localizer["Cart"].Value;
    public string IdiomaSetCart() => ShopLocalization.Cart.ToString();
    public string IdiomaInlineInFieldCart() => InlineTextsInField.Cart.ToString();
    public string IdiomaInlineInMarkupCart() => InlineTextsInMarkup.Cart.ToString();
    public string IdiomaAttributeCart() => LocalizableText.GetTextByMember<ShopLabels>(nameof(ShopLabels.Cart))!.ToString();

    public string LocalizerGreeting() => localizer["Greeting", this.name].Value;
    public string IdiomaSetGreeting() => ShopLocalization.Greeting(this.name).ToString();
    public string IdiomaInlineInFieldGreeting() => InlineTextsInField.Greeting.ToString();
    public string IdiomaInlineInMarkupGreeting() => InlineTextsInMarkup.Greeting(this.name).ToString();

    /// <summary>
    /// Every scenario by text and approach, for a runner without BenchmarkDotNet.
    /// </summary>
    public IReadOnlyList<Scenario> All =>
    [
        new("Checkout", "IStringLocalizer", this.LocalizerCheckout),
        new("Checkout", "Idioma set", this.IdiomaSetCheckout),
        new("Checkout", "Idioma inline, in a field", this.IdiomaInlineInFieldCheckout),
        new("Checkout", "Idioma inline, in markup", this.IdiomaInlineInMarkupCheckout),
        new("Checkout", "Idioma attribute", this.IdiomaAttributeCheckout),
        new("Cart", "IStringLocalizer", this.LocalizerCart),
        new("Cart", "Idioma set", this.IdiomaSetCart),
        new("Cart", "Idioma inline, in a field", this.IdiomaInlineInFieldCart),
        new("Cart", "Idioma inline, in markup", this.IdiomaInlineInMarkupCart),
        new("Cart", "Idioma attribute", this.IdiomaAttributeCart),
        new("Greeting(name)", "IStringLocalizer", this.LocalizerGreeting),
        new("Greeting(name)", "Idioma set", this.IdiomaSetGreeting),
        new("Greeting(name)", "Idioma inline, in a field", this.IdiomaInlineInFieldGreeting),
        new("Greeting(name)", "Idioma inline, in markup", this.IdiomaInlineInMarkupGreeting),
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
