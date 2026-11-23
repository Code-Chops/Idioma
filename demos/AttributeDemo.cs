#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages(defaultLanguage: "en", "en-GB", "nl")]

var parcel = new Parcel() { Recipient = "Max", PostalCode = "1012 AB" };

// The texts are looked up once. Each shows its wording in the language of the user it's shown to.
var title = LocalizableText.GetTextOfType<Parcel>();
var recipient = LocalizableText.GetTextByMember<Parcel>(nameof(Parcel.Recipient));
var postalCode = LocalizableText.GetTextByMember<Parcel>(nameof(Parcel.PostalCode));

// The supported languages, and German, which isn't one of them. A German user sees English, the default.
foreach (var culture in LocalizationRegistry.SupportedLanguages.Append("de-DE"))
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    Console.WriteLine($"{culture}: {title} - {recipient}: {parcel.Recipient}, {postalCode}: {parcel.PostalCode}");
}

// Leave out an nl:, and it doesn't build.
[Localized(en: "Parcel", enGb: "Parcel", nl: "Pakket")]
public class Parcel
{
    [Localized(en: "Recipient", enGb: "Recipient", nl: "Ontvanger")]
    public string Recipient { get; init; } = "";

    [Localized(en: "Postal code", enGb: "Postcode", nl: "Postcode")]
    public string PostalCode { get; init; } = "";
}
