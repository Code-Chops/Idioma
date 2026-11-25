#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages(defaultLanguage: "en", "nl")]

var name = args.FirstOrDefault() ?? "Max";

foreach (var culture in LocalizationRegistry.SupportedLanguages)
{
    // UseCulture gives every text in the block the wording of one language, such as that of an email's recipient.
    // The culture formats the day too, so the Dutch line names it in Dutch.
    using var scope = LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo(culture));
    var day = DateTime.Now.ToString("dddd");

    // The text gives the wording for the language of the moment it's read, so it's read where it's shown.
    // Leave out nl:, or the {day} in it, and it doesn't build.
    var greeting = LocalizableText.Inline(
        en: $"Welcome {name}! Today is {day}.",
        nl: $"Welkom {name}! Vandaag is het {day}.");

    Console.WriteLine($"{culture}: {greeting}");
}
