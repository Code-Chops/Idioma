#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages(defaultLanguage: "en", "nl")]

foreach (var culture in LocalizationRegistry.SupportedLanguages)
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    var day = DateTime.Now.ToString("dddd", CultureInfo.CurrentUICulture);

    // The text gives the wording for the language of the moment it's read, so it's read where it's shown.
    // Leave out nl:, or the {day} in it, and it doesn't build.
    Console.WriteLine($"{culture}: {LocalizableText.Inline(en: $"Today is {day}.", nl: $"Vandaag is het {day}.")}");
}
