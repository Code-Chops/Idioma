namespace CodeChops.Idioma;

// The half of LocalizationRegistry that finds a text's wording: how a culture or a language code resolves to the sets
// of a registry, and which of those sets words a text. A plain comment: the compiler merges the XML docs of every
// partial declaration, and the type's summary lives in LocalizationRegistry.cs.
//
// Every text shown comes through here, so each step is worked out once and kept: the names a culture falls back
// through (Resolve), the sets of a contract per language (LanguageSets), and the build's record of each set
// (ContractSet).
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// Per culture name, how the culture resolves here (see <see cref="Resolve"/>). Up to <see cref="MaxLookupsKept"/>
    /// names are kept.
    /// </summary>
    // Every text works this out for the culture of its reader, so without it a render worked it out once per text. Kept
    // by name, because an app may create a culture of the same name per request. Never dropped: a registry never
    // changes, and a registration builds a new one.
    private ConcurrentDictionary<string, CultureLookup> ByCulture { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Per language code, how the language resolves here (see <see cref="ResolveLanguage"/>). Up to
    /// <see cref="MaxLookupsKept"/> codes are kept.
    /// </summary>
    // Apart from ByCulture, though a code is usually a culture name. A lookup there holds the culture object of the
    // reader who asked first, which a thread compares by reference. A code's lookup holds the culture of its language
    // instead, while an app's readers bring culture objects of their own, such as those of ASP.NET Core's options. So
    // in one dictionary, the lookup of en that a fallback made first would make every text of an English reader miss
    // that comparison.
    private ConcurrentDictionary<string, CultureLookup> ByLanguageCode { get; } = new(StringComparer.Ordinal);

    // Far more than the cultures of an app's users. A culture name can come from outside, such as a request header that
    // CurrentUICultureProvider passes on, so the lookups can't grow without end.
    private const int MaxLookupsKept = 256;

    // The culture and the language this thread resolved last, in any registry. Nearly every text of a render asks for
    // the same ones, and comparing a reference with them skips the lookup by name. Per thread, because threads of a
    // server render for users of different languages at once. Shared by them, every lookup wrote the field the others
    // read, and a text took 105 ns where it takes 16 ns for one language (8 threads, measured 2026-11-19). A thread
    // keeps its last lookups, and so their registry, until it resolves others.
    [ThreadStatic]
    private static CultureLookup? LastCulture;

    [ThreadStatic]
    private static CultureLookup? LastLanguage;

    // Set once a dictionary above holds MaxLookupsKept entries, which is for good, as entries are never removed. A lookup
    // past the limit then skips Count, which takes every lock of the dictionary.
    private bool _hasKeptMaxCultures;
    private bool _hasKeptMaxLanguages;

    /// <summary>
    /// How a culture resolves here: the names it falls back through, the sets of each, and whether the
    /// <see cref="AppLanguages"/> serve it. Worked out once per culture name.
    /// </summary>
    internal CultureLookup Resolve(CultureInfo culture)
    {
        if (LastCulture is { } last && ReferenceEquals(last.Culture, culture) && ReferenceEquals(last.Registry, this))
            return last;

        if (!this.ByCulture.TryGetValue(culture.Name, out var lookup))
            lookup = Keep(this.ByCulture, culture.Name, new CultureLookup(this, new CultureFallback(culture), culture, language: null), ref this._hasKeptMaxCultures);

        LastCulture = lookup;
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
            lookup = Keep(this.ByLanguageCode, language, new CultureLookup(this, CultureFallback.OfLanguage(language), CultureName.FindCulture(language), language), ref this._hasKeptMaxLanguages);

        LastLanguage = lookup;
        return lookup;
    }

    /// <summary>
    /// Keeps a lookup under its name, unless <see cref="MaxLookupsKept"/> are kept already. Gives the lookup that is
    /// kept for the name, also when another thread kept one in the meantime, or else the given one.
    /// </summary>
    private static CultureLookup Keep(ConcurrentDictionary<string, CultureLookup> kept, string name, CultureLookup lookup, ref bool hasKeptMax)
    {
        if (hasKeptMax)
            return lookup;

        if (kept.Count < MaxLookupsKept)
            return kept.GetOrAdd(name, lookup);

        hasKeptMax = true;
        return lookup;
    }

    /// <summary>
    /// How a culture or a language code resolves in a registry: the names it falls back through, most specific first,
    /// and the sets of the registry for each name that has any. See <see cref="Resolve"/> and
    /// <see cref="ResolveLanguage"/>.
    /// </summary>
    // A class, for the same reason as ContractSet: ReadyToRun compiles the framework's generic types for it.
    internal sealed class CultureLookup
    {
        /// <summary>
        /// The names to try, most specific first, as <see cref="CultureFallback"/> walks them.
        /// </summary>
        public string[] Names { get; }

        /// <summary>
        /// The sets of each name that has any, in the order of <see cref="Names"/>.
        /// </summary>
        public LanguageSets[] Levels { get; }

        /// <summary>
        /// The registry this was worked out in. A lookup of another registry holds other sets.
        /// </summary>
        public LocalizationRegistry Registry { get; }

        /// <summary>
        /// The culture this was worked out for, which another culture of the same name gets too. For a language code,
        /// the culture of the language, or <see langword="null"/> where this machine has none, as under invariant
        /// globalization (see <see cref="CultureName.FindCulture"/>).
        /// </summary>
        public CultureInfo? Culture { get; }

        /// <summary>
        /// The language code this was worked out for, or <see langword="null"/> for a culture.
        /// </summary>
        public string? Language { get; }

        /// <summary>
        /// Whether a text shows the culture's own wording: the registry has no app languages, or one of them serves the
        /// culture. Else the culture gets the default app language.
        /// </summary>
        public bool ShowsCulture { get; }

        public CultureLookup(LocalizationRegistry registry, CultureFallback fallback, CultureInfo? culture, string? language)
        {
            var byLanguage = registry.ByLanguage;
            var names = new List<string>(capacity: 4);
            var levels = new List<LanguageSets>(capacity: 4);

            foreach (var name in fallback)
            {
                names.Add(name);

                if (byLanguage.TryGetValue(name, out var sets))
                    levels.Add(sets);
            }

            this.Registry = registry;
            this.Names = [.. names];
            this.Levels = [.. levels];
            this.Culture = culture;
            this.Language = language;

            // By the names the lookup walks, so that this gate and the lookup agree on which language serves the culture,
            // also under invariant globalization, where en-GB (if it can be created: PredefinedCulturesOnly off) has no
            // parent culture and en still serves it.
            var appLanguages = registry.AppLanguages;
            this.ShowsCulture = appLanguages.IsDefaultOrEmpty || appLanguages.Any(this.IsServedBy);
        }

        /// <summary>
        /// Whether a language serves the culture: the culture, or one it falls back to, is that language or a region of
        /// it.
        /// </summary>
        public bool IsServedBy(string language)
        {
            foreach (var name in this.Names)
                if (IsLanguageOrRegion(name, language))
                    return true;

            return false;
        }

        // Per contract, the sets found so far for its texts (ResolvedSets<TContract>). An object, because the
        // contract is a type argument only where it's asked for.
        private ConcurrentDictionary<Type, object> ResolvedByContract { get; } = new();

        /// <summary>
        /// The sets found so far for the texts of a contract, for a reader of this culture. See
        /// <see cref="ResolvedSets{TContract}"/>.
        /// </summary>
        /// <param name="count">The number of texts the contract has.</param>
        public ResolvedSets<TContract> GetResolvedSets<TContract>(int count)
            where TContract : class
        {
            var resolved = this.ResolvedByContract.TryGetValue(typeof(TContract), out var known)
                ? known
                : this.ResolvedByContract.GetOrAdd(typeof(TContract), new ResolvedSets<TContract>(this, count));

            return (ResolvedSets<TContract>)resolved;
        }
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
    /// The wording the sets of a contract give a text for the first of the names that has a set, or
    /// <see langword="null"/> when none has. Within a name, it's the wording of the set registered last that words the
    /// text itself, rather than leaving it to the set it derives from (<see cref="FindLevelSet"/>). So rewordings of
    /// one set, registered one after another, each show the texts they change. When that set leaves the text to a set of
    /// a less specific name it derives from, the text takes the wording of that name instead, and so on up. So a set that
    /// rewords a language reaches the regional sets built on that language, for every text they leave as it was.
    /// </summary>
    /// <typeparam name="TContract">The contract the text belongs to.</typeparam>
    /// <param name="lookup">
    /// The culture or language to look in, from <see cref="Resolve"/> or <see cref="ResolveLanguage"/>.
    /// </param>
    /// <param name="name">The name of the text, such as <c>Checkout</c>.</param>
    /// <param name="select">Gives the text as a set of the contract words it.</param>
    /// <param name="set">The set that gave the wording, or <see langword="null"/> when none did.</param>
    /// <param name="isToldByRecord">
    /// Whether the build's record alone told which set words the text, without comparing wordings. The same set then
    /// words the text on every call, so a caller may keep it.
    /// </param>
    // Which texts a set words itself comes from the build's record of its class (RecordSet), not from reflection, which
    // the trimmer can't see. A set the build has no record of, such as a nested one registered by hand, is compared by
    // wording instead: it gives way where it words the text as the set it derives from does. A set deriving from no
    // other set of its name, or a regional set deriving from no set of its language, gives way to nothing, as it
    // inherits none of its texts.
    // A set is asked for its wording only once it's known to give it, or where only its wording can tell. A British set
    // that leaves a text to the English one isn't called for that text at all. Called anyway, a text that a wording
    // shows was worded twice per level for a British user.
    internal string? FindWording<TContract>(CultureLookup lookup, string name, Func<TContract, string?> select,
        out TContract? set, out bool isToldByRecord)
        where TContract : class
    {
        var contract = typeof(TContract);
        ContractSet? found = null;
        string? wording = null;
        var isWorded = false;

        set = null;
        isToldByRecord = true;

        foreach (var sets in lookup.Levels)
        {
            var candidates = sets.Of(contract);
            ILocalizationSet? basis = null;
            string? basisWording = null;

            if (found is not null)
            {
                basis = FindBase(candidates, found.Set);

                if (basis is null)
                    continue;

                var wordsItself = found.WordsItselfOver(basis.GetType(), name);

                if (wordsItself is null)
                {
                    isToldByRecord = false;

                    if (!isWorded)
                    {
                        wording = select((TContract)found.Set);
                        isWorded = true;
                    }

                    basisWording = select((TContract)basis);
                    wordsItself = !String.Equals(basisWording, wording, StringComparison.Ordinal);
                }

                if (wordsItself is true)
                    break;
            }

            // Null only before the first name with a set of the contract: a basis is one of the candidates.
            if (FindLevelSet(candidates, name, select, out var levelWording, out var isLevelWorded, ref isToldByRecord) is not { } level)
                continue;

            found = level;
            wording = levelWording;
            isWorded = isLevelWorded;

            // The basis was worded to compare it, so where it's the set of this name too, its wording is that one.
            if (!isWorded && basisWording is not null && ReferenceEquals(level.Set, basis))
            {
                wording = basisWording;
                isWorded = true;
            }
        }

        if (found is null)
            return null;

        set = (TContract)found.Set;

        return isWorded ? wording : select(set);
    }

    /// <summary>
    /// The set of a contract that words a text for one name: the set registered last that words the text itself rather
    /// than leaving it to the candidate it derives from, or that derives from none. <see langword="null"/> when the name
    /// has no set of the contract.
    /// </summary>
    /// <param name="candidates">The sets of the contract for the name.</param>
    /// <param name="name">The name of the text.</param>
    /// <param name="select">Gives the text as a set of the contract words it.</param>
    /// <param name="wording">The set's wording, where telling the set took it.</param>
    /// <param name="isWorded">
    /// Whether <paramref name="wording"/> holds the set's wording. It doesn't where the build's record told the set, so
    /// that a set a less specific name overrides isn't called at all.
    /// </param>
    /// <param name="isToldByRecord">Set to <see langword="false"/> where a wording was compared to tell the set.</param>
    private static ContractSet? FindLevelSet<TContract>(ContractSet[] candidates, string name, Func<TContract, string?> select,
        out string? wording, out bool isWorded, ref bool isToldByRecord)
        where TContract : class
    {
        wording = null;
        isWorded = false;

        // Ends at the latest at the candidate deriving from no other, as no class derives from itself.
        for (var i = candidates.Length - 1; i >= 0; i--)
        {
            var candidate = candidates[i];
            var (set, basis) = candidate;

            // A set deriving from no other words every text itself. Told here, without a call, because nearly every
            // lookup ends at such a set, and in WebAssembly's interpreter a call cost every text about 40 ns.
            if (basis is null)
                return candidate;

            // Asked of the build's record first, so that a set that leaves the text as it is isn't called at all.
            var wordsItself = candidate.WordsItselfOverBasis(name);
            if (wordsItself is false)
                continue;

            if (wordsItself is true)
                return candidate;

            // No record: only the wording tells, so it's kept for the caller.
            isToldByRecord = false;

            var candidateWording = select((TContract)set);

            if (String.Equals(select((TContract)basis), candidateWording, StringComparison.Ordinal))
                continue;

            wording = candidateWording;
            isWorded = true;

            return candidate;
        }

        return null;
    }

    /// <summary>
    /// The set registered last among the candidates that the class of <paramref name="derived"/> derives from, or
    /// <see langword="null"/>.
    /// </summary>
    private static ILocalizationSet? FindBase(ContractSet[] candidates, ILocalizationSet derived)
    {
        var type = derived.GetType();

        for (var i = candidates.Length - 1; i >= 0; i--)
            if (type.IsSubclassOf(candidates[i].Set.GetType()))
                return candidates[i].Set;

        return null;
    }

    /// <summary>
    /// A set of a contract, with the set registered last among those of its contract and language that its class
    /// derives from, or <see langword="null"/> when it derives from none.
    /// </summary>
    // A class, kept in arrays, because ReadyToRun can't compile the framework's generic types for a struct of this
    // library, and the framework already holds their shared code for classes. As a struct, it left a
    // ConcurrentDictionary and an ImmutableArray of it to the JIT, about 20 methods at the first text of every app.
    internal sealed class ContractSet
    {
        public ILocalizationSet Set { get; }

        public ILocalizationSet? Basis { get; }

        private Type? BasisType { get; }

        /// <summary>
        /// The set's class and each class it derives from, in that order, up to <see cref="object"/>.
        /// </summary>
        private Type[] Classes { get; }

        /// <summary>
        /// The build's record of each of <see cref="Classes"/>, or <see langword="null"/> for a class without one.
        /// </summary>
        // Looked up once, here, and not in RecordedSets on every text, which cost a British user's text about 10 ns on
        // .NET, as did walking Type.BaseType. A registry is built anew after every record (RecordSet), so none is missed.
        private RecordedSet?[] Records { get; }

        public ContractSet(ILocalizationSet set, ILocalizationSet? basis)
        {
            this.Set = set;
            this.Basis = basis;
            this.BasisType = basis?.GetType();

            var classes = new List<Type>(capacity: 2);
            var records = new List<RecordedSet?>(capacity: 2);

            for (var current = set.GetType(); current is not null && current != typeof(object); current = current.BaseType)
            {
                classes.Add(current);
                records.Add(RecordedSets.GetValueOrDefault(current));
            }

            this.Classes = [.. classes];
            this.Records = [.. records];
        }

        public void Deconstruct(out ILocalizationSet set, out ILocalizationSet? basis)
        {
            set = this.Set;
            basis = this.Basis;
        }

        /// <summary>
        /// Whether the set words a text itself rather than leaving it to <see cref="Basis"/>, for a set that has one.
        /// See <see cref="WordsItselfOver"/>.
        /// </summary>
        public bool? WordsItselfOverBasis(string name)
        {
            var wordsItself = this.WordsItselfOver(this.BasisType!, name);
            return wordsItself;
        }

        /// <summary>
        /// Whether the set words a text itself rather than leaving it to a set of the given class it derives from, as
        /// the build recorded it: the set's class, or a class between it and that one, words the text.
        /// <see langword="null"/> where one of those classes has no record, such as a nested one the build didn't see.
        /// </summary>
        public bool? WordsItselfOver(Type basisType, string name)
        {
            if (name.Length is 0)
                return null;

            var classes = this.Classes;
            var records = this.Records;

            for (var i = 0; i < classes.Length; i++)
            {
                if (classes[i] == basisType)
                    return false;

                if (records[i] is not { } recorded)
                    return null;

                if (recorded.Words(name))
                    return true;
            }

            // Not a class the set derives from.
            return null;
        }
    }

    /// <summary>
    /// The sets of one language, and per contract the ones implementing it.
    /// </summary>
    // A language holds the sets of every contract of the app, and a text needs only those of its own: found once per
    // contract and kept, so that a lookup does not test every set of the language against the contract, on every text
    // of every render. Never dropped, as a registry never changes: a registration builds a new one.
    internal sealed class LanguageSets(ImmutableArray<Registration> registrations, ImmutableArray<Registration> unfiled)
    {
        private ImmutableArray<Registration> Registrations { get; } = registrations;

        // The sets of the registry that couldn't be filed under a language, because creating one, or reading its code,
        // threw. Every language holds them, so that every text of their contracts tries them again.
        private ImmutableArray<Registration> Unfiled { get; } = unfiled;

        // Never written to once added, so an array is as safe here as an immutable one.
        private ConcurrentDictionary<Type, ContractSet[]> ByContract { get; } = new();

        /// <summary>
        /// The sets of this language that implement a contract, in the order they apply, each with the set it derives
        /// from. Empty when none implements it.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// When the constructor of such a set throws, with that exception inside. Nothing is kept then, so the next text
        /// of the contract tries again.
        /// </exception>
        public ContractSet[] Of(Type contract)
        {
            var contractSets = this.ByContract.TryGetValue(contract, out var known)
                ? known
                : this.ByContract.GetOrAdd(contract, static (contract, languageSets) => languageSets.Find(contract), this);

            return contractSets;
        }

        // Each set is created here, at the first text of its contract in this language, so a set whose constructor
        // throws fails the texts of its own contract alone.
        private ContractSet[] Find(Type contract)
        {
            foreach (var registration in this.Unfiled)
                if (contract.IsAssignableFrom(registration.Type))
                    Refile(registration);

            var implementing = this.Registrations.Where(registration => contract.IsAssignableFrom(registration.Type)).ToImmutableArray();
            var found = new ContractSet[implementing.Length];

            for (var i = 0; i < implementing.Length; i++)
            {
                var type = implementing[i].Type;
                var basis = implementing.LastOrDefault(other => type.IsSubclassOf(other.Type));

                found[i] = new ContractSet(implementing[i].Set, basis?.Set);
            }

            return found;
        }
    }

    /// <summary>
    /// Tries again to file a set the registry couldn't file under a language when it was built: creates it, and reads
    /// its code. Throws while either does. Otherwise the registry is built again at the next read, with the set filed
    /// under its language.
    /// </summary>
    private static void Refile(Registration registration)
    {
        _ = ReadLanguageCode(registration.Set);

        ImmutableInterlocked.Update(ref Registered, static registered => registered.Copy());
    }
}
