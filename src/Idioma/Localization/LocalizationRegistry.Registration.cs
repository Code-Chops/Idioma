namespace CodeChops.Idioma;

// The half of LocalizationRegistry that keeps what is registered: the sets of the whole app, the app's languages, and
// what the build recorded about each set. A plain comment: the compiler merges the XML docs of every partial
// declaration, and the type's summary lives in LocalizationRegistry.cs.
//
// Nothing here takes a lock. The registrations are one object that never changes, and a registration swaps it for a
// new one in one step. The registry of the whole app is built from it at the first read.
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// What <see cref="Current"/> is built from: every registered set, and the app's languages. It never changes. A
    /// registration swaps it for a new one in one step, so nothing here takes a lock.
    /// </summary>
    // A field, because the swap takes it by reference. A swap that needs a value captures it, rather than handing it to
    // the overload that takes an argument. That overload is generic over the argument's type, and for a struct,
    // ReadyToRun can't compile it, so every app would JIT it at its start.
    private static Registrations Registered = new(defaults: [], overrides: [], languages: []);

    /// <summary>
    /// What the source generator recorded about the sets it found, per type: the language the build read from its name,
    /// and, for a set deriving from another, the texts its own class words. Lock-free to read, because every
    /// LocalizationLanguageCode a set doesn't implement reads it, and so does every lookup in a set that derives from another.
    /// </summary>
    // The language, so that a set keeps it when its type is renamed after the build, and a set named after a longer
    // code its assembly declares, such as ShopLocalizationEnGb for en-GB, isn't read by its last two letters. The texts,
    // so that the registry knows which texts a set rewords without calling it (FindWording).
    private static ConcurrentDictionary<Type, RecordedSet> RecordedSets { get; } = new();

    /// <summary>
    /// Adds a set that isn't registered automatically to the sets of your whole app.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A set isn't registered automatically when:
    /// </para>
    /// <list type="bullet">
    /// <item>It's marked
    /// <see cref="ExcludeFromLocalizationDiscoveryAttribute">[ExcludeFromLocalizationDiscovery]</see>.</item>
    /// <item>It's in an assembly loaded at run time whose code nothing calls, such as a plugin's set for one of your
    /// app's languages.</item>
    /// </list>
    /// <para>
    /// The set wins over the sets registered before it, for every text it changes. See <see cref="ILocalizationSet"/>.
    /// </para>
    /// <para>
    /// When your app declares languages, a set for another language never shows, because a culture outside
    /// <see cref="SupportedLanguages"/> gets the app's default language. Only <see cref="Find{TContract}(CultureInfo)"/>
    /// returns such a set.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSet">The set.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that isn't a culture name. Name the set after its language
    /// (<c>ShopLocalizationDe</c>), or have its <see cref="ILocalizationSet.LocalizationLanguageCode"/> return a culture name such
    /// as <c>en-GB</c>.
    /// </exception>
    public static void Register<TSet>()
        where TSet : class, ILocalizationSet, new()
    {
        Register(new TSet());
    }

    /// <summary>
    /// Adds sets you created yourself to the sets of your whole app, such as a set that needs constructor arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each set wins over the sets registered before it, for every text it changes. See <see cref="ILocalizationSet"/>.
    /// Registering a set of the same type and language again replaces the earlier one.
    /// </para>
    /// <para>
    /// When your app declares languages, a set for another language never shows, because a culture outside
    /// <see cref="SupportedLanguages"/> gets the app's default language. Only <see cref="Find{TContract}(CultureInfo)"/>
    /// returns such a set.
    /// </para>
    /// </remarks>
    /// <param name="sets">
    /// The sets, in the order they apply. For one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <exception cref="ArgumentNullException">When one of the sets is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that isn't a culture name. Name the set after its language
    /// (<c>ShopLocalizationDe</c>), or have its <see cref="ILocalizationSet.LocalizationLanguageCode"/> return a culture name such
    /// as <c>en-GB</c>.
    /// </exception>
    public static void Register(params ReadOnlySpan<ILocalizationSet> sets)
    {
        Add(sets, asDefaults: false);
    }

    /// <summary>
    /// Adds a library's own sets to the sets of your whole app. They give the wording its texts show until an app rewords
    /// them. The source generator calls this for every set in the same assembly as its contract. So you need it only for
    /// a library's set that isn't registered automatically.
    /// </summary>
    /// <remarks>
    /// A set from <see cref="Register(ReadOnlySpan{ILocalizationSet})"/> wins over these for every text it changes.
    /// Registering a set of the same type and language again replaces the earlier one.
    /// </remarks>
    /// <param name="sets">
    /// The sets, in the order they apply. For one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <exception cref="ArgumentNullException">When one of the sets is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that isn't a culture name. Name the set after its language
    /// (<c>ShopLocalizationDe</c>), or have its <see cref="ILocalizationSet.LocalizationLanguageCode"/> return a culture name such
    /// as <c>en-GB</c>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterDefaults(params ReadOnlySpan<ILocalizationSet> sets)
    {
        Add(sets, asDefaults: true);
    }

    /// <summary>
    /// Sets the app's <see cref="SupportedLanguages"/>, for <see cref="Internals.GeneratedCode.RegisterLanguages"/>.
    /// Only the first call counts.
    /// </summary>
    internal static void RegisterLanguages(string defaultLanguage, params ReadOnlySpan<string> otherLanguages)
    {
        // Checked as a registry of your own checks its languages, and here rather than at the first text shown.
        var languages = ToCultureNames(defaultLanguage, otherLanguages);

        // The app's own registrar runs before any assembly it loads, and calls this before the registrars of the
        // assemblies it references, so the first call is the app's. The swap gives a new registry instance, so that a
        // cache a consumer keys on it drops wording resolved before.
        // ReSharper disable once HeapView.CanAvoidClosure - The overload that takes the languages, a struct, is left to the JIT under ReadyToRun.
        ImmutableInterlocked.Update(
            ref Registered,
            registered => registered.Languages.IsEmpty ? registered.WithLanguages(languages) : registered);
    }

    /// <summary>
    /// Records what the build read about a set, for <see cref="Internals.GeneratedCode.RecordSet"/>: its language, or
    /// <see langword="null"/> for a set that implements <see cref="ILocalizationSet.LocalizationLanguageCode"/>, and the texts its
    /// own class words. A set has one code. Recording the same code again changes nothing, and another code is refused.
    /// </summary>
    internal static void RecordSet<TSet>(string? languageCode, string[] ownTexts)
        where TSet : class, ILocalizationSet
    {
        ArgumentNullException.ThrowIfNull(ownTexts);

        // Checked here: a code that is no culture name would otherwise fail every text of the app once the lookup
        // tables are built again, far from this call.
        if (languageCode is not null)
            languageCode = ToCultureName(languageCode, nameof(languageCode));

        // A set registered before its record is filed under the language of its name, and compared by wording, until
        // the lookup tables are built again. So the registrations are swapped for a copy, which builds them anew.
        if (!RecordedSets.TryAdd(typeof(TSet), new RecordedSet(languageCode, ownTexts)))
        {
            // The same code again, as when the generated registration runs twice, leaves the set as it is. Codes are
            // never removed, so the one recorded is there.
            var recorded = RecordedSets[typeof(TSet)].LanguageCode;

            if (String.Equals(recorded, languageCode, StringComparison.Ordinal))
                return;

            throw new ArgumentException($"'{typeof(TSet).Name}' already has the language code '{recorded ?? "null"}', so it can't take '{languageCode ?? "null"}'. A set's language doesn't change.", nameof(languageCode));
        }

        ImmutableInterlocked.Update(ref Registered, static registered => registered.Copy());
    }

    /// <summary>
    /// The language code recorded for a set's type, or else for the nearest class it derives from that has one. So a
    /// rewording of a British set serves British English too. <see langword="null"/> when none has one.
    /// </summary>
    internal static string? FindLanguageCode(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (RecordedSets.TryGetValue(current, out var recorded) && recorded.LanguageCode is { } languageCode)
                return languageCode;

        return null;
    }

    /// <summary>
    /// What the build recorded about a set: the language it read from the set's name, if any, and the texts the set's
    /// own class words, overriding them, declaring them anew, or implementing them explicitly. Each text by its name,
    /// such as <c>Checkout</c>.
    /// </summary>
    // A class, for the reason ContractSet is one.
    internal sealed class RecordedSet
    {
        // Up to this many texts are told apart one by one, which is fastest for the few texts a regional set usually
        // rewords. More are looked up in sorted order, so that a set rewording hundreds of texts doesn't slow every
        // lookup of its users down: scanning 500 names took a text about 0.5 µs on .NET (measured 2026-11-19).
        private const int MaxScannedTexts = 8;

        public string? LanguageCode { get; }

        // In ordinal order when there are more than MaxScannedTexts, and as given otherwise.
        private string[] OwnTexts { get; }

        public RecordedSet(string? languageCode, string[] ownTexts)
        {
            this.LanguageCode = languageCode;

            // A copy, because the generated registrar may hand over an array it keeps.
            if (ownTexts.Length > MaxScannedTexts)
            {
                ownTexts = [.. ownTexts];
                Array.Sort(ownTexts, StringComparer.Ordinal);
            }

            this.OwnTexts = ownTexts;
        }

        /// <summary>
        /// Whether the set's own class words the text of this name.
        /// </summary>
        public bool Words(ReadOnlySpan<char> textName)
        {
            var ownTexts = this.OwnTexts;

            if (ownTexts.Length > MaxScannedTexts)
                return IsAmongSorted(ownTexts, textName);

            // The length first, which tells most names apart without a call: in WebAssembly's interpreter, a call costs
            // more than the comparison.
            foreach (var ownText in ownTexts)
                if (ownText.Length == textName.Length && textName.SequenceEqual(ownText))
                    return true;

            return false;
        }

        /// <summary>
        /// Whether a name is among names in ordinal order, found by halving them.
        /// </summary>
        private static bool IsAmongSorted(string[] sortedNames, ReadOnlySpan<char> name)
        {
            var low = 0;
            var high = sortedNames.Length - 1;

            while (low <= high)
            {
                var middle = low + ((high - low) / 2);
                var order = name.SequenceCompareTo(sortedNames[middle]);

                if (order is 0)
                    return true;

                if (order < 0)
                    high = middle - 1;
                else
                    low = middle + 1;
            }

            return false;
        }
    }

    private static void Add(ReadOnlySpan<ILocalizationSet> sets, bool asDefaults)
    {
        // Read here so that a set without a language code fails at its registration, not at the first text rendered
        // afterwards. And read once, before the swap: a language code is consumer code, and the swap may run again.
        var entries = new Registration[sets.Length];
        for (var i = 0; i < sets.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(sets[i], nameof(sets));
            entries[i] = new Registration(sets[i], ReadLanguageCode(sets[i]));
        }

        // ReSharper disable once HeapView.CanAvoidClosure - The overload that takes both values, as a tuple, is left to the JIT under ReadyToRun.
        ImmutableInterlocked.Update(ref Registered, registered => registered.With(entries, asDefaults));
    }

    /// <summary>
    /// A registered set, with the language it had when it registered.
    /// </summary>
    // A class, for the reason ContractSet is one. As a tuple, it left nine methods of ImmutableArray to the JIT at the
    // start of every app.
    private sealed record Registration(ILocalizationSet Set, string Language);

    /// <summary>
    /// The registered sets and the app's languages, with the registry built from them. It never changes, so a reader
    /// always sees sets and languages that belong together, and its registry holds exactly these.
    /// </summary>
    private sealed class Registrations(
        ImmutableArray<Registration> defaults,
        ImmutableArray<Registration> overrides,
        ImmutableArray<string> languages)
    {
        // Each set with the language it had when it registered, so that a registration can replace an earlier one of
        // the same type and language without calling into the sets, which are consumer code.
        private ImmutableArray<Registration> Defaults { get; } = defaults;
        private ImmutableArray<Registration> Overrides { get; } = overrides;

        /// <summary>
        /// The app's languages, the default first. Empty until the app's registrar gives them.
        /// </summary>
        public ImmutableArray<string> Languages { get; } = languages;

        /// <summary>
        /// The registry of these sets and languages. Built at the first read, and the same instance from then on.
        /// </summary>
        public LocalizationRegistry Registry => this._registry ?? this.CreateRegistry();
        private LocalizationRegistry? _registry;

        private LocalizationRegistry CreateRegistry()
        {
            var sets = ImmutableArray.CreateBuilder<ILocalizationSet>(this.Defaults.Length + this.Overrides.Length);

            foreach (var registration in this.Defaults)
                sets.Add(registration.Set);

            foreach (var registration in this.Overrides)
                sets.Add(registration.Set);

            // Building reads the language code of every set, which is consumer code, so nothing is held meanwhile and
            // two threads may build at once. The first one done gives the registry every reader gets, so that a cache
            // a consumer keys on the instance sees one.
            var created = Create(sets.MoveToImmutable(), this.Languages);

            return Interlocked.CompareExchange(ref this._registry, created, comparand: null) ?? created;
        }

        /// <summary>
        /// These registrations with sets added to the library's own sets or to the sets that reword them.
        /// </summary>
        public Registrations With(Registration[] entries, bool asDefaults)
        {
            var tier = asDefaults ? this.Defaults : this.Overrides;

            // Moved to the end rather than kept in place, so the last registration wins; matched on type and language
            // so that registering again (a second service provider, a test) can't grow the list without bound. On the
            // language each set registered with: a set whose code was recorded only after it registered
            // (RecordSet) is kept once next to a later registration of its type, and no more than once.
            foreach (var entry in entries)
            {
                tier = tier
                    .RemoveAll(existing => existing.Set.GetType() == entry.Set.GetType() && String.Equals(existing.Language, entry.Language, StringComparison.OrdinalIgnoreCase))
                    .Add(entry);
            }

            var registrations = asDefaults
                ? new Registrations(tier, this.Overrides, this.Languages)
                : new Registrations(this.Defaults, tier, this.Languages);

            return registrations;
        }

        /// <summary>
        /// These registrations with the app's languages.
        /// </summary>
        public Registrations WithLanguages(ImmutableArray<string> appLanguages)
        {
            var registrations = new Registrations(this.Defaults, this.Overrides, appLanguages);
            return registrations;
        }

        /// <summary>
        /// The same sets and languages, with a registry that is yet to be built.
        /// </summary>
        public Registrations Copy()
        {
            var copy = new Registrations(this.Defaults, this.Overrides, this.Languages);
            return copy;
        }
    }
}
