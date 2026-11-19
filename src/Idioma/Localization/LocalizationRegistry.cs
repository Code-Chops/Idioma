namespace CodeChops.Idioma;

/// <summary>
/// All the <see cref="ILocalizationSet">sets</see> of your app, by language. A text finds its wording for the current
/// UI culture here. You rarely need this class, because every set is registered automatically.
/// </summary>
/// <remarks>
/// <para>
/// Use this class to:
/// </para>
/// <list type="bullet">
/// <item>Register a set that isn't registered automatically, such as one that needs constructor arguments. Mark it
/// <see cref="ExcludeFromLocalizationDiscoveryAttribute">[ExcludeFromLocalizationDiscovery]</see> and pass an instance to
/// <see cref="Register(ReadOnlySpan{ILocalizationSet})"/>.</item>
/// <item>Read a set directly with <see cref="Find{TContract}(CultureInfo)"/>, such as for the Dutch wording of a text
/// whatever the current UI culture.</item>
/// <item>Give part of an app, or a test, sets of its own with
/// <see cref="Create(IEnumerable{ILocalizationSet})"/>.</item>
/// </list>
/// <para>
/// When several sets word a text for one language, the last set in <see cref="Sets"/> that changes the text wins. See
/// <see cref="ILocalizationSet"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A set that takes the shop's name from configuration, so it needs a constructor argument.
/// [ExcludeFromLocalizationDiscovery]
/// public sealed class BrandedShopLocalizationEn(string shopName) : ShopLocalizationEn
/// {
///     public override string Checkout => $"Pay at {shopName}";
/// }
///
/// // At startup:
/// LocalizationRegistry.Register(new BrandedShopLocalizationEn(configuration["ShopName"]!));
///
/// // The Dutch wording of one text, whatever the current UI culture:
/// var dutch = CultureInfo.GetCultureInfo("nl");
/// string? checkout = LocalizationRegistry.Current.Find&lt;IShopLocalization&gt;(dutch)?.Checkout;
/// </code>
/// </example>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// The sets of your whole app: every set that registered itself, and every set you registered. A text looks up its
    /// wording here. So this is what <c>@ShopLocalization.Checkout</c> in markup shows.
    /// </summary>
    public static LocalizationRegistry Current => Volatile.Read(ref Registered).Registry;

    /// <summary>
    /// Your app's languages, its default language first, as its
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see> declares them. Empty in an app that
    /// declares no languages.
    /// </summary>
    /// <remarks>
    /// A culture none of these languages serves gets the wording in <see cref="DefaultLanguage"/>, for every text. For
    /// example, in an app that declares <c>("en", "fr")</c>, a Dutch user sees a library's texts in English, like the
    /// app's own, even though the library has Dutch. See <see cref="LocalizableText"/>.
    /// </remarks>
    public static ImmutableArray<string> SupportedLanguages => Volatile.Read(ref Registered).Languages;

    /// <summary>
    /// Your app's default language, the first language it declares. For example, <c>nl</c> for
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages("nl", "en")]</see>. <see langword="null"/>
    /// in an app that declares no languages.
    /// </summary>
    /// <remarks>
    /// Every text falls back to this language, including the texts of the libraries your app uses. See
    /// <see cref="LocalizableText"/>. In an app that declares no languages, each text falls back to its own default
    /// language instead.
    /// </remarks>
    public static string? DefaultLanguage => SupportedLanguages is [var defaultLanguage, ..] ? defaultLanguage : null;

    /// <summary>
    /// A function that gives the user's culture, for an app that keeps it somewhere other than
    /// <see cref="CultureInfo.CurrentUICulture"/>. Every text then shows the wording for the culture it returns.
    /// <see langword="null"/> by default, which leaves texts on <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set it once, at startup. It applies to every text of your app, including the texts of the libraries it uses.
    /// The function runs each time a text is shown, so keep it fast. When it returns <see langword="null"/>, the text
    /// follows <see cref="CultureInfo.CurrentUICulture"/> for that call.
    /// </para>
    /// <para>
    /// A text that takes values, such as <c>ShopLocalization.Total(amount)</c>, formats them for the same culture, so
    /// a date or a number reads as the user writes it. <see cref="LocalizableText.ToString(CultureInfo)"/> still shows
    /// the wording for the culture you pass.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // At startup, in an app that keeps each user's language in their profile:
    /// LocalizationRegistry.CurrentUICultureProvider = () => UserProfile.Current?.Culture;
    /// </code>
    /// </example>
    // ReSharper disable once InconsistentNaming - Same casing as CultureInfo.CurrentUICulture.
    public static Func<CultureInfo?>? CurrentUICultureProvider { get; set; }

    /// <summary>
    /// The sets of this registry, in the order they apply. For one contract and language, a later set wins for every
    /// text it changes. In <see cref="Current"/>, the sets declared next to their contract come first. The sets that
    /// reword another assembly's texts and the sets you registered come after them.
    /// </summary>
    public ImmutableArray<ILocalizationSet> Sets { get; }

    /// <summary>
    /// The languages this registry has at least one set for, such as <c>en</c> and <c>nl</c>, as each set's
    /// <see cref="ILocalizationSet.LanguageCode"/> gives them. In <see cref="Current"/>, that includes the languages of
    /// the libraries your app uses. So it can list more languages than your app declares.
    /// </summary>
    /// <remarks>
    /// Each language is written as culture names are, whatever letter case its sets use. For example, <c>en-GB</c> for
    /// a set whose code is <c>EN-gb</c>.
    /// </remarks>
    public IReadOnlyCollection<string> Languages { get; }

    /// <summary>
    /// The languages a text read from this registry shows, the default language first. In <see cref="Current"/>, these
    /// are your app's <see cref="SupportedLanguages"/>. In a registry of your own, they're the languages you passed to
    /// <see cref="Create(IEnumerable{ILocalizationSet}, string, ReadOnlySpan{string})"/>. Empty when none were given.
    /// </summary>
    /// <remarks>
    /// A culture none of these languages serves gets the wording in the first of them, for every text. A registry
    /// without languages shows every language it has a set for. There, a culture without wording gets each text's own
    /// default language.
    /// </remarks>
    // Internal: for Current it repeats SupportedLanguages, and a registry of your own has the languages you passed.
    internal ImmutableArray<string> AppLanguages { get; }

    private FrozenDictionary<string, LanguageSets> ByLanguage { get; }

    private LocalizationRegistry(ImmutableArray<ILocalizationSet> sets, ImmutableArray<string> appLanguages,
        FrozenDictionary<string, LanguageSets> byLanguage)
    {
        ArgumentNullException.ThrowIfNull(byLanguage);

        this.Sets = sets;
        this.AppLanguages = appLanguages;
        this.ByLanguage = byLanguage;
        this.Languages = byLanguage.Keys;
    }

    /// <summary>
    /// A registry of exactly the given sets, apart from the sets of your whole app. Use it for a part of an app that
    /// keeps sets of its own, or a test that needs exact sets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read a text's wording from it with <see cref="LocalizableText.ToString(CultureInfo, LocalizationRegistry)"/>. To
    /// add sets to your whole app, use <see cref="Register(ReadOnlySpan{ILocalizationSet})"/> instead.
    /// </para>
    /// <para>
    /// The registry stands apart from your app's languages too. A text read from it shows every language the registry
    /// has a set for, and a culture without wording gets the text's own default language. To give the registry
    /// languages of its own, pass them to
    /// <see cref="Create(IEnumerable{ILocalizationSet}, string, ReadOnlySpan{string})"/>.
    /// </para>
    /// </remarks>
    /// <param name="sets">
    /// The sets, in the order they apply. For one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="sets"/> or one of the sets is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that isn't a culture name. Name the set after its language
    /// (<c>ShopLocalizationDe</c>), or have its <see cref="ILocalizationSet.LanguageCode"/> return a culture name such
    /// as <c>en-GB</c>.
    /// </exception>
    public static LocalizationRegistry Create(IEnumerable<ILocalizationSet> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);

        return Create(sets, appLanguages: []);
    }

    /// <summary>
    /// A registry of exactly the given sets with languages of its own, the default language first. A text read from it
    /// treats these languages as <see cref="Current"/> treats those of your app. For example:
    /// <c>LocalizationRegistry.Create(sets, "en", "nl")</c>.
    /// </summary>
    /// <remarks>
    /// A culture none of the languages serves gets the wording in the default language, for every text, even where the
    /// registry has a set for that culture. So does a culture that a text has no wording for.
    /// </remarks>
    /// <param name="sets">
    /// The sets, in the order they apply. For one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <param name="defaultLanguage">The default language, a culture name such as <c>en</c> or <c>en-GB</c>.</param>
    /// <param name="otherLanguages">The other languages, each a culture name.</param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="sets"/>, one of the sets, or a language is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// When a language isn't a culture name, such as an empty string or <c>en_GB</c>. Pass a culture name such as
    /// <c>en</c> or <c>en-GB</c>. Also when a language is listed twice, as <c>en</c> and <c>EN</c> are.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that isn't a culture name. Name the set after its language
    /// (<c>ShopLocalizationDe</c>), or have its <see cref="ILocalizationSet.LanguageCode"/> return a culture name such
    /// as <c>en-GB</c>.
    /// </exception>
    public static LocalizationRegistry Create(IEnumerable<ILocalizationSet> sets, string defaultLanguage,
        params ReadOnlySpan<string> otherLanguages)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(defaultLanguage);

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
        {
            var culture = ToCultureName(language, nameof(otherLanguages));

            if (languages.Contains(culture))
                throw new ArgumentException($"'{culture}' is listed twice. List each language once.", nameof(otherLanguages));

            languages.Add(culture);
        }

        return languages.MoveToImmutable();
    }

    /// <summary>
    /// A language written as culture names are, such as <c>en-GB</c> for <c>EN-gb</c>. Throws when it isn't a culture
    /// name.
    /// </summary>
    private static string ToCultureName(string language, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(language, parameterName);

        if (!CultureName.IsValid(language))
            throw new ArgumentException($"\"{language}\" isn't a culture name. Pass one such as en or en-GB.", parameterName);

        var culture = CultureName.Normalize(language);
        return culture;
    }

    /// <summary>
    /// A registry of the given sets, filed by the language each set gives, with the given app languages. Both public
    /// <c>Create</c> overloads end here, and so does the registry of the whole app (<see cref="Current"/>).
    /// </summary>
    /// <param name="sets">
    /// The sets, in the order they apply. Within a language they keep that order, so a later set wins.
    /// </param>
    /// <param name="appLanguages">
    /// The languages a text read from the registry shows, the default first, each written as culture names are. Empty
    /// for a registry that shows every language it has a set for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="sets"/> or one of the sets is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that isn't a culture name.
    /// </exception>
    // Each set's LanguageCode is read once, here. It's consumer code, and every lookup afterwards goes by the language
    // the set is filed under.
    private static LocalizationRegistry Create(IEnumerable<ILocalizationSet> sets, ImmutableArray<string> appLanguages)
    {
        ArgumentNullException.ThrowIfNull(sets);

        var ordered = sets.ToImmutableArray();

        var byLanguage = new Dictionary<string, List<ILocalizationSet>>(StringComparer.OrdinalIgnoreCase);
        foreach (var set in ordered)
        {
            ArgumentNullException.ThrowIfNull(set, nameof(sets));

            var language = CultureName.Normalize(ReadLanguageCode(set));

            if (!byLanguage.TryGetValue(language, out var list))
                byLanguage[language] = list = [];

            list.Add(set);
        }

        // Filled first and then frozen, not frozen through selectors. That overload is a generic method over
        // KeyValuePair, a struct, so ReadyToRun can't compile it outside the framework and every app would JIT it.
        var languageSets = new Dictionary<string, LanguageSets>(byLanguage.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in byLanguage)
            languageSets[pair.Key] = new LanguageSets([.. pair.Value]);

        var registry = new LocalizationRegistry(
            sets: ordered,
            appLanguages: appLanguages,
            byLanguage: languageSets.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));

        return registry;
    }

    /// <summary>
    /// The set implementing <typeparamref name="TContract"/> for a culture, or else for its parent culture
    /// (<c>en-GB</c> falls back to <c>en</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read a text from it to get that set's wording, whatever the current UI culture. For example:
    /// <c>LocalizationRegistry.Current.Find&lt;IShopLocalization&gt;(culture)?.Checkout</c>. Unlike a text, this
    /// doesn't fall back to a default language. A culture without a set gives <see langword="null"/>. Among several
    /// sets for one language, this returns the last one in <see cref="Sets"/>.
    /// </para>
    /// <para>
    /// The set's wording isn't always what a user sees, because a text can take its wording from another set. For
    /// example, a rewording registered for <c>en</c> also shows for <c>en-GB</c>, for the texts the <c>en-GB</c> set
    /// doesn't change. This method still returns the <c>en-GB</c> set. For the wording a user of the culture sees, call
    /// <see cref="LocalizableText.ToString(CultureInfo)"/> on the text instead.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContract">The contract the set implements, such as <c>IShopLocalization</c>.</typeparam>
    /// <param name="culture">The culture to find a set for.</param>
    /// <returns>
    /// The set, or <see langword="null"/> when no set of the contract serves the culture.
    /// </returns>
    /// <exception cref="ArgumentNullException">When <paramref name="culture"/> is <see langword="null"/>.</exception>
    public TContract? Find<TContract>(CultureInfo culture)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(culture);

        var contract = (TContract?)this.Find(typeof(TContract), culture);
        return contract;
    }

    /// <summary>
    /// The set implementing a contract for a culture, or else for its parent culture (<c>en-GB</c> falls back to
    /// <c>en</c>). Use it for a contract you have as a <see cref="Type"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This works as <see cref="Find{TContract}(CultureInfo)"/> does. That one is simpler when you know the contract
    /// at compile time.
    /// </para>
    /// </remarks>
    /// <param name="contract">The contract the set implements, such as <c>typeof(IShopLocalization)</c>.</param>
    /// <param name="culture">The culture to find a set for.</param>
    /// <returns>
    /// The set, or <see langword="null"/> when no set of the contract serves the culture.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="contract"/> or <paramref name="culture"/> is <see langword="null"/>.
    /// </exception>
    public ILocalizationSet? Find(Type contract, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(culture);

        foreach (var name in new CultureFallback(culture))
            if (this.FindByLanguage(contract, name) is { } set)
                return set;

        return null;
    }

    /// <summary>
    /// The set implementing a contract that is registered for one language, or <see langword="null"/> when none is. The
    /// language is an <see cref="ILocalizationSet.LanguageCode"/>, matched in any letter case, without the culture walk.
    /// Among several sets for the language, the last in <see cref="Sets"/> wins.
    /// </summary>
    internal ILocalizationSet? FindByLanguage(Type contract, string language)
    {
        var set = this.ByLanguage.TryGetValue(language, out var sets) && sets.Of(contract) is [.., var last]
            ? last.Set
            : null;

        return set;
    }

    /// <summary>
    /// A set's language code, read once to file the set under it.
    /// </summary>
    private static string ReadLanguageCode(ILocalizationSet set)
    {
        var languageCode = set.LanguageCode;

        // Checked here, as the lookup tables can't hold a null key, and every text reads them: an unchecked null
        // would make each one throw, far from the registration that caused it. A code that is no culture name (en_GB,
        // or en with a space after it) would file the set under a language no culture reaches, without a word.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (languageCode is null || !CultureName.IsValid(languageCode))
        {
            var given = languageCode is null ? "null" : $"\"{languageCode}\"";

            throw new InvalidOperationException($"{set.GetType()} gives {given} as its {nameof(ILocalizationSet)}.{nameof(ILocalizationSet.LanguageCode)}. Return a culture name, such as en or en-GB.");
        }

        return languageCode;
    }
}
