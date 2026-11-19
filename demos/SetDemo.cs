#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "en-GB", "nl")] // The first is the default

foreach (var language in LocalizationRegistry.SupportedLanguages)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
    Console.WriteLine($"{ShopLocalization.Greeting("Max")} {ShopLocalization.Cart}");
}

public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
{
    public virtual string Cart => "Cart";
    public string Greeting(string name) => $"Hello, {name}!";
}

// British English: only the texts that differ from the English above.
public class ShopLocalizationEnGb : ShopLocalizationEn
{
    public override string Cart => "Basket";
}

// Dutch
public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
{
    public string Cart => "Winkelwagen";
    public string Greeting(string name) => $"Hallo, {name}!";
}