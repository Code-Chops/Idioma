#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages(defaultLanguage: "en", "en-GB", "nl")]

var name = args.FirstOrDefault() ?? "Max";

// The text is made once. It shows its wording in the language of the user it's shown to.
// Leave out nl:, or the {name} in it, and it doesn't build.
var choice = LocalizableText.Inline(
    en: $"{name}, pick a color.",
    enGb: $"{name}, pick a colour.",
    nl: $"{name}, kies een kleur.");

// The supported languages, and German, which isn't one of them. A German user sees English, the default.
foreach (var culture in LocalizationRegistry.SupportedLanguages.Append("de-DE"))
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
    Console.WriteLine($"{culture}: {choice}");
}
