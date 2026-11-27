namespace CodeChops.Idioma;

/// <summary>
/// Picks the language a standalone text shows for the user. A standalone text carries its own wording instead of a
/// set's. It's written with <c>LocalizedText.Inline</c>, or with <c>[Localized]</c> on a member.
/// </summary>
/// <remarks>
/// A standalone text has a wording for each language of its assembly, the default language first. It shows the first
/// of these:
/// <list type="number">
/// <item>The wording for the user's culture, or else for a culture it falls back to. A culture none of the app's
/// languages serves skips this.</item>
/// <item>The wording for the app's default language, or else for a language it falls back to.</item>
/// <item>The wording for the assembly's default language.</item>
/// </list>
/// </remarks>
internal static class StandaloneLanguage
{
    /// <summary>
    /// What this thread picked last. A render asks for the same languages and culture text after text, so comparing two
    /// references skips walking the names. All standalone texts of an assembly pass one array, so a render that shows
    /// inline texts and texts on members in turn still finds its pick here. Per thread, because threads of a server
    /// render for users of different languages at once.
    /// </summary>
    [ThreadStatic]
    private static LocalizationRegistry.CultureLookup? LastLookup;

    [ThreadStatic]
    private static string[]? LastLanguages;

    [ThreadStatic]
    private static int LastIndex;

    /// <summary>
    /// The position, among an assembly's languages, of the one a text shows for the user's culture
    /// (<see cref="CultureInfo.CurrentUICulture"/>).
    /// </summary>
    /// <param name="languages">The languages of the assembly, each a culture name, the default first.</param>
    public static int IndexOfShownLanguage(string[] languages)
    {
        var lookup = LocalizationRegistry.Current.Resolve(CultureInfo.CurrentUICulture);

        if (ReferenceEquals(lookup, LastLookup) && ReferenceEquals(languages, LastLanguages))
            return LastIndex;

        var index = IndexOfShownLanguage(languages, lookup);

        LastLookup = lookup;
        LastLanguages = languages;
        LastIndex = index;

        return index;
    }

    /// <summary>
    /// The position of the language a text shows for a culture, as a registry resolves it.
    /// </summary>
    internal static int IndexOfShownLanguage(string[] languages, LocalizationRegistry.CultureLookup lookup)
    {
        if (lookup.ShowsCulture)
            foreach (var name in lookup.Names)
                if (IndexOf(languages, name) is var index and >= 0)
                    return index;

        var registry = lookup.Registry;

        if (registry.AppLanguages is [var appDefaultLanguage, ..])
            foreach (var name in registry.ResolveLanguage(appDefaultLanguage).Names)
                if (IndexOf(languages, name) is var index and >= 0)
                    return index;

        return 0;
    }

    private static int IndexOf(string[] languages, string language)
    {
        for (var i = 0; i < languages.Length; i++)
            if (String.Equals(languages[i], language, StringComparison.OrdinalIgnoreCase))
                return i;

        return -1;
    }
}
