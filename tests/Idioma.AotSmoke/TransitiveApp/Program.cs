using System.Globalization;
using AotSmokeLibrary;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl")]

var failures = new List<string>();

void Expect(string what, string? actual, string expected)
{
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
        failures.Add($"{what}: expected \"{expected}\", got \"{actual}\"");
}

// Only the registrar the source generator writes registers the app's languages, so they show that it ran here.
Expect("languages", string.Join(",", LocalizationRegistry.SupportedLanguages), "en,nl");

// An inline text takes an argument per language only where the generator wrote Inline for this app's languages.
static string Orders() => LocalizedText.Inline(en: "Orders", nl: "Bestellingen");

using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("nl")))
{
    Expect("text of the library for nl", GridLocalization.Title, "Raster");
    Expect("inline text for nl", Orders(), "Bestellingen");
}

if (failures.Count > 0)
{
    foreach (var failure in failures)
        Console.Error.WriteLine(failure);

    return 1;
}

Console.WriteLine("Idioma transitive reference smoke test: OK");
return 0;
