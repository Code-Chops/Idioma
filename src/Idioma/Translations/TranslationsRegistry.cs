namespace CodeChops.Idioma;

/// <summary>
/// All the <see cref="ITranslations"/> sets of your app, by language: where a text finds its wording for the current UI
/// culture. You rarely need this class, as every set registers itself.
/// </summary>
/// <remarks>
/// <para>
/// Use this class to:
/// </para>
/// <list type="bullet">
/// <item>register a set that does not register itself, such as one that needs constructor arguments: mark it
/// <see cref="ExcludeFromTranslationDiscoveryAttribute">[ExcludeFromTranslationDiscovery]</see> and pass an instance to
/// <see cref="Register(ReadOnlySpan{ITranslations})"/>;</item>
/// <item>read a set directly, such as the Dutch wording of a text whatever the current UI culture, with
/// <see cref="Find{TTranslations}(CultureInfo)"/>;</item>
/// <item>give part of an app, or a test, sets of its own, with <see cref="Create(IEnumerable{ITranslations})"/>.</item>
/// </list>
/// <para>
/// When several sets give a text wording for one language, the set registered last that changes the text wins: see
/// <see cref="ITranslations"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A set that takes the shop's name from configuration, so it needs a constructor argument.
/// [ExcludeFromTranslationDiscovery]
/// public sealed class BrandedShopTranslationsEN(string shopName) : ShopTranslationsEN
/// {
///     public override string Checkout => $"Pay at {shopName}";
/// }
///
/// // At startup:
/// TranslationsRegistry.Register(new BrandedShopTranslationsEN(configuration["ShopName"]!));
///
/// // The Dutch wording of one text, whatever the current UI culture:
/// var dutch = CultureInfo.GetCultureInfo("nl");
/// string? checkout = TranslationsRegistry.Current.Find&lt;IShopTranslations&gt;(dutch)?.Checkout;
/// </code>
/// </example>
public sealed class TranslationsRegistry
{
    private static Lock Gate { get; } = new();

    // Each set with the language it had when it registered, so that a registration can replace an earlier one of the
    // same type and language without calling into the sets, which are consumer code, while holding the gate.
    private static List<(ITranslations Set, string Language)> Defaults { get; } = [];
    private static List<(ITranslations Set, string Language)> Overrides { get; } = [];

    /// <summary>
    /// The language codes the source generator recorded for sets named after a code their assembly declares that is not
    /// two letters (ShopTranslationsEnGB for en-GB), which the last two letters of the name would get wrong. Read on
    /// every LanguageCode a set does not implement, so lock-free to read.
    /// </summary>
    private static ConcurrentDictionary<Type, string> LanguageCodes { get; } = new();

    /// <summary>
    /// Null until read after a registration, so that registering (which generated code does at every assembly load)
    /// stays cheap and the lookup tables are built once. Replaced under the gate, read without it: a reference read is
    /// atomic, and a stale registry is still valid (a text resolves against what was registered a moment earlier).
    /// </summary>
    private static TranslationsRegistry? Snapshot { get; set; }

    /// <summary>
    /// Counts the changes to what a snapshot is built from, so that one built outside the gate is published only when
    /// nothing changed meanwhile. Read and written under the gate.
    /// </summary>
    private static int Version { get; set; }

    /// <summary>
    /// The sets of your whole app: every set that registered itself, and every set you registered. A text looks up its
    /// wording here, so this is what <c>@ShopTranslations.Checkout</c> in markup shows.
    /// </summary>
    public static TranslationsRegistry Current
    {
        get
        {
            while (true)
            {
                if (Snapshot is { } snapshot)
                    return snapshot;

                ImmutableArray<ITranslations> sets;
                ImmutableArray<string> languages;
                int version;

                lock (Gate)
                {
                    sets = [.. Defaults.Select(static entry => entry.Set), .. Overrides.Select(static entry => entry.Set)];
                    languages = SupportedLanguages;
                    version = Version;
                }

                // Built outside the gate: it reads the language code of every set, which is consumer code, and that
                // must not run while this class holds a lock another thread's registration waits for.
                var created = Create(sets, languages);

                lock (Gate)
                {
                    if (Version == version)
                        return Snapshot ??= created;
                }
            }
        }
    }

