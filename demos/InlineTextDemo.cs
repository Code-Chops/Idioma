#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages(defaultLanguage: "en", "en-GB", "nl")]

var name = args.FirstOrDefault() ?? "Max";

// The supported languages, and German, which isn't one of them. A German user sees English, the default.
foreach (var culture in LocalizationRegistry.SupportedLanguages.Append("de-DE"))
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    Console.WriteLine($"{culture}: {Choice(name)}");
}

// The text gives the wording for the language of the moment it's read, so it's read where it's shown.
// Leave out nl:, or the {name} in it, and it doesn't build.
static string Choice(string name) => LocalizableText.Inline(
    en: $"{name}, pick a color.",
    enGb: $"{name}, pick a colour.",
    nl: $"{name}, kies een kleur.");
