namespace CodeChops.Idioma;

// Every text shown comes through here, so each step is worked out once and kept: the names a culture falls back
// through (Resolve), the sets of a contract per language (SetsOfLanguage), and the build's description of each set
// (ContractSet).
/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// Per culture name, how the culture resolves here (see <see cref="Resolve"/>). Up to <see cref="MaxLookupsKept"/>
    /// names are kept.
    /// </summary>
    // Every text works this out for the culture of its user, so without it a render worked it out once per text. Kept
    // by name, because an app may create a culture of the same name per request. Dropped only when more names came in
    // than are kept (see Keep): a registry never changes, and a registration builds a new one.
    private ConcurrentDictionary<string, CultureLookup> ByCulture { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Per language code, how the language resolves here (see <see cref="ResolveLanguage"/>). Up to
    /// <see cref="MaxLookupsKept"/> codes are kept.
    /// </summary>
    // Apart from ByCulture, though a code is usually a culture name. A lookup there holds the culture object of the
    // caller who asked first, which a thread compares by reference. A code's lookup holds the culture of its language
    // instead, while an app's callers bring culture objects of their own, such as those of ASP.NET Core's options. So
    // in one dictionary, the lookup of en that a fallback made first would make every text of an English user miss
    // that comparison.
    private ConcurrentDictionary<string, CultureLookup> ByLanguageCode { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Far more than the cultures of an app's users. A culture name can come from outside, such as a request header that
    /// an app makes the thread's culture, so the lookups can't grow without end.
    /// </summary>
    private const int MaxLookupsKept = 256;

    /// <summary>
    /// The culture and the language this thread resolved last, in any registry. Nearly every text of a render asks for
    /// the same ones, and comparing a reference with them skips the lookup by name. Per thread, because threads of a
    /// server render for users of different languages at once. Shared by them, every lookup wrote the field the others
    /// read, and a text took 105 ns where it takes 16 ns for one language (8 threads, measured 2026-11-19). A thread
    /// keeps its last lookups, and so their registry, until it resolves others.
    /// </summary>
    [ThreadStatic]
    private static CultureLookup? LastCulture;

    /// <summary>
    /// The culture object this thread asked LastCulture for. A lookup holds the object of the caller who asked first,
    /// and a later caller may bring another object of the same name, as ASP.NET Core's options do after a
    /// UseCulture(CultureInfo.GetCultureInfo("nl-NL")). Compared by reference too, so that caller pays the lookup by
    /// name once, and not for every text. Read only where the lookup's own culture isn't the one asked for.
    /// </summary>
    [ThreadStatic]
    private static CultureInfo? LastCultureAsked;

    [ThreadStatic]
    private static CultureLookup? LastLanguage;

    /// <summary>
    /// How many lookups each dictionary above was given since it was last emptied. Counted here, because Count takes
    /// every lock of the dictionary.
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
            lookup = Keep(this.ByCulture, culture.Name, new CultureLookup(this, new CultureFallback(culture), culture, language: null), ref this._culturesKept);

        // Both written on this thread, one after the other, so the culture asked always belongs to the lookup next to it.
        LastCulture = lookup;
        LastCultureAsked = culture;

        return lookup;
    }

    /// <summary>
    /// How a language code resolves here: the names it falls back through, as a user of that language gets them (see
    /// <see cref="CultureFallback.OfLanguage"/>), and the sets of each. Worked out once per code.
    /// </summary>
    internal CultureLookup ResolveLanguage(string language)
    {
        if (LastLanguage is { } last && ReferenceEquals(last.Language, language) && ReferenceEquals(last.Registry, this))
            return last;

        if (!this.ByLanguageCode.TryGetValue(language, out var lookup))
            lookup = Keep(this.ByLanguageCode, language, new CultureLookup(this, CultureFallback.OfLanguage(language), CultureName.GetCulture(language), language), ref this._languagesKept);

        LastLanguage = lookup;
        return lookup;
    }

    /// <summary>
    /// Keeps a lookup under its name, and gives the lookup that is kept for the name, also when another thread kept one
    /// in the meantime. Once more than <see cref="MaxLookupsKept"/> were given, the lookups kept so far are dropped
    /// first.
    /// </summary>
    // Dropped all at once, and not left full: names that don't exist, such as those of a request header, would then
    // take every place for good, and a culture of a real user that came after them was worked out again for every
    // text. A lookup that is dropped is worked out once more by its next caller, which the cultures of an app's users
    // are within a few texts. A thread that holds a dropped lookup keeps reading it, as it's of the same registry.
    //
    // Two threads that keep one name both count, so the count may run ahead of the dictionary, which then empties a
    // little early. A thread that counts while another empties may leave a few lookups more, which the next round
    // drops.
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
        var result = name.StartsWith(language, StringComparison.OrdinalIgnoreCase) && (name.Length == language.Length || name[language.Length] is '-');
        return result;
    }

    /// <summary>
    /// The set of a contract to read a text's wording from, for the first of the names that has a set, or
    /// <see langword="null"/> when none has. Within a name, the wording is that of the set registered last that words the
    /// text itself, rather than leaving it to the set it derives from (<see cref="GetLevelSet"/>). So rewordings of
    /// one set, registered one after another, each show the texts they change. When that set leaves the text to a set of
    /// a less specific name it derives from, the text takes the wording of that name instead, and so on up. So a set that
    /// rewords a language reaches the regional sets built on that language, for every text they leave as it was.
    /// </summary>
    /// <remarks>
    /// The wording is read from the most specific set that inherits it, rather than from the set whose class declares
    /// the text. So a set that overrides another member its inherited texts read, such as a protected one, shows those
    /// texts with its own member. A regional set comes first. Within a name, the set registered last comes first
    /// (<see cref="GetInheritor"/>).
    /// </remarks>
    /// <typeparam name="TContract">The contract the text belongs to.</typeparam>
    /// <param name="lookup">
    /// The culture or language to look in, from <see cref="Resolve"/> or <see cref="ResolveLanguage"/>.
    /// </param>
    /// <param name="name">The name of the text, such as <c>Checkout</c>.</param>
    // Which texts a set words itself comes from the build's description of its class (DescribeSet), not from reflection,
    // which the trimmer can't see. A set the build doesn't describe, such as a nested one registered by hand, words every
    // text itself. A set deriving from no other set of its name, or a regional set deriving from no set of its language,
    // gives way to nothing, as it inherits none of its texts.
    //
    // The description names only the texts a class declares, not the other members it overrides. So the walk below finds
    // the class whose wording a text shows, and the set to read it from is chosen apart from it (readFrom). Read from the
    // instance of that class instead, a British set that overrides only a protected Currency showed its British users
    // the English one.
    //
    // Only that one set is given, and the caller reads it. Read on every level instead, a text that a wording shows was
    // worded twice per level for a British user.
    internal TContract? GetWordingSet<TContract>(CultureLookup lookup, string name)
        where TContract : class
    {
        var contract = typeof(TContract);
        ContractSet? found = null;
        ContractSet? readFrom = null;

        foreach (var sets in lookup.Levels)
        {
            var candidates = sets.Of(contract);

            // Past the first name with a set of the contract, a less specific name words the text only where the set
            // found so far derives from one of its sets and leaves the text to it. A name it derives from no set of is
            // passed over.
            if (found is not null)
            {
                if (GetBase(candidates, found.Set) is not { } basis)
                    continue;

                if (found.WordsItselfOver(basis.GetType(), name))
                    break;
            }

            if (GetLevelSet(candidates, name, out var inheritor) is { } level)
            {
                // A set of a more specific name that inherits this level's wording keeps being the one read, so a
                // British set's own members still show in a text it leaves to English.
                if (readFrom is null || readFrom.WordsItselfOver(level.Set.GetType(), name))
                    readFrom = inheritor ?? level;

                found = level;
            }
        }

        var set = (TContract?)readFrom?.Set;
        return set;
    }

    /// <summary>
    /// The set of a contract that words a text for one name: the set registered last that words the text itself rather
    /// than leaving it to the candidate it derives from, or that derives from none. <see langword="null"/> when the name
    /// has no set of the contract.
    /// </summary>
    /// <param name="candidates">The sets of the contract for the name.</param>
    /// <param name="name">The name of the text.</param>
    /// <param name="inheritor">
    /// The set to read the wording from where it isn't the set returned (<see cref="GetInheritor"/>), or
    /// <see langword="null"/>.
    /// </param>
    private static ContractSet? GetLevelSet(ContractSet[] candidates, string name, out ContractSet? inheritor)
    {
        // Ends at the latest at the candidate deriving from no other, as no class derives from itself.
        for (var i = candidates.Length - 1; i >= 0; i--)
        {
            var candidate = candidates[i];

            // A set deriving from no other words every text itself. Told without a call, because nearly every lookup
            // ends at such a set, and in WebAssembly's interpreter a call cost every text about 40 ns.
            if (candidate.Basis is null || candidate.WordsItselfOverBasis(name))
            {
                inheritor = GetInheritor(candidates, i, name);
                return candidate;
            }
        }

        inheritor = null;
        return null;
    }

    /// <summary>
    /// The set registered last after the chosen candidate that inherits the chosen candidate's wording of a text, or
    /// <see langword="null"/>. It's a rewording that leaves the text as it was, but may override another member the
    /// text reads, such as a protected one. So the wording is read from it.
    /// </summary>
    /// <param name="candidates">The sets of the contract for one name, in the order they apply.</param>
    /// <param name="chosen">The position of the candidate that words the text.</param>
    /// <param name="name">The name of the text.</param>
    private static ContractSet? GetInheritor(ContractSet[] candidates, int chosen, string name)
    {
        var chosenType = candidates[chosen].Set.GetType();

        // Nearly every lookup chooses the last candidate, which has none after it.
        for (var i = candidates.Length - 1; i > chosen; i--)
            if (!candidates[i].WordsItselfOver(chosenType, name))
                return candidates[i];

        return null;
    }

    /// <summary>
    /// The set registered last among the candidates that the class of <paramref name="derived"/> derives from, or
    /// <see langword="null"/>.
    /// </summary>
    private static ILocalizationSet? GetBase(ContractSet[] candidates, ILocalizationSet derived)
    {
        var type = derived.GetType();

        for (var i = candidates.Length - 1; i >= 0; i--)
            if (type.IsSubclassOf(candidates[i].Set.GetType()))
                return candidates[i].Set;

        return null;
    }
}
