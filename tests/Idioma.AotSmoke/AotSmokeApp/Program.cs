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
Expect("languages", string.Join(",", LocalizationRegistry.SupportedLanguages), "en,nl,en-GB");

// The language of a set comes from the end of its name, which the published app must still know, and a regional set from
// the code its assembly declares.
var british = LocalizationRegistry.Current.Find<IShopLocalization>(CultureInfo.GetCultureInfo("en-GB"));
Expect("set for en-GB", british?.GetType().Name, nameof(ShopLocalizationEnGb));
Expect("language code of the set for en-GB", (british as ILocalizationSet)?.LocalizationLanguageCode, "en-GB");
Expect("language code of the set for nl", (LocalizationRegistry.Current.Find<IShopLocalization>(CultureInfo.GetCultureInfo("nl")) as ILocalizationSet)?.LocalizationLanguageCode, "nl");

var orders = LocalizableText.Inline(en: "Orders", nl: "Bestellingen", enGb: "Orders");

// Written again, a plain inline text is the one created before, as markup needs that writes it on every render. It's
// found by its string literals, which a published app must not take for new strings.
var ordersAgain = LocalizableText.Inline(en: "Orders", nl: "Bestellingen", enGb: "Orders");
Expect("inline text written again", ReferenceEquals(orders, ordersAgain) ? "the text created before" : "another text", "the text created before");

// A text that takes values, created once, before any of the cultures below is the current one: it words itself with
// its values for whichever culture it is shown in.
var items = ShopLocalization.ItemsInCart(3, "Idioma");

// The wording for each culture, a culture of a language the app declares or not (de-DE gets the default language).
(string Culture, string Cart, string Greeting, string Orders, string Grid, string Items, string Rows)[] cases =
[
    ("en", "Cart", "Hello, Ada!", "Orders", "Grid", "3 items in your cart at Idioma", "1 row"),
    ("en-US", "Cart", "Hello, Ada!", "Orders", "Grid", "3 items in your cart at Idioma", "1 row"),
    ("en-GB", "Basket", "Hello, Ada!", "Orders", "Grid", "3 items in your basket at Idioma", "1 row"),
    ("nl", "Winkelwagen", "Hallo, Ada!", "Bestellingen", "Raster", "3 artikelen in je winkelwagen bij Idioma", "1 rij"),
    ("nl-BE", "Winkelwagen", "Hallo, Ada!", "Bestellingen", "Raster", "3 artikelen in je winkelwagen bij Idioma", "1 rij"),
    ("de-DE", "Cart", "Hello, Ada!", "Orders", "Grid", "3 items in your cart at Idioma", "1 row"),
];

foreach (var (culture, cart, greeting, order, grid, itemsInCart, rows) in cases)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    Expect($"{culture}: cart of the app's set", ShopLocalization.Cart.ToString(), cart);
    Expect($"{culture}: greeting of the app's set", ShopLocalization.Greeting("Ada").ToString(), greeting);
    Expect($"{culture}: inline text", orders.ToString(), order);
    Expect($"{culture}: title of the library's set", GridLocalization.Title.ToString(), grid);
    Expect($"{culture}: cart compared with its wording", ShopLocalization.Cart == cart ? cart : "not equal", cart);
    Expect($"{culture}: text of the app's set that takes values", items.ToString(), itemsInCart);
    Expect($"{culture}: text of the library's set that takes a value", GridLocalization.Rows(1).ToString(), rows);
}

Expect("values of a text that takes them", string.Join(",", items.Arguments), "3,Idioma");

// For a culture that is passed, while another is the current one: the set words the text as for a user of that culture.
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
Expect("text that takes values, for a culture that is passed", items.ToString(CultureInfo.GetCultureInfo("nl-NL")), "3 artikelen in je winkelwagen bij Idioma");
Expect("the current UI culture after it", CultureInfo.CurrentUICulture.Name, "en");

// A text through JSON, written by the System.Text.Json source generator, and read back in each language.
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
var json = JsonSerializer.Serialize(new Panel(orders, ShopLocalization.Cart, items), SmokeJson.Default.Panel);

if (!json.Contains("\"nl\":\"Winkelwagen\"", StringComparison.Ordinal) || !json.Contains("\"en-GB\":\"Basket\"", StringComparison.Ordinal))
    failures.Add($"json: expected every wording of the cart, got {json}");

if (!json.Contains("\"nl\":\"3 artikelen in je winkelwagen bij Idioma\"", StringComparison.Ordinal))
    failures.Add($"json: expected every wording of the text that takes values, with its values, got {json}");

foreach (var (culture, cart, _, order, _, itemsInCart, _) in cases.Where(static c => c.Culture is "en" or "en-GB" or "nl"))
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    var panel = JsonSerializer.Deserialize(json, SmokeJson.Default.Panel);

    Expect($"{culture}: cart read back from json", panel?.Cart.ToString(), cart);
    Expect($"{culture}: inline text read back from json", panel?.Title.ToString(), order);
    Expect($"{culture}: text that takes values read back from json", panel?.Items.ToString(), itemsInCart);
}

if (failures.Count > 0)
{
    foreach (var failure in failures)
        Console.Error.WriteLine(failure);

    return 1;
}

Console.WriteLine("Idioma NativeAOT smoke test: OK");
return 0;

public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
{
    public virtual string Cart => "Cart";
    public virtual string Greeting(string name) => $"Hello, {name}!";

    public virtual string ItemsInCart(int count, string shop) => count is 1
        ? $"1 item in your cart at {shop}"
        : $"{count} items in your cart at {shop}";
}

public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
{
    public virtual string Cart => "Winkelwagen";
    public virtual string Greeting(string name) => $"Hallo, {name}!";

    public virtual string ItemsInCart(int count, string shop) => count is 1
        ? $"1 artikel in je winkelwagen bij {shop}"
        : $"{count} artikelen in je winkelwagen bij {shop}";
}

public class ShopLocalizationEnGb : ShopLocalizationEn
{
    public override string Cart => "Basket";

    public override string ItemsInCart(int count, string shop) => count is 1
        ? $"1 item in your basket at {shop}"
        : $"{count} items in your basket at {shop}";
}

public sealed record Panel(LocalizableText Title, LocalizableText Cart, LocalizableText Items);

[JsonSerializable(typeof(Panel))]
public sealed partial class SmokeJson : JsonSerializerContext;
