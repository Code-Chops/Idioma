#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "en-GB", "nl")] // The first is the default

var choice = LocalizableText.Inline(en: "{0}, pick a color.", enGB: "{0}, pick a colour.", nl: "{0}, kies een kleur.");

foreach (var language in TranslationsRegistry.SupportedLanguages)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
    Console.WriteLine(choice.Format("Max"));
}
