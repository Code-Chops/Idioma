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

// Created once and kept, as a component can keep an inline text in a field, so that its time is the lookup alone. The
// value of Greeting is filled in once, so it shows the same name every time.
public static class InlineTextsInField
{
    public static LocalizableText Checkout { get; } = LocalizableText.Inline(en: "Checkout", nl: "Afrekenen", enGb: "Checkout");
    public static LocalizableText Cart { get; } = LocalizableText.Inline(en: "Cart", nl: "Winkelwagen", enGb: "Basket");
    public static LocalizableText Greeting { get; } = LocalizableText.Inline(en: "Hello, Ada!", nl: "Hallo, Ada!", enGb: "Hello, Ada!");
}

// Written in the call, as markup writes an inline text, so that it's called on every render. Idioma finds a plain text it
// created before, and creates Greeting again on every call, because its wording is a new string each time.
public static class InlineTextsInMarkup
{
    public static LocalizableText Checkout => LocalizableText.Inline(en: "Checkout", nl: "Afrekenen", enGb: "Checkout");
    public static LocalizableText Cart => LocalizableText.Inline(en: "Cart", nl: "Winkelwagen", enGb: "Basket");
    public static LocalizableText Greeting(string name) => LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!", enGb: $"Hello, {name}!");
}
