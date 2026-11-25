#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages(defaultLanguage: "en", "nl")]

foreach (var culture in LocalizationRegistry.SupportedLanguages)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

    // The text of ParcelStatus itself, from the attribute on the enum. It's read where it's shown.
    var parcelStatus = LocalizedText.GetTextOfType<ParcelStatus>();

    // Either way, a value of the enum gives its text with GetText().
    Console.WriteLine($"{culture}: {OrderStatus.Shipped.GetText()}, {parcelStatus}: {ParcelStatus.Delivered.GetText()}");
}

// An enum gets its texts in one of two ways.
public enum OrderStatus { Pending, Shipped }

// 1. A set of the enum. It's a set like any other, with a text for each member of the enum.
// English: the default.
public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public string Pending => "Pending";
    public string Shipped => "Shipped";
}

// Dutch
public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public string Pending => "In behandeling";
    public string Shipped => "Verzonden";
}

// 2. An attribute on each member. Leave out an nl:, and it doesn't build.
// Add a member without [Localized], and the build warns that it shows its name in every language.
[Localized(en: "Parcel status", nl: "Pakketstatus")] // The text of the enum itself
public enum ParcelStatus
{
    [Localized(en: "Shipped", nl: "Verzonden")]
    Shipped,

    [Localized(en: "Delivered", nl: "Bezorgd")]
    Delivered,
}
