#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages(defaultLanguage: "en", "en-GB", "nl")]

// The supported languages, and German, which isn't one of them. A German user sees English, the default.
foreach (var culture in LocalizationRegistry.SupportedLanguages.Append("de-DE"))
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    Console.WriteLine($"{culture}:");
    Console.WriteLine($"{ShopLocalization.Greeting("Max")} {ShopLocalization.ItemsInCart(2)}.");
    Console.WriteLine($"{ShopLocalization.Checkout}");
    Console.WriteLine();
}

// English: the default. The source generator writes two types from this set:
// - the contract IShopLocalization, which every set implements.
// - the class ShopLocalization, which shows the texts.
public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
{
    public string Checkout => "Checkout";
    public string Greeting(string name) => $"Hello, {name}!";

    // Idioma has no plural rules. Each language writes its own conditions in C#.
    public virtual string ItemsInCart(int count) => count is 1
        ? "You have 1 item in your cart"
        : $"You have {count} items in your cart";
}

// British English: only the texts that differ from the English above.
public class ShopLocalizationEnGb : ShopLocalizationEn
{
    public override string ItemsInCart(int count) => count is 1
        ? "You have 1 item in your basket"
        : $"You have {count} items in your basket";
}

// Dutch. Delete a text here, or the {name} in Greeting, and it doesn't build.
public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
{
    public string Checkout => "Afrekenen";
    public string Greeting(string name) => $"Hallo, {name}!";

    public string ItemsInCart(int count) => count is 1
        ? "Je hebt 1 artikel in je winkelwagen"
        : $"Je hebt {count} artikelen in je winkelwagen";
}
