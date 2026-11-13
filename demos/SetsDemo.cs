#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl", "en-GB")] // the first is the default

foreach (var language in new[] { "en", "nl", "en-GB" })
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
    Console.WriteLine($"{ShopTranslations.Greeting.Format("Ada")} {ShopTranslations.Cart}");
}

public partial class ShopTranslationsEN : ITranslations
{
    public virtual string Cart => "Cart";
    public virtual string Greeting => "Hello, {0}!";
}

public class ShopTranslationsNL : IShopTranslations
{
    public virtual string Cart => "Winkelwagen";
    public virtual string Greeting => "Hallo, {0}!";
}

// British English: only the texts that differ from the English above.
public class ShopTranslationsEnGB : ShopTranslationsEN
{
    public override string Cart => "Basket";
}
