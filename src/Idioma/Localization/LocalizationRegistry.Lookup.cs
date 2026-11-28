namespace CodeChops.Idioma;

// Every text comes through here, so each step is worked out once and kept: the names a culture falls back through
// (Resolve), and the set that wins for a contract per language (SetsOfLanguage).
/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    // By name, because an app may create a culture of the same name per request. A registry never changes, so a lookup
    // is dropped only when more names come in than are kept (Keep).
    private ConcurrentDictionary<string, CultureLookup> ByCulture { get; } = new(StringComparer.Ordinal);

    // Apart from ByCulture, because a code's lookup holds the culture of its language, while a culture's lookup holds
    // the caller's culture object, which a thread compares by reference.
    private ConcurrentDictionary<string, CultureLookup> ByLanguageCode { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Far more than the cultures of an app's users. A culture name can come from outside, such as a request header, so
    /// the lookups can't grow without end.
    /// </summary>
    private const int MaxLookupsKept = 256;

    /// <summary>
    /// The culture this thread resolved last, in any registry, which nearly every text of a render asks for again. Per
    /// thread, because threads of a server render for users of different languages at once, and a shared field would
    /// be written by every lookup.
    /// </summary>
    [ThreadStatic]
    private static CultureLookup? LastCulture;

    /// <summary>
    /// The culture object this thread asked <see cref="LastCulture"/> for. A later caller may bring another object of
    /// the same name, as ASP.NET Core's options do, so that caller pays the lookup by name once, not for every text.
    /// </summary>
    [ThreadStatic]
    private static CultureInfo? LastCultureAsked;

    [ThreadStatic]
    private static CultureLookup? LastLanguage;

    /// <summary>
    /// Counted here, because <c>Count</c> takes every lock of the dictionary.
    /// </summary>
    private int _culturesKept;
    private int _languagesKept;

    /// <summary>
    /// How a culture resolves here: the names it falls back through, the sets of each, and whether the
    /// <see cref="AppLanguages"/> serve it. Worked out once per culture name.
    /// </summary>
    internal CultureLookup Resolve(CultureInfo culture)
    {
        if (LastCulture is { } last && (ReferenceEquals(last.Culture, culture) || ReferenceEquals(LastCultureAsked, culture)) && ReferenceEquals(last.Registry, this))
            return last;

        if (!this.ByCulture.TryGetValue(culture.Name, out var lookup))
            lookup = Keep(this.ByCulture, culture.Name, new CultureLookup(this, new CultureFallback(culture, this.AppScripts), culture, language: null), ref this._culturesKept);

        // Both written on this thread, one after the other, so the culture asked always belongs to the lookup next to it.
        LastCulture = lookup;
        LastCultureAsked = culture;

        return lookup;
    }

    /// <summary>
    /// How a language code resolves here, as a user of that language gets it (<see cref="CultureFallback.OfLanguage"/>).
    /// Worked out once per code.
    /// </summary>
    internal CultureLookup ResolveLanguage(string language)
    {
        if (LastLanguage is { } last && ReferenceEquals(last.Language, language) && ReferenceEquals(last.Registry, this))
            return last;

        if (!this.ByLanguageCode.TryGetValue(language, out var lookup))
            lookup = Keep(this.ByLanguageCode, language, new CultureLookup(this, CultureFallback.OfLanguage(language, this.AppScripts), CultureName.GetCulture(language), language), ref this._languagesKept);

        LastLanguage = lookup;
        return lookup;
    }

    /// <summary>
    /// Keeps a lookup under its name, and gives the one kept for it, also where another thread kept one meanwhile. Past
    /// <see cref="MaxLookupsKept"/>, the lookups kept so far are dropped first.
    /// </summary>
    // Dropped all at once, not left full, so names that don't exist can't take every place for good. A thread that
    // holds a dropped lookup keeps reading it, as it's of the same registry. The count may run a little off between
    // threads, which only moves when the dictionary empties.
    private static CultureLookup Keep(ConcurrentDictionary<string, CultureLookup> kept, string name, CultureLookup lookup, ref int keptCount)
    {
        if (Interlocked.Increment(ref keptCount) > MaxLookupsKept)
        {
            kept.Clear();
            Volatile.Write(ref keptCount, 1);
        }

        return kept.GetOrAdd(name, lookup);
    }

    /// <summary>
    /// Whether a culture name is a language or a region of it, such as <c>en</c> or <c>en-GB</c> for <c>en</c>.
    /// </summary>
    internal static bool IsLanguageOrRegion(string name, string language)
    {
        var result = name.StartsWith(
            value: language,
            comparisonType: StringComparison.OrdinalIgnoreCase) && (name.Length == language.Length || name[language.Length] is '-');

        return result;
    }

    /// <summary>
    /// The set of a contract for the first of the names that has one. Within a name, one set wins for every text
    /// (<see cref="SetsOfLanguage.Of"/>).
    /// </summary>
    internal static TContract? GetWordingSet<TContract>(CultureLookup lookup)
        where TContract : class
    {
        var set = (TContract?)FindSet(lookup, typeof(TContract));
        return set;
    }

    /// <inheritdoc cref="GetWordingSet{TContract}"/>
    internal static ILocalizationSet? FindSet(CultureLookup lookup, Type contract)
    {
        foreach (var sets in lookup.Levels)
            if (sets.Of(contract) is { } set)
                return set;

        return null;
    }
}
