#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "en-GB", "nl")] // The first is the default

foreach (var language in TranslationsRegistry.SupportedLanguages)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
    Console.WriteLine($"{ShopTranslations.Greeting("Max")} {ShopTranslations.Cart}");
}

public partial class ShopTranslationsEN : ITranslationSet
{
    public virtual string Cart => "Cart";
    public string Greeting(string name) => $"Hello, {name}!";
}

// British English: only the texts that differ from the English above.
public class ShopTranslationsEnGB : ShopTranslationsEN
{
    public override string Cart => "Basket";
}

// Dutch
public class ShopTranslationsNL : IShopTranslations
{
    public string Cart => "Winkelwagen";
    public string Greeting(string name) => $"Hallo, {name}!";
}
