#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "en-GB", "nl")] // The first is the default

const string name = "Max";
var choice = LocalizableText.Inline(en: $"{name}, pick a color.", enGb: $"{name}, pick a colour.", nl: $"{name}, kies een kleur.");

foreach (var language in LocalizationRegistry.SupportedLanguages)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
    Console.WriteLine(choice);
}
