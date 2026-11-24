using CodeChops.Idioma;

namespace AotSmokeLibrary;

// Texts on members: the build writes them as code, and the app finds them through the library's registrar, without
// reading an attribute.
[Localized(en: "Address", nl: "Adres")]
public class Address
{
    [Localized(en: "City", nl: "Plaats")]
    public virtual string City { get; set; } = "";

    public string Unmarked { get; set; } = "";
}

[Localized(en: "Parcel status", nl: "Pakketstatus")]
public enum ParcelStatus
{
    [Localized(en: "Assigned", nl: "Toegewezen")]
    Assigned,

    [Localized(en: "Picked", nl: "Gepickt")]
    Picked,

    // Without a text on purpose, which the build would report (LOC501).
#pragma warning disable LOC501
    Lost,
#pragma warning restore LOC501
}
