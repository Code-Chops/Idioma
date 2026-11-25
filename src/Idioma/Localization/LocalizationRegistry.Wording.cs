namespace CodeChops.Idioma;

// The half of LocalizationRegistry that words a text of a contract for a reader: the order a text falls back in, and
// the sets it found, kept per culture. A plain comment: the compiler merges the XML docs of every partial declaration,
// and the type's summary lives in LocalizationRegistry.cs.
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
    /// words it, a rewording of that language wins (<see cref="FindWording"/>).</item>
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
    /// (<see cref="WordingScope.FindReaderCulture"/>).
    /// </param>
    /// <param name="name">The name of the text, such as <c>Checkout</c>.</param>
    /// <param name="select">Gives the text as a set of the contract words it, with its values where it takes any.</param>
    /// <param name="defaultSetType">The type of the set the text was declared with.</param>
    /// <param name="getDefaultSet">Gives the set the text was declared with, which implements the contract.</param>
    /// <param name="takesValues">Whether the text takes values, so that a method of the set words it.</param>
    /// <param name="resolved">Where a set that words the text on every call is kept, or <see langword="null"/>.</param>
    /// <param name="text">The position of the text in <paramref name="resolved"/>.</param>
    /// <exception cref="InvalidOperationException">
    /// When every set gives <see langword="null"/> as the wording, the set the text was declared with included.
    /// </exception>
    internal string Word<TContract>(CultureLookup lookup, CultureInfo culture, CultureInfo? valuesCulture, string name,
        Func<TContract, string?> select, Type defaultSetType, Func<ILocalizationSet> getDefaultSet, bool takesValues,
        ResolvedSets<TContract>? resolved = null, int text = -1)
        where TContract : class
    {
        // A method of a set formats the values as it runs, so it runs in the culture the text is worded for. A text
        // its wording shows follows this scope without asking the provider again, which may read a request or a
        // user's profile. A property formats nothing, and a text it shows follows the reader's culture by itself.
        using var scope = takesValues ? new WordingScope(culture, valuesCulture) : default;

        var appLanguages = this.AppLanguages;
        var appDefaultLanguage = appLanguages.IsDefaultOrEmpty ? null : appLanguages[0];
        var showsCulture = appDefaultLanguage is null || lookup.ShowsCulture;

        // The set kept for a text with parameters, which the generated class couldn't call itself because its method
        // has to run in this scope. That's every read where a provider gives a culture the thread doesn't have.
        if (takesValues && resolved is not null && (uint)text < (uint)resolved.SetsForValues.Length
            && resolved.SetsForValues[text] is { } keptSet && select(keptSet) is { } keptWording)
        {
            return keptWording;
        }

        TContract? set = null;

        if (showsCulture && this.FindWording(lookup, name, select, out set, out var isToldByRecord) is { } wording)
        {
            if (isToldByRecord)
                resolved?.Keep(text, set!, forProperty: true, forValues: true);

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
            && this.WordInLanguage(appDefaultLanguage, lookup, valuesCulture, entersScope, name, select, out set, out isToldByRecord, out var isServed) is { } appDefaultWording)
        {
            if (isToldByRecord && mayKeep)
                resolved?.Keep(text, set!, forProperty: !showsCulture || isServed, forValues: isServed);

            return appDefaultWording;
        }

        mayKeep &= appDefaultLanguage is null || set is null;

        // The language the build recorded for the set, so that a registered set of that language words the text without
        // the set being created to tell it. A set the build recorded none for, such as one whose code isn't a constant,
        // tells it itself.
        var recordedLanguage = FindLanguageCode(defaultSetType);
        var defaultSet = recordedLanguage is null ? getDefaultSet() : null;
        var defaultLanguage = recordedLanguage ?? defaultSet!.LocalizationLanguageCode;

        if (this.WordInLanguage(defaultLanguage, lookup, valuesCulture, entersScope, name, select, out set, out isToldByRecord, out isServed) is { } rewording)
        {
            if (isToldByRecord && mayKeep)
                resolved?.Keep(text, set!, forProperty: !showsCulture || isServed, forValues: isServed);

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
            resolved?.Keep(text, (TContract)defaultSet, forProperty: !showsCulture || isServed, forValues: isServed);

        return defaultWording;
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
    /// <param name="isToldByRecord">Whether the same set words the text on every call (<see cref="FindWording"/>).</param>
    /// <param name="isServed">Whether the language serves the reader's culture, so that no scope is needed.</param>
    // Through the culture of the language where there is one, so that a text falling back to a language shows what a
    // user of it sees: zh-TW reaches a set for zh-Hant, its parent culture, which the name alone (zh-TW, then zh) walks
    // past. By name where there is none, as under invariant globalization. The registry keeps both per language.
    private string? WordInLanguage<TContract>(string language, CultureLookup reader, CultureInfo? valuesCulture, bool entersScope,
        string name, Func<TContract, string?> select, out TContract? set, out bool isToldByRecord, out bool isServed)
        where TContract : class
    {
        var languageLookup = this.ResolveLanguage(language);
        isServed = reader.IsServedBy(language);

        // Entered per lookup, not per set. Where the build has no record of a set, the registry compares its wording
        // with that of the set it derives from, and that comparison only holds where both are worded in one culture.
        using var scope = entersScope && !isServed ? EnterLanguage(languageLookup.Culture, valuesCulture) : default;

        var wording = this.FindWording(languageLookup, name, select, out set, out isToldByRecord);
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

/// <summary>
/// The sets found so far for the texts of one contract, for a reader of one culture. A text reads its set here by its
/// position, and asks that set for its wording, without a lookup.
/// </summary>
/// <remarks>
/// A set is kept only where it words the text on every call: the build's record told it, without comparing wordings,
/// and no scope is needed to word the text (<see cref="LocalizationRegistry.Word"/>). Any other text finds its wording
/// anew on each call.
/// </remarks>
/// <typeparam name="TContract">The contract.</typeparam>
internal sealed class ResolvedSets<TContract>
    where TContract : class
{
    // What this thread read last. Nearly every text of a render asks for the same culture, and comparing a reference
    // skips the lookup by name. Per thread, because threads of a server render for users of different languages at once.
    [ThreadStatic]
    private static ResolvedSets<TContract>? Last;

    // The culture object this thread asked Last for. It may be another object of the same name as the one the lookup
    // holds, which is the object of the reader who asked first. Read only where the lookup's own culture isn't the one
    // asked for, so a reader with the lookup's culture pays nothing for it.
    [ThreadStatic]
    private static CultureInfo? LastAsked;

    /// <summary>
    /// How the registry resolves the culture these sets are for.
    /// </summary>
    public LocalizationRegistry.CultureLookup Lookup { get; }

    /// <summary>
    /// Per text without parameters, the set that words it, or <see langword="null"/> where none is kept.
    /// </summary>
    // Written one reference at a time, so a reader sees a set or none, and either is right.
    public TContract?[] Sets { get; }

    /// <summary>
    /// Per text with parameters, the set that words it, or <see langword="null"/> where none is kept.
    /// </summary>
    public TContract?[] SetsForValues { get; }

    public ResolvedSets(LocalizationRegistry.CultureLookup lookup, int count)
    {
        this.Lookup = lookup;
        this.Sets = new TContract?[count];
        this.SetsForValues = new TContract?[count];
    }

    /// <summary>
    /// The sets for a culture, in the registry of the whole app.
    /// </summary>
    /// <param name="culture">The reader's culture.</param>
    /// <param name="count">The number of texts the contract has.</param>
    public static ResolvedSets<TContract> Find(CultureInfo culture, int count)
    {
        var registry = LocalizationRegistry.Current;
        var last = Last;

        // The registry is compared on every read, so a registration, which builds a new one, drops what a thread kept.
        if (last is not null && (ReferenceEquals(last.Lookup.Culture, culture) || ReferenceEquals(LastAsked, culture)) && ReferenceEquals(last.Lookup.Registry, registry))
            return last;

        // Both written on this thread, one after the other. So the culture asked always belongs to the sets next to it,
        // also where code goes on on another thread after an await.
        last = registry.Resolve(culture).GetResolvedSets<TContract>(count);
        Last = last;
        LastAsked = culture;

        return last;
    }

    /// <summary>
    /// Keeps the set that words a text on every call.
    /// </summary>
    public void Keep(int text, TContract set, bool forProperty, bool forValues)
    {
        if ((uint)text >= (uint)this.Sets.Length)
            return;

        if (forProperty)
            this.Sets[text] = set;

        if (forValues)
            this.SetsForValues[text] = set;
    }
}
