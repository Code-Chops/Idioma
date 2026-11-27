namespace CodeChops.Idioma;

/// <summary>
/// The sets found so far for the texts of one contract, for a user of one culture. A text reads its set here by its
/// position, and asks that set for its wording, without a lookup.
/// </summary>
/// <remarks>
/// The set found for a text words it on every call, so it's kept (<see cref="LocalizationRegistry.Word"/>). A set that
/// needs no scope to word the text is kept for the generated class to call. A set whose method needs a scope of the
/// language its text falls back to is kept with that language's culture. Any other text finds its wording anew on each
/// call.
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
    // holds, which is the object of the caller who asked first. Read only where the lookup's own culture isn't the one
    // asked for, so a caller with the lookup's culture pays nothing for it.
    [ThreadStatic]
    private static CultureInfo? LastAsked;

    /// <summary>
    /// How the registry resolves the culture these sets are for.
    /// </summary>
    public LocalizationRegistry.CultureLookup Lookup { get; }

    // Each array below is written one reference at a time, so a thread sees a set or none, and either is right.

    /// <summary>
    /// Per text without parameters, the set that words it, or <see langword="null"/> where none is kept.
    /// </summary>
    public TContract?[] Sets { get; }

    /// <summary>
    /// Per text with parameters, the set that words it, or <see langword="null"/> where none is kept.
    /// </summary>
    public TContract?[] SetsForValues { get; }

    /// <summary>
    /// Per text with parameters that falls back to a language that doesn't serve the user's culture, the set that
    /// words it there, or <see langword="null"/> where none is kept. Read through <see cref="GetFallback"/>.
    /// </summary>
    // Apart from the sets above, because the set's member must run in a scope of that language. So the generated class
    // can't call it itself, and GeneratedCode enters the scope and calls it, without looking the set up again.
    private FallbackSet<TContract>?[] FallbackSets { get; }

    public ResolvedSets(LocalizationRegistry.CultureLookup lookup, int textCount)
    {
        this.Lookup = lookup;
        this.Sets = new TContract?[textCount];
        this.SetsForValues = new TContract?[textCount];
        this.FallbackSets = new FallbackSet<TContract>?[textCount];
    }

    /// <summary>
    /// The sets for a culture, in the registry of the whole app.
    /// </summary>
    /// <param name="culture">The user's culture.</param>
    /// <param name="textCount">The number of texts the contract has.</param>
    public static ResolvedSets<TContract> Get(CultureInfo culture, int textCount)
    {
        var registry = LocalizationRegistry.Current;
        var last = Last;

        // The registry is compared on every read, so a registration, which builds a new one, drops what a thread kept.
        if (last is not null && (ReferenceEquals(last.Lookup.Culture, culture) || ReferenceEquals(LastAsked, culture)) && ReferenceEquals(last.Lookup.Registry, registry))
            return last;

        last = registry.Resolve(culture).GetResolvedSets<TContract>(textCount);

        // Both written on this thread, one after the other. So the culture asked always belongs to the sets next to it,
        // also where code continues on another thread after an await.
        Last = last;
        LastAsked = culture;

        return last;
    }

    /// <summary>
    /// Keeps the set that words a text on every call.
    /// </summary>
    public void Keep(int textIndex, TContract set, bool forProperty, bool forValues)
    {
        if ((uint)textIndex >= (uint)this.Sets.Length)
            return;

        if (forProperty)
            this.Sets[textIndex] = set;

        if (forValues)
            this.SetsForValues[textIndex] = set;
    }

    /// <summary>
    /// Keeps the set that words a text on every call in a language the text falls back to, with the culture of that
    /// language (<see cref="FallbackSets"/>).
    /// </summary>
    public void KeepFallback(int textIndex, TContract set, CultureInfo? culture)
    {
        if ((uint)textIndex >= (uint)this.FallbackSets.Length)
            return;

        this.FallbackSets[textIndex] = new FallbackSet<TContract>(set, culture);
    }

    /// <summary>
    /// The set kept as a text's fallback, or <see langword="null"/> where none is kept.
    /// </summary>
    /// <param name="textIndex">The position of the text.</param>
    public FallbackSet<TContract>? GetFallback(int textIndex)
    {
        var fallbackSets = this.FallbackSets;
        var fallback = (uint)textIndex < (uint)fallbackSets.Length ? fallbackSets[textIndex] : null;

        return fallback;
    }
}
