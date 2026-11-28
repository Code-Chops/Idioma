namespace CodeChops.Idioma;

/// <summary>
/// The sets found so far for the texts of one contract, for a user of one culture. One set wins for every text of a
/// contract, so only a property and a text with values differ, and whether the set needs a scope. A text whose set
/// isn't kept finds its wording anew on each call (<see cref="LocalizationRegistry.Word"/>).
/// </summary>
internal sealed class ResolvedSets<TContract>
    where TContract : class
{
    // What this thread read last, which nearly every text of a render asks for again. Per thread, because threads of a
    // server render for users of different languages at once.
    [ThreadStatic]
    private static ResolvedSets<TContract>? Last;

    // The culture object this thread asked Last for, which may be another object of the same name as the lookup's.
    [ThreadStatic]
    private static CultureInfo? LastAsked;

    public LocalizationRegistry.CultureLookup Lookup { get; }

    // Each set below is written as one reference, so a thread sees a set or none, and either is right.

    /// <summary>
    /// The set that words a text without parameters.
    /// </summary>
    public TContract? Set { get; private set; }

    /// <summary>
    /// The set that words a text with parameters.
    /// </summary>
    public TContract? SetForValues { get; private set; }

    /// <summary>
    /// For a text with parameters that falls back alone to a language that doesn't serve the user's culture, the set
    /// that words it there. That's for a culture the app shows whose contract lacks its set. A culture the app doesn't
    /// show needs no scope, so its set is <see cref="SetForValues"/>.
    /// </summary>
    // Apart from the others, because the set's member must run in a scope of that language, so GeneratedCode calls it.
    public FallbackSet<TContract>? Fallback { get; private set; }

    public ResolvedSets(LocalizationRegistry.CultureLookup lookup)
    {
        this.Lookup = lookup;
    }

    /// <summary>
    /// The sets for a culture, in the registry of the whole app.
    /// </summary>
    public static ResolvedSets<TContract> Get(CultureInfo culture)
    {
        var registry = LocalizationRegistry.Current;
        var last = Last;

        // The registry is compared on every read, so a registration, which builds a new one, drops what a thread kept.
        if (last is not null && (ReferenceEquals(last.Lookup.Culture, culture) || ReferenceEquals(LastAsked, culture)) && ReferenceEquals(last.Lookup.Registry, registry))
            return last;

        last = registry.Resolve(culture).GetResolvedSets<TContract>();

        // Both written on this thread, one after the other, so the culture asked always belongs to the sets next to it.
        Last = last;
        LastAsked = culture;

        return last;
    }

    public void Keep(TContract set, bool forProperty, bool forValues)
    {
        if (forProperty)
            this.Set = set;

        if (forValues)
            this.SetForValues = set;
    }

    public void KeepFallback(TContract set, CultureInfo? culture)
    {
        this.Fallback = new FallbackSet<TContract>(set, culture);
    }
}
