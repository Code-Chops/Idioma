#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "en-GB", "nl")] // The first is the default

var parcel = new Parcel { PostalCode = "1012 AB", Status = ParcelStatus.Shipped };

// The texts are looked up once. Each shows its wording in the language of the user it's shown to.
var title = LocalizableText.GetTextOfType<Parcel>();
var postalCode = LocalizableText.GetTextByMember<Parcel>(nameof(Parcel.PostalCode));
var status = LocalizableText.GetTextByMember<Parcel>(nameof(Parcel.Status));

// The supported languages, and German, which isn't one of them. A German user sees English, the default.
foreach (var culture in LocalizationRegistry.SupportedLanguages.Append("de-DE"))
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    Console.WriteLine($"{culture}: {title} - {postalCode}: {parcel.PostalCode}, {status}: {parcel.Status.GetText()}");
}

// Leave out an nl:, and it doesn't build.
[Localized(en: "Parcel", enGb: "Parcel", nl: "Pakket")]
public class Parcel
{
    [Localized(en: "Postal code", enGb: "Postcode", nl: "Postcode")]
    public string PostalCode { get; init; } = "";

    [Localized(en: "Status", enGb: "Status", nl: "Status")]
    public ParcelStatus Status { get; init; }
}

// Add a member without [Localized], and the build warns that it shows its name in every language.
public enum ParcelStatus
{
    [Localized(en: "Shipped", enGb: "Dispatched", nl: "Verzonden")]
    Shipped,

    [Localized(en: "Delivered", enGb: "Delivered", nl: "Bezorgd")]
    Delivered,
}
