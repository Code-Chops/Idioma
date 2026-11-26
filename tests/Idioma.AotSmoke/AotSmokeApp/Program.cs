using System.Globalization;
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
var british = LocalizationRegistry.GetLocalizationSet<IShopLocalization>(CultureInfo.GetCultureInfo("en-GB"));
Expect("set for en-GB", british?.GetType().Name, nameof(ShopLocalizationEnGb));
Expect("language code of the set for en-GB", (british as ILocalizationSet)?.LocalizationLanguageCode, "en-GB");
Expect("language code of the set for nl", (LocalizationRegistry.GetLocalizationSet<IShopLocalization>(CultureInfo.GetCultureInfo("nl")) as ILocalizationSet)?.LocalizationLanguageCode, "nl");

// An inline text gives the wording of the moment it's read, so it's read in each culture below.
static string Orders() => LocalizedText.Inline(en: "Orders", nl: "Bestellingen", enGb: "Orders");

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

    Expect($"{culture}: cart of the app's set", ShopLocalization.Cart, cart);
    Expect($"{culture}: greeting of the app's set", ShopLocalization.Greeting("Ada"), greeting);
    Expect($"{culture}: inline text", Orders(), order);
    Expect($"{culture}: title of the library's set", GridLocalization.Title, grid);
    Expect($"{culture}: cart read a second time, from the set kept for the culture", ShopLocalization.Cart, cart);
    Expect($"{culture}: text of the app's set that takes values", ShopLocalization.ItemsInCart(3, "Idioma"), itemsInCart);
    Expect($"{culture}: text of the library's set that takes a value", GridLocalization.Rows(1), rows);
    Expect($"{culture}: text by its key", ShopLocalization.GetTextByKey("Cart"), cart);
    Expect($"{culture}: text by its key, with its values", ShopLocalization.GetTextByKey("ItemsInCart", 3, "Idioma"), itemsInCart);
}

// A text read once keeps the wording of that moment, whatever the culture is afterwards.
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("nl-NL");
var dutchItems = ShopLocalization.ItemsInCart(3, "Idioma");
CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
Expect("text read for a Dutch user, shown later", dutchItems, "3 artikelen in je winkelwagen bij Idioma");

// For a culture a provider gives, while the thread has another: the set words the text as for a user of that culture.
LocalizationRegistry.CurrentUICultureProvider = static () => CultureInfo.GetCultureInfo("nl-NL");
Expect("text that takes values, for a culture the provider gives", ShopLocalization.ItemsInCart(3, "Idioma"), "3 artikelen in je winkelwagen bij Idioma");
Expect("text without values, for a culture the provider gives", ShopLocalization.Cart, "Winkelwagen");
Expect("inline text, for a culture the provider gives", Orders(), "Bestellingen");
Expect("the current UI culture after it", CultureInfo.CurrentUICulture.Name, "en");
LocalizationRegistry.CurrentUICultureProvider = null;

// A text on a member, of the library and of the app. The build wrote each as code, and the app's registrar registered
// those of the library when the app started, so nothing reads an attribute here. An enum's value gives its text
// itself: by the attribute of its member, or from a set of the enum.
(string Culture, string City, string Picked, string Status, string Surname, string Shipped)[] memberCases =
[
    ("en", "City", "Picked", "Parcel status", "Surname", "Shipped"),
    ("en-GB", "City", "Picked", "Parcel status", "Family name", "Dispatched"),
    ("nl", "Plaats", "Gepickt", "Pakketstatus", "Achternaam", "Verzonden"),
    ("de-DE", "City", "Picked", "Parcel status", "Surname", "Shipped"),
];

foreach (var (culture, city, picked, status, surname, shipped) in memberCases)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    Expect($"{culture}: text of a property of the library", LocalizedText.GetTextByMember<Address>(nameof(Address.City)), city);
    Expect($"{culture}: text of a property by a type held at run time", LocalizedText.GetTextByMember(typeof(Address), "City"), city);
    Expect($"{culture}: text of a property the app holds", LocalizedText.GetTextByMember(typeof(Address).GetProperty(nameof(Address.City))!), city);
    Expect($"{culture}: text of an override without one of its own", LocalizedText.GetTextByMember<BusinessAddress>(nameof(Address.City)), city);
    Expect($"{culture}: text of an enum's value", ParcelStatus.Picked.GetText(), picked);
    Expect($"{culture}: text of an enum's member by its name", LocalizedText.GetTextByMember(typeof(ParcelStatus), ParcelStatus.Picked.ToString()), picked);
    Expect($"{culture}: text of an enum itself", LocalizedText.GetTextOfType<ParcelStatus>(), status);
    Expect($"{culture}: text of a property of the app", LocalizedText.GetTextByMember<Customer>(nameof(Customer.Surname)), surname);
    Expect($"{culture}: text of a value of an enum with a set", OrderStatus.Shipped.GetText(), shipped);
}

Expect("text of a member that isn't marked", LocalizedText.GetTextByMember<Address>(nameof(Address.Unmarked)) ?? "none", "none");
Expect("text of an enum's value that isn't marked", ParcelStatus.Lost.GetText(), "Lost");

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

public class Customer
{
    [Localized(en: "Surname", nl: "Achternaam", enGb: "Family name")]
    public string Surname { get; set; } = "";
}

// An override without a text of its own, so it keeps that of the library's property.
public sealed class BusinessAddress : Address
{
    public override string City { get; set; } = "";
}

public enum OrderStatus { Pending, Shipped }

public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public virtual string Pending => "Pending";
    public virtual string Shipped => "Shipped";
}

public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public virtual string Pending => "In behandeling";
    public virtual string Shipped => "Verzonden";
}

public class OrderStatusLocalizationEnGb : OrderStatusLocalizationEn
{
    public override string Shipped => "Dispatched";
}

