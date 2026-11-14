using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AotSmokeLibrary;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl", "en-GB")]

var failures = new List<string>();

void Expect(string what, string? actual, string expected)
{
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
        failures.Add($"{what}: expected \"{expected}\", got \"{actual}\"");
}

// What the app registered when it started.
Expect("languages", string.Join(",", TranslationsRegistry.SupportedLanguages), "en,nl,en-GB");

// The language of a set comes from the end of its name, which the published app must still know, and a regional set from
// the code its assembly declares.
var british = TranslationsRegistry.Current.Find<IShopTranslations>(CultureInfo.GetCultureInfo("en-GB"));
Expect("set for en-GB", british?.GetType().Name, nameof(ShopTranslationsEnGB));
Expect("language code of the set for en-GB", british?.LanguageCode, "en-GB");
Expect("language code of the set for nl", TranslationsRegistry.Current.Find<IShopTranslations>(CultureInfo.GetCultureInfo("nl"))?.LanguageCode, "nl");

var orders = LocalizableText.Inline(en: "Orders", nl: "Bestellingen", enGB: "Orders");

// The wording for each culture, a culture of a language the app declares or not (de-DE gets the default language).
(string Culture, string Cart, string Greeting, string Orders, string Grid)[] cases =
[
    ("en", "Cart", "Hello, Ada!", "Orders", "Grid"),
    ("en-US", "Cart", "Hello, Ada!", "Orders", "Grid"),
    ("en-GB", "Basket", "Hello, Ada!", "Orders", "Grid"),
    ("nl", "Winkelwagen", "Hallo, Ada!", "Bestellingen", "Raster"),
    ("nl-BE", "Winkelwagen", "Hallo, Ada!", "Bestellingen", "Raster"),
    ("de-DE", "Cart", "Hello, Ada!", "Orders", "Grid"),
];

foreach (var (culture, cart, greeting, order, grid) in cases)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    Expect($"{culture}: cart of the app's set", ShopTranslations.Cart.ToString(), cart);
    Expect($"{culture}: greeting of the app's set", ShopTranslations.Greeting.Format("Ada"), greeting);
    Expect($"{culture}: inline text", orders.ToString(), order);
    Expect($"{culture}: title of the library's set", GridTranslations.Title.ToString(), grid);
    Expect($"{culture}: cart compared with its wording", ShopTranslations.Cart == cart ? cart : "not equal", cart);
}

// A text through JSON, written by the System.Text.Json source generator, and read back in each language.
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
var json = JsonSerializer.Serialize(new Panel(orders, ShopTranslations.Cart), SmokeJson.Default.Panel);

if (!json.Contains("\"nl\":\"Winkelwagen\"", StringComparison.Ordinal) || !json.Contains("\"en-GB\":\"Basket\"", StringComparison.Ordinal))
    failures.Add($"json: expected every wording of the cart, got {json}");

foreach (var (culture, cart, _, order, _) in cases.Where(static c => c.Culture is "en" or "en-GB" or "nl"))
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    var panel = JsonSerializer.Deserialize(json, SmokeJson.Default.Panel);

    Expect($"{culture}: cart read back from json", panel?.Cart.ToString(), cart);
    Expect($"{culture}: inline text read back from json", panel?.Title.ToString(), order);
}

if (failures.Count > 0)
{
    foreach (var failure in failures)
        Console.Error.WriteLine(failure);

    return 1;
}

Console.WriteLine("Idioma NativeAOT smoke test: OK");
return 0;

public partial class ShopTranslationsEN : ITranslationSet
{
    public virtual string Cart => "Cart";
    public virtual string Greeting => "Hello, {0}!";
}

public class ShopTranslationsNL : IShopTranslations
{
    public virtual string Cart => "Winkelwagen";
    public virtual string Greeting => "Hallo, {0}!";
}

public class ShopTranslationsEnGB : ShopTranslationsEN
{
    public override string Cart => "Basket";
}

public sealed record Panel(LocalizableText Title, ResourceText Cart);

[JsonSerializable(typeof(Panel))]
public sealed partial class SmokeJson : JsonSerializerContext;
