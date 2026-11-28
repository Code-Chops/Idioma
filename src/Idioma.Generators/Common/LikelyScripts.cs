namespace CodeChops.Idioma.Generators;

/// <summary>
/// The script a language is written in by default in a region, for the languages written in more than one script. For
/// example, <c>Hant</c> for <c>zh-TW</c> and <c>Cyrl</c> for <c>sr-RS</c>.
/// </summary>
/// <remarks>
/// The app's registrar passes the script of each declared language listed here. At run time, a culture with its
/// script, such as <c>zh-Hant-TW</c>, then tries <c>zh-TW</c> only where that's the same script.
/// </remarks>
internal static class LikelyScripts
{
    // From the likely subtags of the Unicode CLDR (likelySubtags.xml).
    private static readonly Dictionary<string, string> ScriptsByLanguage = new(StringComparer.OrdinalIgnoreCase)
    {
        ["az-AZ"] = "Latn",
        ["bs-BA"] = "Latn",
        ["ff-SN"] = "Latn",
        ["ha-NG"] = "Latn",
        ["kok-IN"] = "Deva",
        ["ks-IN"] = "Arab",
        ["mn-MN"] = "Cyrl",
        ["ms-BN"] = "Latn",
        ["ms-MY"] = "Latn",
        ["pa-IN"] = "Guru",
        ["pa-PK"] = "Arab",
        ["sd-IN"] = "Deva",
        ["sd-PK"] = "Arab",
        ["shi-MA"] = "Tfng",
        ["sr-BA"] = "Cyrl",
        ["sr-ME"] = "Latn",
        ["sr-RS"] = "Cyrl",
        ["sr-XK"] = "Cyrl",
        ["tzm-MA"] = "Latn",
        ["uz-AF"] = "Arab",
        ["uz-UZ"] = "Latn",
        ["vai-LR"] = "Vaii",
        ["zh-CN"] = "Hans",
        ["zh-HK"] = "Hant",
        ["zh-MO"] = "Hant",
        ["zh-SG"] = "Hans",
        ["zh-TW"] = "Hant",
    };

    /// <summary>
    /// The default script of a language in a region, such as <c>Hant</c> for <c>zh-TW</c>, or <see langword="null"/>
    /// for a code that isn't listed, such as <c>en-GB</c>.
    /// </summary>
    public static string? Find(string code)
    {
        var script = ScriptsByLanguage.TryGetValue(code, out var found) ? found : null;
        return script;
    }
}
