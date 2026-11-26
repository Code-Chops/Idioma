namespace CodeChops.Idioma;

/// <summary>
/// Picks the language a text that carries its own wording shows for the reader: one written with
/// <c>LocalizedText.Inline</c>, or with <c>[Localized]</c> on a member.
/// </summary>
/// <remarks>
/// Such a text has a wording for each language of its assembly, the default language first. It shows the first of
/// these:
/// <list type="number">
/// <item>The wording for the reader's culture, or else for a culture it falls back to. A culture none of the app's
/// languages serves skips this.</item>
/// <item>The wording for the app's default language, or else for a language it falls back to.</item>
/// <item>The wording for the assembly's default language.</item>
/// </list>
/// </remarks>
internal static class InlineLanguage
{
    // What this thread picked last. A render asks for the same languages and culture text after text, so comparing two
    // references skips walking the names. An assembly's inline texts and texts on members pass one array, so a render
    // that shows both in turn still finds its pick here. Per thread, for the reason LocalizationRegistry.LastCulture is.
    [ThreadStatic]
    private static LocalizationRegistry.CultureLookup? LastLookup;

    [ThreadStatic]
    private static string[]? LastLanguages;

    [ThreadStatic]
    private static int LastIndex;

    /// <summary>
    /// The position, among an assembly's languages, of the one a text shows for the reader's culture
    /// (<see cref="WordingScope.GetReaderCulture(out CultureInfo)"/>).
    /// </summary>
    /// <param name="languages">The languages of the assembly, each a culture name, the default first.</param>
    public static int IndexOfShownLanguage(string[] languages)
    {
        var culture = WordingScope.GetReaderCulture(out _);
        var lookup = LocalizationRegistry.Current.Resolve(culture);

        if (ReferenceEquals(lookup, LastLookup) && ReferenceEquals(languages, LastLanguages))
            return LastIndex;

        var index = IndexOfShownLanguage(languages, lookup);

        // Not for a registry that holds a plugin's sets (LocalizationRegistry.HoldsCollectibleSets).
        if (!lookup.Registry.HoldsCollectibleSets)
        {
            LastLookup = lookup;
            LastLanguages = languages;
            LastIndex = index;
        }

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
