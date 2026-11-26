namespace CodeChops.Idioma;

// The part of LocalizationRegistry that words a text of a contract for a reader: the order a text falls back in, and
// the sets it found, kept per culture. A plain comment: the compiler merges the XML docs of every partial declaration,
// and the type's summary lives in LocalizationRegistry.cs.
/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// The wording of a text of a contract for a reader's culture: the first wording the order below finds. The
    /// generated class calls this, through <see cref="Internals.GeneratedCode"/>, for a text whose set isn't kept yet.
    /// </summary>
    /// <remarks>
    /// The one place that decides the order:
    /// <list type="number">
    /// <item>The wording of the sets registered for the culture. Where a regional set leaves a text as its language
    /// words it, a rewording of that language wins (<see cref="GetWording"/>).</item>
    /// <item>The same for the default language of this registry's <see cref="AppLanguages"/>, so that a library's text
    /// falls back to the language the app chose rather than the library's own.</item>
    /// <item>The set registered for the text's own default language, which may reword the default wording.</item>
    /// <item>The set the text was declared with.</item>
    /// </list>
    /// A culture none of the app's languages serves skips the first. The app's own texts show their default language
    /// there, so a library's text must too, even where the library has the culture's language.
    /// </remarks>
    /// <typeparam name="TContract">The contract the text belongs to.</typeparam>
    /// <param name="lookup">How this registry resolves <paramref name="culture"/> (<see cref="Resolve"/>).</param>
    /// <param name="culture">The reader's culture.</param>
    /// <param name="valuesCulture">
    /// The culture to format values for, or <see langword="null"/> to format them as the thread does
    /// (<see cref="WordingScope.GetReaderCulture(out CultureInfo)"/>).
    /// </param>
    /// <param name="name">The name of the text, such as <c>Checkout</c>.</param>
    /// <param name="select">Gives the text as a set of the contract words it, with its values where it takes any.</param>
    /// <param name="defaultSetType">The type of the set the text was declared with.</param>
    /// <param name="getDefaultSet">Gives the set the text was declared with, which implements the contract.</param>
    /// <param name="takesValues">Whether the text takes values, so that a method of the set words it.</param>
    /// <param name="resolved">Where a set that words the text on every call is kept, or <see langword="null"/>.</param>
    /// <param name="textIndex">The position of the text in <paramref name="resolved"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// When every set gives <see langword="null"/> as the wording, the set the text was declared with included. Also
    /// when the build described that set with no language, and its language code isn't a culture name.
    /// </exception>
    internal string Word<TContract>(CultureLookup lookup, CultureInfo culture, CultureInfo? valuesCulture, string name,
        Func<TContract, string?> select, Type defaultSetType, Func<ILocalizationSet> getDefaultSet, bool takesValues,
        ResolvedSets<TContract>? resolved = null, int textIndex = -1)
        where TContract : class
    {
        // A method of a set formats the values as it runs, so it runs in the culture the text is worded for. A text
        // its wording shows follows this scope without asking the provider again, which may read a request or a
        // user's profile. A property formats nothing, and a text it shows follows the reader's culture by itself.
        using var scope = takesValues ? new WordingScope(culture, valuesCulture) : default;

        var appLanguages = this.AppLanguages;
        var appDefaultLanguage = appLanguages.IsDefaultOrEmpty ? null : appLanguages[0];
        var showsCulture = appDefaultLanguage is null || lookup.ShowsCulture;

        TContract? set = null;

        if (showsCulture && this.GetWording(lookup, name, select, out set) is { } wording)
        {
            resolved?.Keep(textIndex, set!, forProperty: true, forValues: true);
            return wording;
        }

        // A set that gave null at run time is passed over for this call alone. The set found after it isn't kept,
        // because it would then word the text on every later call, also once the first set gives a wording again.
        var mayKeep = set is null;

        // Below the culture's own wording, a text of a culture the app shows falls back alone: the texts around it, and
        // a text its own wording shows, still have that culture's wording. So it enters a scope of the language it
        // falls back to. A culture the app doesn't show enters none for a property: a text the property shows falls
        // back as it does, so both end in that language anyway.
        var entersScope = takesValues || showsCulture;

        if (appDefaultLanguage is not null
            && this.WordInLanguage(appDefaultLanguage, lookup, valuesCulture, entersScope, name, select, out set, out var isServed) is { } appDefaultWording)
        {
            if (mayKeep)
                this.KeepSet(resolved, textIndex, set!, appDefaultLanguage, showsCulture, takesValues, isServed);

            return appDefaultWording;
        }

        mayKeep &= appDefaultLanguage is null || set is null;

        // The language the build described the set with, so that a registered set of that language words the text without
        // the set being created to tell it. A set the build described with none, such as one whose code isn't a constant,
        // tells it itself. Its code is checked as a registered set's is, so that null or a code that is no culture name
        // names the set, rather than fail deep in the lookup.
        var describedLanguage = GetDescribedLanguageCode(defaultSetType);
        var defaultSet = describedLanguage is null ? getDefaultSet() : null;
        var defaultLanguage = describedLanguage ?? ReadLanguageCode(defaultSet!);

        if (this.WordInLanguage(defaultLanguage, lookup, valuesCulture, entersScope, name, select, out set, out isServed) is { } rewording)
        {
            if (mayKeep)
                this.KeepSet(resolved, textIndex, set!, defaultLanguage, showsCulture, takesValues, isServed);

            return rewording;
        }

        mayKeep &= set is null;

        // The set the text was declared with, where no registered set words it, as in a registry of its own without a
        // set of that language.
        isServed = lookup.IsServedBy(defaultLanguage);

        using var defaultScope = entersScope && !isServed ? EnterLanguage(this.ResolveLanguage(defaultLanguage).Culture, valuesCulture) : default;

        defaultSet ??= getDefaultSet();
        var defaultWording = select((TContract)defaultSet);

        // A member of a set may hand on a value that's null at run time, such as a nickname from a database, though
        // it returns a string. Nothing is left to fall back to here, so the text says so rather than show null.
        if (defaultWording is null)
            throw new InvalidOperationException(
                $"{defaultSet.GetType()} gives null for the text '{name}' of '{typeof(TContract).Name}', so the text has no wording in '{defaultLanguage}', its default language. "
                + "Return a string, such as an empty one where a value is missing.");

        if (mayKeep)
            this.KeepSet(resolved, textIndex, (TContract)defaultSet, defaultLanguage, showsCulture, takesValues, isServed);

        return defaultWording;
    }

    /// <summary>
    /// Keeps the set that words a text on every call, found in a language below the culture's own wording. Where the
    /// text needs no scope, the generated class calls the set itself. Else a text with parameters keeps the set as its
    /// fallback, with the culture of its language, so the scope is entered without looking the set up again.
    /// </summary>
    /// <param name="resolved">Where the set is kept, or <see langword="null"/>.</param>
    /// <param name="textIndex">The position of the text in <paramref name="resolved"/>.</param>
    /// <param name="set">The set that words the text.</param>
    /// <param name="language">The language the set words the text in.</param>
    /// <param name="showsCulture">Whether one of the app's languages serves the reader's culture.</param>
    /// <param name="takesValues">Whether the text takes values, so that a method of the set words it.</param>
    /// <param name="isServed">Whether <paramref name="language"/> serves the reader's culture.</param>
    // A property isn't kept as a fallback. It needs a scope only where it falls back alone, in a culture the app
    // shows, and the contract then lacks a set of one of the app's languages, which is build error LOC700.
    private void KeepSet<TContract>(ResolvedSets<TContract>? resolved, int textIndex, TContract set, string language,
        bool showsCulture, bool takesValues, bool isServed)
        where TContract : class
    {
        if (resolved is null)
            return;

        resolved.Keep(textIndex, set, forProperty: !showsCulture || isServed, forValues: isServed);

        if (takesValues && !isServed)
            resolved.KeepFallback(textIndex, set, this.ResolveLanguage(language).Culture);
    }

    /// <summary>
    /// The wording the sets registered for a language give a text, as a user of that language gets it, or
    /// <see langword="null"/> when none words it.
    /// </summary>
    /// <param name="language">The language code.</param>
    /// <param name="reader">How this registry resolves the reader's culture.</param>
    /// <param name="valuesCulture">The culture to format values for, or <see langword="null"/>.</param>
    /// <param name="entersScope">
    /// Whether a text the wording shows follows the language too, where the reader's culture isn't that language or a
    /// region of it.
    /// </param>
    /// <param name="name">The name of the text.</param>
    /// <param name="select">Gives the text as a set of the contract words it.</param>
    /// <param name="set">The set that gave the wording.</param>
    /// <param name="isServed">Whether the language serves the reader's culture, so that no scope is needed.</param>
    // Through the culture of the language where there is one, so that a text falling back to a language shows what a
    // user of it sees: zh-TW reaches a set for zh-Hant, its parent culture, which the name alone (zh-TW, then zh) walks
    // past. By name where there is none, as under invariant globalization. The registry keeps both per language.
    private string? WordInLanguage<TContract>(string language, CultureLookup reader, CultureInfo? valuesCulture, bool entersScope,
        string name, Func<TContract, string?> select, out TContract? set, out bool isServed)
        where TContract : class
    {
        var languageLookup = this.ResolveLanguage(language);
        isServed = reader.IsServedBy(language);

        using var scope = entersScope && !isServed ? EnterLanguage(languageLookup.Culture, valuesCulture) : default;

        var wording = this.GetWording(languageLookup, name, select, out set);
        return wording;
    }

    /// <summary>
    /// A scope for the culture of a language, or none where the language has no culture on this machine, as under
    /// invariant globalization. The values keep the reader's format, because a language says nothing about the region
    /// its reader writes numbers for.
    /// </summary>
    private static WordingScope EnterLanguage(CultureInfo? culture, CultureInfo? valuesCulture)
    {
        var scope = culture is null
            ? default
            : new WordingScope(culture, valuesCulture);

        return scope;
    }
}
