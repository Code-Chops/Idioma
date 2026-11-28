namespace CodeChops.Idioma;

// The part of LocalizationRegistry that words a text: the order a text falls back in, and the sets it found.
/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// The wording of a text for a user's culture, for a text whose set isn't kept yet. The first wording found, in the
    /// one place that decides the order:
    /// <list type="number">
    /// <item>The set for the culture (<see cref="GetWordingSet"/>).</item>
    /// <item>The set for the default language of <see cref="AppLanguages"/>, so a library's text falls back to the
    /// app's language rather than its own.</item>
    /// <item>The set for the text's own default language, which may reword it.</item>
    /// <item>The set the text was declared with.</item>
    /// </list>
    /// A culture none of the app's languages serves skips the first, so a library's text shows the app's default
    /// language as the app's own texts do.
    /// </summary>
    /// <param name="select">Gives the text as a set of the contract words it, with its values where it takes any.</param>
    /// <param name="takesValues">Whether the text takes values, so a method of the set words it.</param>
    /// <param name="resolved">Where a set that words the text on every call is kept, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// When the set gives <see langword="null"/> as the wording, or the declared set's type gives no language.
    /// </exception>
    internal string Word<TContract>(CultureLookup lookup, string name, Func<TContract, string?> select, Type defaultSetType,
        Func<ILocalizationSet> getDefaultSet, bool takesValues, ResolvedSets<TContract>? resolved = null)
        where TContract : class
    {
        var appLanguages = this.AppLanguages;
        var appDefaultLanguage = appLanguages.IsDefaultOrEmpty ? null : appLanguages[0];
        var showsCulture = appDefaultLanguage is null || lookup.ShowsCulture;

        if (showsCulture && GetWordingSet<TContract>(lookup) is { } set)
        {
            var wording = Read(set, name, select);
            this.KeepSet(resolved, set, language: null, showsCulture, takesValues, isServed: true);

            return wording;
        }

        // A text of a culture the app shows falls back alone, so it enters a scope of the language it falls back to, and
        // a text its wording shows follows. A text of a culture the app doesn't show needs none, with or without values,
        // because every text it shows ends in that language anyway.
        var entersScope = showsCulture;

        if (appDefaultLanguage is not null
            && this.WordInLanguage(appDefaultLanguage, lookup, entersScope, showsCulture, takesValues, name, select, resolved) is { } appDefaultWording)
            return appDefaultWording;

        // From the set's type, so a registered set of that language words the text without the declared set being created.
        // Both codes are written as culture names are, so the same language isn't walked twice.
        var defaultLanguage = GetLanguageCode(defaultSetType);

        if (!String.Equals(defaultLanguage, appDefaultLanguage, StringComparison.Ordinal)
            && this.WordInLanguage(defaultLanguage, lookup, entersScope, showsCulture, takesValues, name, select, resolved) is { } rewording)
            return rewording;

        // No registered set words it, as in a test's registry without a set of that language.
        var isServed = lookup.IsServedBy(defaultLanguage);

        using var defaultScope = new WordingScope(entersScope && !isServed ? this.ResolveLanguage(defaultLanguage).Culture : null);

        var declaredSet = (TContract)getDefaultSet();
        var defaultWording = Read(declaredSet, name, select);
        this.KeepSet(resolved, declaredSet, defaultLanguage, showsCulture, takesValues, isServed);

        return defaultWording;
    }

    /// <exception cref="InvalidOperationException">When the set gives <see langword="null"/>.</exception>
    private static string Read<TContract>(TContract set, string name, Func<TContract, string?> select)
        where TContract : class
    {
        var wording = select(set) ?? throw CreateNullWordingException(set, name);
        return wording;
    }

    // A set's member may hand on a value that's null at run time, such as a nickname from a database. The text names the
    // set rather than show null.
    internal static InvalidOperationException CreateNullWordingException<TContract>(TContract set, string name)
        where TContract : class
    {
        var exception = new InvalidOperationException($"'{set.GetType()}' gives null for the text '{name}' of '{typeof(TContract).Name}'. Return a string, such as an empty one where a value is missing.");

        return exception;
    }

    /// <summary>
    /// Keeps the set that words a text on every call. Where the text needs no scope, the generated class calls the set
    /// itself: where the set's language serves the user's culture, or the app doesn't show that culture. Else a text with
    /// parameters keeps it as a fallback with its language's culture, so no lookup is needed.
    /// </summary>
    /// <param name="language">The set's language, which only a fallback needs, so <see langword="null"/> where it's served.</param>
    /// <param name="isServed">Whether <paramref name="language"/> serves the user's culture.</param>
    // A property isn't kept as a fallback, because it needs a scope only where the contract lacks a set of one of the
    // app's languages, which is LOC700.
    private void KeepSet<TContract>(ResolvedSets<TContract>? resolved, TContract set, string? language,
        bool showsCulture, bool takesValues, bool isServed)
        where TContract : class
    {
        if (resolved is null)
            return;

        var needsNoScope = !showsCulture || isServed;
        resolved.Keep(set, forProperty: needsNoScope, forValues: needsNoScope);

        if (takesValues && !needsNoScope && language is not null)
            resolved.KeepFallback(set, this.ResolveLanguage(language).Culture);
    }

    /// <summary>
    /// The wording the sets of a language give a text, as a user of that language gets it, or <see langword="null"/>
    /// where the language has no set of the contract.
    /// </summary>
    /// <param name="entersScope">
    /// Whether a text the wording shows follows the language too, where the user's culture isn't served by it.
    /// </param>
    /// <exception cref="InvalidOperationException">When the set gives <see langword="null"/> as the wording.</exception>
    // Through the culture of the language, so zh-TW reaches a set for zh-Hant, its parent culture. No scope is entered
    // where the language has no culture on this machine.
    private string? WordInLanguage<TContract>(string language, CultureLookup userLookup, bool entersScope, bool showsCulture,
        bool takesValues, string name, Func<TContract, string?> select, ResolvedSets<TContract>? resolved)
        where TContract : class
    {
        var languageLookup = this.ResolveLanguage(language);
        var isServed = userLookup.IsServedBy(language);

        using var scope = new WordingScope(entersScope && !isServed ? languageLookup.Culture : null);

        if (GetWordingSet<TContract>(languageLookup) is not { } set)
            return null;

        var wording = Read(set, name, select);
        this.KeepSet(resolved, set, language, showsCulture, takesValues, isServed);

        return wording;
    }
}
