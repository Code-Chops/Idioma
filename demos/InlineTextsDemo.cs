#:package CodeChops.Idioma@*

using System.Globalization;
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl", "en-GB")] // the first is the default

var choice = LocalizableText.Inline(en: "{0}, pick a color.", nl: "{0}, kies een kleur.", enGB: "{0}, pick a colour.");

foreach (var language in new[] { "en", "nl", "en-GB" })
{
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
    Console.WriteLine(choice.Format("Ada"));
}
