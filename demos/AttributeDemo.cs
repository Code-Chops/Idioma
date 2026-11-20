#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "en-GB", "nl")] // The first is the default

var parcel = new Parcel { PostalCode = "1012 AB", Status = ParcelStatus.Shipped };

foreach (var language in LocalizationRegistry.SupportedLanguages)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);

    var title = LocalizableText.GetTextOfType<Parcel>();
    var postalCode = LocalizableText.GetTextByMember<Parcel>(nameof(Parcel.PostalCode));
    var status = LocalizableText.GetTextByMember<Parcel>(nameof(Parcel.Status));

    Console.WriteLine($"{title} - {postalCode}: {parcel.PostalCode}, {status}: {parcel.Status.GetText()}");
}

[Localized(en: "Parcel", enGb: "Parcel", nl: "Pakket")]
public class Parcel
{
    [Localized(en: "Postal code", enGb: "Postcode", nl: "Postcode")]
    public string PostalCode { get; init; } = "";

    [Localized(en: "Status", enGb: "Status", nl: "Status")]
    public ParcelStatus Status { get; init; }
}

public enum ParcelStatus
{
    [Localized(en: "Shipped", enGb: "Dispatched", nl: "Verzonden")]
    Shipped,

    [Localized(en: "Delivered", enGb: "Delivered", nl: "Bezorgd")]
    Delivered,
}