    /// <summary>
    /// Your app's languages, its default language first, as its
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see> declares them. Empty in an app that
    /// declares no languages.
    /// </summary>
    /// <remarks>
    /// A culture none of these languages serves gets the wording in <see cref="DefaultLanguage"/>, for every text: in
    /// an app declaring <c>("en", "fr")</c>, a Dutch user sees a library's texts in English, like the app's own,
    /// though the library has Dutch. See <see cref="LocalizableText"/>.
    /// </remarks>
    public static ImmutableArray<string> SupportedLanguages { get; private set; } = [];

    /// <summary>
    /// Your app's default language: the first language it declares, such as <c>nl</c> for
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages("nl", "en")]</see>. <see langword="null"/>
    /// in an app that declares no languages.
    /// </summary>
    /// <remarks>
    /// Every text falls back to this language, the texts of the libraries your app uses included: see
    /// <see cref="LocalizableText"/>. In an app that declares no languages, each text falls back to its own default
    /// language instead.
    /// </remarks>
    public static string? DefaultLanguage => SupportedLanguages.IsEmpty ? null : SupportedLanguages[0];

    /// <summary>
    /// The sets of this registry, in the order they apply: for one contract and language, a later set wins for every
    /// text it changes. In <see cref="Current"/>, the sets declared next to their contract come first, then the sets
    /// that reword another assembly's texts and the sets you registered.
    /// </summary>
    public ImmutableArray<ITranslations> Sets { get; }

    /// <summary>
    /// The languages this registry has at least one set for, such as <c>en</c> and <c>nl</c>, as each set's
    /// <see cref="ITranslations.LanguageCode"/> gives them. In <see cref="Current"/>, that includes the languages of
    /// the libraries your app uses, so it can list more languages than your app declares.
    /// </summary>
    /// <remarks>
    /// Each language is written as culture names are, whatever letter case its sets give it: <c>en-GB</c> for a set
    /// whose code is <c>EN-gb</c>.
    /// </remarks>
    public IReadOnlyCollection<string> Languages { get; }

    /// <summary>
    /// The languages a text read from this registry shows, the default language first. In <see cref="Current"/>, these
    /// are your app's <see cref="SupportedLanguages"/>; in a registry of your own, the languages you passed to
    /// <see cref="Create(IEnumerable{ITranslations}, string, ReadOnlySpan{string})"/>. Empty when none were given.
    /// </summary>
    /// <remarks>
    /// A culture none of these languages serves gets the wording in the first of them, for every text. A registry
    /// without languages shows every language it has a set for, and a culture without wording gets each text's own
    /// default language.
    /// </remarks>
    public ImmutableArray<string> AppLanguages { get; }

    private FrozenDictionary<string, LanguageSets> ByLanguage { get; }

    private TranslationsRegistry(ImmutableArray<ITranslations> sets, ImmutableArray<string> appLanguages,
        FrozenDictionary<string, LanguageSets> byLanguage)
    {
        this.Sets = sets;
        this.AppLanguages = appLanguages;
        this.ByLanguage = byLanguage;
        this.Languages = byLanguage.Keys;
    }

    /// <summary>
    /// A registry of exactly the given sets, apart from the sets of your whole app: for a part of an app that keeps
    /// sets of its own, or a test that needs exact sets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read a text's wording from it with <see cref="LocalizableText.GetText(CultureInfo, TranslationsRegistry)"/>. To
    /// add sets to your whole app, use <see cref="Register(ReadOnlySpan{ITranslations})"/> instead.
    /// </para>
    /// <para>
    /// The registry stands apart from your app's languages too: a text read from it shows every language the registry
    /// has a set for, and a culture without wording gets the text's own default language. To give the registry
    /// languages of its own, pass them to
    /// <see cref="Create(IEnumerable{ITranslations}, string, ReadOnlySpan{string})"/>.
    /// </para>
    /// </remarks>
    /// <param name="sets">
    /// The sets, in the order they apply: for one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that is not a culture name. Name the set after its language
    /// (<c>ShopTranslationsDE</c>), or have its <see cref="ITranslations.LanguageCode"/> return a culture name such as
    /// <c>en-GB</c>.
    /// </exception>
    public static TranslationsRegistry Create(IEnumerable<ITranslations> sets)
    {
        return Create(sets, appLanguages: []);
    }

    /// <summary>
    /// A registry of exactly the given sets with languages of its own, the default language first:
    /// <c>TranslationsRegistry.Create(sets, "en", "nl")</c>. A text read from it treats these languages as
    /// <see cref="Current"/> treats those of your app.
    /// </summary>
    /// <remarks>
    /// A culture none of the languages serves gets the wording in the default language, for every text, even where the
    /// registry has a set for that culture. So does a culture that a text has no wording for.
    /// </remarks>
    /// <param name="sets">
    /// The sets, in the order they apply: for one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <param name="defaultLanguage">The default language: a culture name such as <c>en</c> or <c>en-GB</c>.</param>
    /// <param name="otherLanguages">The other languages, each a culture name.</param>
    /// <exception cref="ArgumentException">
    /// When a language is not a culture name, such as an empty string or <c>en_GB</c>; pass a culture name such as
    /// <c>en</c> or <c>en-GB</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that is not a culture name. Name the set after its language
    /// (<c>ShopTranslationsDE</c>), or have its <see cref="ITranslations.LanguageCode"/> return a culture name such as
    /// <c>en-GB</c>.
    /// </exception>
    public static TranslationsRegistry Create(IEnumerable<ITranslations> sets, string defaultLanguage,
        params ReadOnlySpan<string> otherLanguages)
    {
        return Create(sets, ToCultureNames(defaultLanguage, otherLanguages));
    }

    /// <summary>
    /// The languages as one list, the default first, each written as culture names are.
    /// </summary>
    // The parameter names are those of both public members that take languages.
    private static ImmutableArray<string> ToCultureNames(string defaultLanguage, ReadOnlySpan<string> otherLanguages)
    {
        var languages = ImmutableArray.CreateBuilder<string>(otherLanguages.Length + 1);
        languages.Add(ToCultureName(defaultLanguage, nameof(defaultLanguage)));

        foreach (var language in otherLanguages)
            languages.Add(ToCultureName(language, nameof(otherLanguages)));

        return languages.MoveToImmutable();
    }

    /// <summary>
    /// A language written as culture names are (<c>en-GB</c> for <c>EN-gb</c>), or an exception when it is none.
    /// </summary>
    private static string ToCultureName(string language, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(language, parameterName);

        if (!CultureName.IsValid(language))
            throw new ArgumentException($"\"{language}\" is no culture name: pass one such as en or en-GB.", parameterName);

        return CultureName.Normalize(language);
    }

    private static TranslationsRegistry Create(IEnumerable<ITranslations> sets, ImmutableArray<string> appLanguages)
    {
        ArgumentNullException.ThrowIfNull(sets);

        var ordered = sets.ToImmutableArray();

        var byLanguage = new Dictionary<string, List<ITranslations>>(StringComparer.OrdinalIgnoreCase);
        foreach (var translations in ordered)
        {
            var language = CultureName.Normalize(ReadLanguageCode(translations));

            if (!byLanguage.TryGetValue(language, out var list))
                byLanguage[language] = list = [];

            list.Add(translations);
        }

        return new TranslationsRegistry(ordered, appLanguages, byLanguage.ToFrozenDictionary(
            pair => pair.Key,
            pair => new LanguageSets([.. pair.Value]),
            StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds a set to the sets of your whole app, for a set that does not register itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// That is a set marked
    /// <see cref="ExcludeFromTranslationDiscoveryAttribute">[ExcludeFromTranslationDiscovery]</see>, or one in an
    /// assembly loaded at run time whose code nothing calls, such as a plugin's set for one of your app's languages.
    /// The set wins over the sets registered before it, for every text it changes: see <see cref="ITranslations"/>.
    /// </para>
    /// <para>
    /// A set for a language your app does not declare never shows: a culture outside <see cref="SupportedLanguages"/>
    /// gets the app's default language. Only <see cref="Find{TTranslations}(CultureInfo)"/> returns such a set.
    /// </para>
    /// </remarks>
    /// <typeparam name="TTranslations">The set.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that is not a culture name. Name the set after its language
    /// (<c>ShopTranslationsDE</c>), or have its <see cref="ITranslations.LanguageCode"/> return a culture name such as
    /// <c>en-GB</c>.
    /// </exception>
    public static void Register<TTranslations>()
        where TTranslations : class, ITranslations, new()
    {
        Register(new TTranslations());
    }

    /// <summary>
    /// Adds sets you created yourself to the sets of your whole app, such as a set that needs constructor arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each set wins over the sets registered before it, for every text it changes: see <see cref="ITranslations"/>.
    /// Registering a set of the same type and language again replaces the earlier one.
    /// </para>
    /// <para>
    /// A set for a language your app does not declare never shows: a culture outside <see cref="SupportedLanguages"/>
    /// gets the app's default language. Only <see cref="Find{TTranslations}(CultureInfo)"/> returns such a set.
    /// </para>
    /// </remarks>
    /// <param name="translations">
    /// The sets, in the order they apply: for one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that is not a culture name. Name the set after its language
    /// (<c>ShopTranslationsDE</c>), or have its <see cref="ITranslations.LanguageCode"/> return a culture name such as
    /// <c>en-GB</c>.
    /// </exception>
    public static void Register(params ReadOnlySpan<ITranslations> translations)
    {
        Add(Overrides, translations);
    }

    /// <summary>
    /// Adds a library's own sets to the sets of your whole app: what its texts show until an app rewords them. The
    /// source generator calls this for every set in the same assembly as its contract, so you need it only for a
    /// library's set that does not register itself.
    /// </summary>
    /// <remarks>
    /// A set from <see cref="Register(ReadOnlySpan{ITranslations})"/> always wins over these. Registering a set of the
    /// same type and language again replaces the earlier one.
    /// </remarks>
    /// <param name="translations">
    /// The sets, in the order they apply: for one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that is not a culture name. Name the set after its language
    /// (<c>ShopTranslationsDE</c>), or have its <see cref="ITranslations.LanguageCode"/> return a culture name such as
    /// <c>en-GB</c>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterDefaults(params ReadOnlySpan<ITranslations> translations)
    {
        Add(Defaults, translations);
    }

    /// <summary>
    /// Sets the app's <see cref="SupportedLanguages"/>, for <see cref="Internals.GeneratedCode.RegisterLanguages"/>.
    /// Only the first call counts.
    /// </summary>
    internal static void RegisterLanguages(string defaultLanguage, params ReadOnlySpan<string> otherLanguages)
    {
        // Checked as a registry of your own checks its languages, and here rather than at the first text shown.
        var languages = ToCultureNames(defaultLanguage, otherLanguages);

        lock (Gate)
        {
            // The app's own registrar runs before any assembly it loads, and calls this before the registrars of the
            // assemblies it references, so the first call is the app's.
            if (!SupportedLanguages.IsEmpty)
                return;

            SupportedLanguages = languages;

            // A new registry instance, so that a cache keyed on it (Formosa's) drops wording resolved before.
            Invalidate();
        }
    }

    /// <summary>
    /// Records the language a set's name gives it when the last two letters cannot hold it, for
    /// <see cref="Internals.GeneratedCode.RegisterLanguageCode"/>. Only the first call for a set counts.
    /// </summary>
    internal static void RegisterLanguageCode<TTranslations>(string languageCode)
        where TTranslations : class, ITranslations
    {
        // Checked here: a code that is no culture name would otherwise fail every text of the app once the lookup
        // tables are built again, far from this call.
        languageCode = ToCultureName(languageCode, nameof(languageCode));

        lock (Gate)
        {
            if (!LanguageCodes.TryAdd(typeof(TTranslations), languageCode))
                return;

            // A set registered before its code was recorded is filed under the language of its name until the lookup
            // tables are built again.
            Invalidate();
        }
    }

    /// <summary>
    /// The language code recorded for a set's type, or else for the nearest class it derives from that has one (a
    /// rewording of a British set serves British English too), or <see langword="null"/> when none has.
    /// </summary>
    internal static string? FindLanguageCode(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (LanguageCodes.TryGetValue(current, out var languageCode))
                return languageCode;

        return null;
    }

    /// <summary>
    /// The set implementing <typeparamref name="TTranslations"/> for a culture, or else for its parent culture
    /// (<c>en-GB</c> falls back to <c>en</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read a text from it to get that set's wording, whatever the current UI culture:
    /// <c>TranslationsRegistry.Current.Find&lt;IShopTranslations&gt;(culture)?.Checkout</c>. Unlike a text, this does
    /// not fall back to a default language: a culture without a set gives <see langword="null"/>. Among several sets
    /// for one language, this returns the one registered last.
    /// </para>
    /// <para>
    /// The set's wording is not always what a user sees, as a text can take its wording from another set: a rewording
    /// registered for <c>en</c> also shows for <c>en-GB</c>, for the texts the <c>en-GB</c> set does not change, while
    /// this returns the <c>en-GB</c> set. For the wording a user of the culture sees, call
    /// <see cref="LocalizableText.GetText(CultureInfo)"/> on the text instead.
    /// </para>
    /// </remarks>
    /// <typeparam name="TTranslations">The contract the set implements, such as <c>IShopTranslations</c>.</typeparam>
    /// <param name="culture">The culture to find a set for.</param>
    /// <returns>
    /// The set, or <see langword="null"/> when no set of the contract serves the culture.
    /// </returns>
    public TTranslations? Find<TTranslations>(CultureInfo culture)
        where TTranslations : class, ITranslations
    {
        return (TTranslations?)this.Find(typeof(TTranslations), culture);
    }

    /// <summary>
    /// The set implementing a contract for a culture, or else for its parent culture (<c>en-GB</c> falls back to
    /// <c>en</c>), for a contract you have as a <see cref="Type"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This works as <see cref="Find{TTranslations}(CultureInfo)"/> does, which is simpler when you know the contract
    /// at compile time.
    /// </para>
    /// </remarks>
    /// <param name="contract">The <see cref="ITranslations"/> interface the set implements.</param>
    /// <param name="culture">The culture to find a set for.</param>
    /// <returns>
    /// The set, or <see langword="null"/> when no set of the contract serves the culture.
    /// </returns>
    public ITranslations? Find(Type contract, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(culture);

        foreach (var name in new CultureFallback(culture))
            if (this.FindByLanguage(contract, name) is { } translations)
                return translations;

        return null;
    }

    /// <summary>
    /// The set implementing a contract that is registered for one language (an
    /// <see cref="ITranslations.LanguageCode"/>, matched in any letter case, without the culture walk), or
    /// <see langword="null"/> when none is. Among several sets for the language the last registered wins.
    /// </summary>
    internal ITranslations? FindByLanguage(Type contract, string language)
    {
        return this.ByLanguage.TryGetValue(language, out var sets) && sets.Of(contract) is [.., var last]
            ? last.Set
            : null;
    }

    /// <summary>
    /// The wording the sets of a contract give a text for the first of the names that has a set, or
    /// <see langword="null"/> when none has. Within a name, that is the wording of the set registered last that words
    /// the text differently than the set it derives from (<see cref="FindLevelWording"/>), so rewordings of one set
    /// registered one after another each show the texts they change. When the set it comes from words the text as a set
    /// of a less specific name it derives from does, the text takes the wording of that name instead, and so on up. So
    /// a set rewording a language reaches the regional sets built on that language, for every text they leave as it
    /// was.
    /// </summary>
    /// <param name="contract">The contract the text belongs to.</param>
    /// <param name="names">The names to try, most specific first.</param>
    /// <param name="select">Picks the text from a set of the contract.</param>
    // Compared by wording rather than by the class that declares the member, which would take reflection the trimmer
    // cannot see: a set that repeats the wording of the set it derives from gives way like one that inherits it. A set
    // deriving from no other set of its name, or a regional set deriving from no set of its language, gives way to
    // nothing, as it inherits none of its texts.
    internal string? FindWording(Type contract, CultureFallback names, Func<ITranslations, string> select)
    {
        ITranslations? found = null;
        string? wording = null;

        foreach (var name in names)
        {
            if (!this.ByLanguage.TryGetValue(name, out var sets))
                continue;

            var candidates = sets.Of(contract);

            if (found is not null)
            {
                if (FindBase(candidates, found) is not { } basis)
                    continue;

                if (!String.Equals(select(basis), wording, StringComparison.Ordinal))
                    return wording;
            }

            // Null only before the first name with a set of the contract: a basis is one of the candidates.
            if (FindLevelWording(candidates, select) is { } level)
                (found, wording) = level;
        }

        return wording;
    }

    /// <summary>
    /// The wording the sets of a contract for one name give a text, and the set it comes from: that of the set
    /// registered last that words it differently than the candidate it derives from, or that derives from none;
    /// <see langword="null"/> when the name has no set of the contract.
    /// </summary>
    private static (ITranslations Set, string Wording)? FindLevelWording(ImmutableArray<ContractSet> candidates,
        Func<ITranslations, string> select)
    {
        // Ends at the latest at the candidate deriving from no other, as no class derives from itself.
        for (var i = candidates.Length - 1; i >= 0; i--)
        {
            var (set, basis) = candidates[i];
            var wording = select(set);

            if (basis is not null && String.Equals(select(basis), wording, StringComparison.Ordinal))
                continue;

            return (set, wording);
        }

        return null;
    }

    /// <summary>
    /// The set registered last among the candidates that the class of <paramref name="derived"/> derives from, or
    /// <see langword="null"/>.
    /// </summary>
    private static ITranslations? FindBase(ImmutableArray<ContractSet> candidates, ITranslations derived)
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
    private readonly record struct ContractSet(ITranslations Set, ITranslations? Basis);

    /// <summary>
    /// The sets of one language, and per contract the ones implementing it.
    /// </summary>
    // A language holds the sets of every contract of the app, and a text needs only those of its own: found once per
    // contract and kept, so that a lookup does not test every set of the language against the contract, on every text
    // of every render. Never dropped, as a registry never changes: a registration builds a new one.
    private sealed class LanguageSets(ImmutableArray<ITranslations> sets)
    {
        private ImmutableArray<ITranslations> Sets { get; } = sets;

        private ConcurrentDictionary<Type, ImmutableArray<ContractSet>> ByContract { get; } = new();

        /// <summary>
        /// The sets of this language that implement a contract, in the order they apply, each with the set it derives
        /// from. Empty when none implements it.
        /// </summary>
        public ImmutableArray<ContractSet> Of(Type contract)
        {
            return this.ByContract.TryGetValue(contract, out var known)
                ? known
                : this.ByContract.GetOrAdd(contract, static (contract, sets) => Find(sets, contract), this.Sets);
        }

        private static ImmutableArray<ContractSet> Find(ImmutableArray<ITranslations> sets, Type contract)
        {
            var implementing = sets.Where(contract.IsInstanceOfType).ToImmutableArray();
            var found = ImmutableArray.CreateBuilder<ContractSet>(implementing.Length);

            foreach (var set in implementing)
            {
                var type = set.GetType();

                found.Add(new ContractSet(set, implementing.LastOrDefault(other => type.IsSubclassOf(other.GetType()))));
            }

            return found.MoveToImmutable();
        }
    }

    /// <summary>
    /// A set's language code, read once to file the set under it.
    /// </summary>
    private static string ReadLanguageCode(ITranslations translations)
    {
        var languageCode = translations.LanguageCode;

        // Checked here, as the lookup tables cannot hold a null key, and every text reads them: an unchecked null
        // would make each one throw, far from the registration that caused it. A code that is no culture name (en_GB,
        // or en with a space after it) would file the set under a language no culture reaches, without a word.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (languageCode is null || !CultureName.IsValid(languageCode))
        {
            var given = languageCode is null ? "null" : $"\"{languageCode}\"";

            throw new InvalidOperationException(
                $"{translations.GetType()} gives {given} as its {nameof(ITranslations)}.{nameof(ITranslations.LanguageCode)}: return a culture name, such as en or en-GB.");
        }

        return languageCode;
    }

    private static void Add(List<(ITranslations Set, string Language)> tier, ReadOnlySpan<ITranslations> translations)
    {
        // Read here so that a set without a language code fails at its registration, not at the first text rendered
        // afterwards, and before the gate is taken: a language code is consumer code.
        var entries = new (ITranslations Set, string Language)[translations.Length];
        for (var i = 0; i < translations.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(translations[i], nameof(translations));
            entries[i] = (translations[i], ReadLanguageCode(translations[i]));
        }

        lock (Gate)
        {
            // Moved to the end rather than kept in place, so the last registration wins; matched on type and language
            // so that registering again (a second service provider, a test) cannot grow the list without bound. On the
            // language each set registered with: a set whose code was recorded only after it registered
            // (RegisterLanguageCode) is kept once next to a later registration of its type, and no more than once.
            foreach (var entry in entries)
            {
                tier.RemoveAll(existing => existing.Set.GetType() == entry.Set.GetType()
                                           && String.Equals(existing.Language, entry.Language, StringComparison.OrdinalIgnoreCase));
                tier.Add(entry);
            }

            Invalidate();
        }
    }

    /// <summary>
    /// Drops the lookup tables, so that the next read of <see cref="Current"/> builds them again. Called under the
    /// gate.
    /// </summary>
    private static void Invalidate()
    {
        Version++;
        Snapshot = null;
    }
}
