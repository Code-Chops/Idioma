namespace CodeChops.Idioma;

/// <summary>
/// Picks the language a standalone text shows for the user. It shows the first of these:
/// <list type="number">
/// <item>The wording for the user's culture, or a culture it falls back to, where one of the app's languages serves
/// it.</item>
/// <item>The wording for the app's default language, or a language it falls back to.</item>
/// <item>The wording for the assembly's default language.</item>
/// </list>
/// </summary>
internal static class StandaloneLanguage
{
    /// <summary>
    /// What this thread picked last, which a render asks for text after text. All standalone texts of an assembly pass
    /// one array, so comparing two references is enough. Per thread, because threads of a server render for users of
    /// different languages at once.
    /// </summary>
    [ThreadStatic]
    private static LocalizationRegistry.CultureLookup? LastLookup;

    [ThreadStatic]
    private static string[]? LastLanguages;

    [ThreadStatic]
    private static int LastIndex;

    /// <summary>
    /// The position, among an assembly's languages (the default first), of the one a text shows for
    /// <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
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
