namespace CodeChops.Idioma;

// The half of LocalizableText that decides which culture a text of a set is worded in, above all one that takes
// values, such as ShopLocalization.Greeting(customer.Name). A plain comment: the compiler merges the XML docs of every
// partial declaration, and the type's summary lives in LocalizableText.cs.
//
// Such a text is worded by its set's method, which runs each time the text is shown. The method formats the values,
// and shows any text among them, as it runs. So it must run in the culture the text is worded for. A WordingScope makes
// that culture the one texts follow while the method runs, and puts back what it changed afterwards.
//
// A text without values is worded by a property of its set. The property formats no values, but it may show another
// text, as in $"Press {ShopLocalization.Checkout} to pay". Shown for the user's own culture, that text follows the same
// culture anyway, so the text enters no scope, which keeps the commonest path free of one. Worded for another culture
// or language (ToString(culture), a set passed in, the default wording, the copy for JSON), it enters a scope that
// sets the culture texts follow and nothing else (WordingScope.Follow). The thread's cultures stay as they are there,
// so the property itself still reads the thread's UI culture.
//
// Three situations enter a scope, each through one method of this file:
//
// 1. ShowWithValues: the text is shown for a culture, by ToString() or ToString(culture).
// 2. EnterLanguageUnlessServed: the text is worded in a language its reader may not read, such as a fallback language
//    or the language of a set passed in. A reader whose culture that language serves already stays in it, so a British
//    reader stays in British English where the English set words the text.
// 3. EnterLanguage: the text is worded for every language at once, for a copy that travels through JSON.
//
// A scope sets two cultures. The culture texts follow decides the wording, also of a text among the values. The
// culture the values are formatted for decides how a date or a number reads:
//
// - Shown for a culture (1), the values are formatted for that culture, unless the thread's own format is the user's.
//   That's ToString() without a provider, and ToString(culture) for the culture the thread follows already. The values
//   then keep the thread's format, so a user who reads one language and writes numbers as another keeps their numbers.
// - Worded in another language (2 and 3), the values keep the reader's format: the provided culture where there is
//   one, else the thread's. A language says nothing about the region its reader writes numbers for.
// - Inside a text being worded, every scope keeps the format that text chose.
public sealed partial class LocalizableText
{
    /// <summary>
    /// The wording of this text that takes values for a culture, worded by its set's method in a scope of that culture.
    /// </summary>
    /// <param name="culture">The culture the text is shown for.</param>
    /// <param name="registry">The sets to look in.</param>
    /// <param name="appLanguages">The app's languages, the default first.</param>
    /// <param name="valuesCulture">
    /// The culture to format the values for, or <see langword="null"/> to format them as the thread does.
    /// </param>
    private string ShowWithValues(CultureInfo culture, LocalizationRegistry? registry, ImmutableArray<string> appLanguages,
        CultureInfo? valuesCulture)
    {
        // Each fallback, and each text among the values, follows this scope without asking the provider again, which
        // may read a request or a user's profile.
        using var scope = new WordingScope(culture, registry, appLanguages, valuesCulture);

        var text = this.FindText(culture, registry, appLanguages);
        return text;
    }

    /// <summary>
    /// The culture to format the values for when this text is shown for a culture asked for: that culture, or
    /// <see langword="null"/> where the thread follows it already and formats the values as the user does.
    /// </summary>
    // The same culture, and not one equal to it: a clone of the thread's culture with its own number format is equal to it,
    // and formats differently.
    // For a culture the thread follows anyway, ToString(culture) shows what ToString() shows. A provided culture is never
    // the thread's own, so the values are formatted for it.
    private static CultureInfo? FindValuesCulture(CultureInfo culture)
    {
        var followedCulture = WordingScope.GetFollowedCulture(out var providedCulture);

        var valuesCulture = providedCulture is null && ReferenceEquals(culture, followedCulture) ? null : culture;
        return valuesCulture;
    }

    /// <summary>
    /// Makes a language the culture texts follow while this text is worded as a reader of that language reads it, for
    /// the copy of a text that holds every language (<see cref="GetWordingPerLanguage"/>).
    /// </summary>
    // Without this scope, the copy would hold a text among the values in the language of whoever writes the copy, in
    // every language. Nothing changes for a language without a culture on this machine, as under invariant
    // globalization.
    private WordingScope EnterLanguage(string? language, LocalizationRegistry? registry)
    {
        if (language is null || !this.IsOfSet)
            return default;

        _ = WordingScope.GetFollowedCulture(out var providedCulture);
        var culture = CultureName.FindCulture(language);

        return this.TakesValues
            ? WordingScope.Enter(culture, registry, providedCulture)
            : WordingScope.FollowLanguage(culture, registry);
    }

    /// <summary>
    /// Makes a language the culture texts follow while this text is worded in it, unless the reader's culture is that
    /// language or a region of it already. For wording in a language the reader may not read: a fallback, or the
    /// language of a set passed in.
    /// </summary>
    // Entered per lookup, not per set. Where the build has no record of a set, the registry compares its wording with
    // that of the set it derives from, and that comparison only holds where both are worded in one culture.
    private WordingScope EnterLanguageUnlessServed(string? language, LocalizationRegistry? registry)
    {
        if (language is null || !this.IsOfSet)
            return default;

        var followedCulture = WordingScope.GetFollowedCulture(out var providedCulture);

        // A registry keeps how the culture resolves and the culture of the language, so that a render doesn't work them
        // out again for every text that takes values.
        var isServed = registry is null
            ? IsServed(followedCulture, language)
            : registry.Resolve(followedCulture).IsServedBy(language);

        if (isServed)
            return default;

        var culture = registry is null ? CultureName.FindCulture(language) : registry.ResolveLanguage(language).Culture;

        return this.TakesValues
            ? WordingScope.Enter(culture, registry, providedCulture)
            : WordingScope.FollowLanguage(culture, registry);
    }
}
