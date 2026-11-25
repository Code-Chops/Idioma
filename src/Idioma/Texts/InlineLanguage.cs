namespace CodeChops.Idioma;

/// <summary>
/// Picks the language a text that carries its own wording shows for the reader: one written with
/// <c>LocalizableText.Inline</c>, or with <c>[Localized]</c> on a member.
/// </summary>
/// <remarks>
/// Such a text has a wording for each language of its assembly, the default language first. It shows the first of
/// these:
/// <list type="number">
/// <item>The wording for the reader's culture, or else for a culture above it. A culture none of the app's languages
/// serves skips this.</item>
/// <item>The wording for the app's default language, or else for a language above it.</item>
/// <item>The wording for the assembly's default language.</item>
/// </list>
/// </remarks>
internal static class InlineLanguage
{
    // What this thread picked last. A render asks for the same languages and culture text after text, and comparing
    // two references skips the names. Per thread, for the reason the registry keeps its last lookups per thread.
    [ThreadStatic]
    private static LocalizationRegistry.CultureLookup? LastLookup;

    [ThreadStatic]
    private static string[]? LastLanguages;

    [ThreadStatic]
    private static int LastIndex;

    /// <summary>
    /// The position, among an assembly's languages, of the one a text shows for the current UI culture.
    /// </summary>
    /// <param name="languages">The languages of the assembly, each a culture name, the default first.</param>
    public static int IndexOfShownLanguage(string[] languages)
    {
        var culture = WordingScope.FindReaderCulture(out _);
        var lookup = LocalizationRegistry.Current.Resolve(culture);

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
