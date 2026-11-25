using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl", "en-GB")]

namespace Benchmarks;

// The texts every approach shows, with the wording of Shop.resx, Shop.nl.resx and Shop.en-GB.resx. British English
// rewords Cart only, so its Checkout and Greeting fall back to English.

public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
{
    public string Checkout => "Checkout";
    public virtual string Cart => "Cart";
    public string Greeting(string name) => $"Hello, {name}!";
}

public class ShopLocalizationEnGb : ShopLocalizationEn
{
    public override string Cart => "Basket";
}

public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
{
    public string Checkout => "Afrekenen";
    public string Cart => "Winkelwagen";
    public string Greeting(string name) => $"Hallo, {name}!";
}

// Written as an attribute on the member it names, as the label of a property. Found by its type and the member's name
// and then shown, as a form finds and shows the label of a field on every render. An attribute takes constants, so it
// has no Greeting, which takes a value.
public class ShopLabels
{
    [Localized(en: "Checkout", nl: "Afrekenen", enGb: "Checkout")]
    public string Checkout { get; set; } = "";

    [Localized(en: "Cart", nl: "Winkelwagen", enGb: "Basket")]
    public string Cart { get; set; } = "";
}

// Written in the call, as markup writes an inline text, so that it's called on every render. A plain text picks one of
// its wordings. Greeting builds the wording of every language on each call, because C# evaluates each argument before
// Inline picks one.
public static class InlineTexts
{
    public static string Checkout => LocalizedText.Inline(en: "Checkout", nl: "Afrekenen", enGb: "Checkout");
    public static string Cart => LocalizedText.Inline(en: "Cart", nl: "Winkelwagen", enGb: "Basket");
    public static string Greeting(string name) => LocalizedText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!", enGb: $"Hello, {name}!");
}
