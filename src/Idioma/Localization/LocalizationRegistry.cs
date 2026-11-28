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
/// <item>Read a set directly with <see cref="GetLocalizationSet{TContract}(CultureInfo)"/>, such as for the Dutch
/// wording of a text whatever the current UI culture.</item>
/// </list>
/// <para>
/// For each contract and language, one set wins and words every text. See <see cref="ILocalizationSet"/> for which
/// one.
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
/// string? checkout = LocalizationRegistry.GetLocalizationSet&lt;IShopLocalization&gt;(dutch)?.Checkout;
/// </code>
/// </example>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// The sets of the whole app, which every text reads.
    /// </summary>
    internal static LocalizationRegistry Current => Volatile.Read(ref Registered).Registry;

    /// <summary>
    /// Your app's languages, its default language first, as its
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see> declares them. Empty only in a
    /// process that no app of yours started, such as a host written in F# that loads your library.
    /// </summary>
    /// <remarks>
    /// A culture none of these languages serves gets the wording in <see cref="DefaultLanguage"/>, for every text. For
    /// example, in an app that declares <c>("en", "fr")</c>, a Dutch user sees a library's texts in English, like the
    /// app's own, even though the library has Dutch. See <see cref="LocalizedText"/>.
    /// </remarks>
    public static ImmutableArray<string> SupportedLanguages => Volatile.Read(ref Registered).Languages;

    /// <summary>
    /// Your app's default language, the first language it declares. For example, <c>nl</c> for
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages("nl", "en")]</see>. <see langword="null"/>
    /// only where <see cref="SupportedLanguages"/> is empty.
    /// </summary>
    /// <remarks>
    /// Every text falls back to this language, including the texts of the libraries your app uses. See
    /// <see cref="LocalizedText"/>. Where it's <see langword="null"/>, each text falls back to its own default
    /// language instead.
    /// </remarks>
    public static string? DefaultLanguage => SupportedLanguages is [var defaultLanguage, ..] ? defaultLanguage : null;

    /// <summary>
    /// The sets of your whole app, in the order they apply: every set that registered itself, and every set you
    /// registered. For one contract and language, the last set wins and words every text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A library's own sets come first. The sets that reword another assembly's sets come after them, the app's last.
    /// The sets you registered come at the end.
    /// </para>
    /// <para>
    /// A set that registered itself is created when a text of its contract is first shown. Reading this property
    /// creates every set that isn't created yet.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// When the constructor of a set that registered itself throws, with that exception inside.
    /// </exception>
    public static ImmutableArray<ILocalizationSet> Sets => Current.AllSets;

    /// <summary>
    /// The languages your app has at least one set for, such as <c>en</c> and <c>nl</c>. That includes the languages of the libraries
    /// your app uses. So it can list more languages than <see cref="SupportedLanguages"/>, which are the ones your users
    /// see.
    /// </summary>
    /// <remarks>
    /// Each language is written as culture names are, whatever letter case its sets use. For example, <c>en-GB</c> for
    /// a set named <c>ShopLocalizationENGB</c>. The languages are in ordinal order, so <c>en</c> comes before <c>en-GB</c>.
    /// </remarks>
    public static ImmutableArray<string> RegisteredLanguages => Current.SetLanguages;

    /// <summary>
    /// Creates every set that isn't created yet.
    /// </summary>
    internal ImmutableArray<ILocalizationSet> AllSets
    {
        get
        {
            // Kept once all could be created, so a constructor that throws runs again at the next read.
            if (field is { IsDefault: false } sets)
                return sets;

            sets = [.. this.SetRegistrations.Select(static registration => registration.Set)];
            field = sets;

            return sets;
        }
    }

    /// <summary>
    /// In the order they apply, each created when a text of its contract is first shown.
    /// </summary>
    private ImmutableArray<Registration> SetRegistrations { get; }

    /// <summary>
    /// Each written as culture names are, in ordinal order.
    /// </summary>
    internal ImmutableArray<string> SetLanguages { get; }

    /// <summary>
    /// The languages a text read from this registry shows, the default first: <see cref="SupportedLanguages"/> for
    /// <see cref="Current"/>, or a test's. Empty for a registry that shows every language it has a set for, where a
    /// culture without wording gets each text's own default language.
    /// </summary>
    internal ImmutableArray<string> AppLanguages { get; }

    /// <summary>
    /// The default script of each language the build knows one for, such as <c>Hant</c> for <c>zh-TW</c>. The languages
    /// are <see cref="AppLanguages"/> and those of the described sets. A culture with its script tries its name without
    /// it by these (<see cref="CultureFallback"/>).
    /// </summary>
    internal FrozenDictionary<string, string> AppScripts { get; }

    // Never changed once the registry has it, so threads read it without a lock.
    private Dictionary<string, SetsOfLanguage> SetsByLanguage { get; }

    private LocalizationRegistry(ImmutableArray<Registration> registrations, ImmutableArray<string> appLanguages,
        FrozenDictionary<string, string> appScripts, Dictionary<string, SetsOfLanguage> setsByLanguage)
    {
        ArgumentNullException.ThrowIfNull(setsByLanguage);

        this.SetRegistrations = registrations;
        this.AppLanguages = appLanguages;
        this.AppScripts = appScripts;
        this.SetsByLanguage = setsByLanguage;
        // Sorted, because a dictionary's key order can't be relied on.
        this.SetLanguages = setsByLanguage.Keys.ToImmutableArray().Sort(StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether one of your app's languages serves a culture. That's so when the culture, or a culture it falls back to,
    /// is that language or a region of it. For example, <c>en</c> serves <c>en-GB</c>, and <c>zh-TW</c> serves
    /// <c>zh-Hant-TW</c>. A culture that no language serves gets the wording in <see cref="DefaultLanguage"/>, for every
    /// text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It follows the same fallback order as a text, so it agrees with the wording a user sees. A Blazor WebAssembly app
    /// can use it to make its default language the UI culture of a user whose language it doesn't have.
    /// </para>
    /// <para>
    /// It's <see langword="true"/> for every culture where <see cref="SupportedLanguages"/> is empty, because each text
    /// then shows the wording of the culture itself.
    /// </para>
    /// </remarks>
    /// <param name="culture">The culture, such as the user's.</param>
    public static bool IsServed(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var isServed = Current.Resolve(culture).ShowsCulture;
        return isServed;
    }

    /// <summary>
    /// Makes a culture the one every text follows until the scope is disposed, such as the culture of an email's
    /// recipient. Wrap the block that reads the texts: <c>using (LocalizationRegistry.UseCulture(dutch)) { ... }</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The culture chooses the wording and formats dates and numbers. The scope follows your code across an
    /// <see langword="await"/>. See <see cref="CultureScope"/>.
    /// </para>
    /// <para>
    /// Entering a scope copies the execution context. So wrap a block of texts, not each text.
    /// </para>
    /// </remarks>
    /// <param name="culture">The culture texts follow inside the scope.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="culture"/> is <see langword="null"/>.</exception>
    public static CultureScope UseCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var scope = new CultureScope(culture);
        return scope;
    }

    /// <summary>
    /// The set implementing <typeparamref name="TContract"/> for a culture, or else for its parent culture
    /// (<c>en-GB</c> falls back to <c>en</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read a text from it to get that set's wording, whatever the current UI culture. For example:
    /// <c>LocalizationRegistry.GetLocalizationSet&lt;IShopLocalization&gt;(culture)?.Checkout</c>. It's the set a user
    /// of the culture sees, where one of your app's languages serves the culture. Among several sets for one language,
    /// it's the one that wins (see <see cref="ILocalizationSet"/>).
    /// </para>
    /// <para>
    /// Unlike a text, this doesn't fall back to a default language. A culture without a set gives
    /// <see langword="null"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContract">The contract the set implements, such as <c>IShopLocalization</c>.</typeparam>
    /// <param name="culture">The culture to find a set for.</param>
    /// <returns>
    /// The set, or <see langword="null"/> when no set of the contract serves the culture.
    /// </returns>
    /// <exception cref="ArgumentNullException">When <paramref name="culture"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// When the constructor of a set that registered itself throws, with that exception inside.
    /// </exception>
    public static TContract? GetLocalizationSet<TContract>(CultureInfo culture)
        where TContract : class
    {
        return Current.GetByCulture<TContract>(culture);
    }

    /// <summary>
    /// The set implementing a contract for a culture, or else for its parent culture (<c>en-GB</c> falls back to
    /// <c>en</c>). Use it for a contract you have as a <see cref="Type"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This works as <see cref="GetLocalizationSet{TContract}(CultureInfo)"/> does. That one is simpler when you know
    /// the contract at compile time.
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
    /// <exception cref="InvalidOperationException">
    /// When the constructor of a set that registered itself throws, with that exception inside.
    /// </exception>
    public static ILocalizationSet? GetLocalizationSet(Type contract, CultureInfo culture)
    {
        return Current.GetByCulture(contract, culture);
    }

    /// <summary>
    /// A registry of exactly the given sets, in the order they apply, for a test. The texts of a generated class always
    /// read <see cref="Current"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">When a set has no language.</exception>
    internal static LocalizationRegistry Create(IEnumerable<ILocalizationSet> sets)
    {
        ArgumentNullException.ThrowIfNull(sets);

        return Create(sets, appLanguages: [], FrozenDictionary<string, string>.Empty);
    }

    /// <summary>
    /// A registry of exactly the given sets with languages of its own, the default first, which it treats as
    /// <see cref="Current"/> treats the app's. The tests of the fallback order read it through <see cref="Word"/>.
    /// Each language is written as the generated code writes it, such as <c>en-GB</c>, and has no script.
    /// </summary>
    /// <exception cref="ArgumentException">When a language is listed twice.</exception>
    /// <exception cref="InvalidOperationException">When a set has no language.</exception>
    internal static LocalizationRegistry Create(IEnumerable<ILocalizationSet> sets, string defaultLanguage,
        params ReadOnlySpan<string> otherLanguages)
    {
        return Create(sets, FrozenDictionary<string, string>.Empty, defaultLanguage, otherLanguages);
    }

    /// <summary>
    /// A registry as <see cref="Create(IEnumerable{ILocalizationSet}, string, ReadOnlySpan{string})"/> creates it, with
    /// the default script of some of its languages, as the generated code gives them (<see cref="RegisterLanguages"/>).
    /// </summary>
    /// <exception cref="ArgumentException">When a language is listed twice.</exception>
    /// <exception cref="InvalidOperationException">When a set has no language.</exception>
    internal static LocalizationRegistry Create(IEnumerable<ILocalizationSet> sets, FrozenDictionary<string, string> scripts,
        string defaultLanguage, params ReadOnlySpan<string> otherLanguages)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(scripts);
        ArgumentNullException.ThrowIfNull(defaultLanguage);

        // A test passes its codes itself, so a mistake there is caught here rather than in a fallback order.
        var languages = ImmutableArray.CreateBuilder<string>(otherLanguages.Length + 1);
        languages.Add(defaultLanguage);

        foreach (var language in otherLanguages)
        {
            ArgumentNullException.ThrowIfNull(language, nameof(otherLanguages));

            if (languages.Contains(language, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"'{language}' is listed twice. List each language once.", nameof(otherLanguages));

            languages.Add(language);
        }

        return Create(sets, languages.MoveToImmutable(), scripts);
    }

    private static LocalizationRegistry Create(IEnumerable<ILocalizationSet> sets, ImmutableArray<string> appLanguages,
        FrozenDictionary<string, string> appScripts)
    {
        ArgumentNullException.ThrowIfNull(sets);

        var registrations = ImmutableArray.CreateBuilder<Registration>();

        foreach (var set in sets)
        {
            ArgumentNullException.ThrowIfNull(set, nameof(sets));
            registrations.Add(Registration.Of(set, RegistrationTier.ByHand));
        }

        return Create(registrations.ToImmutable(), appLanguages, appScripts);
    }

    /// <summary>
    /// Files each set under the language of its type without creating it, keeping their order, so a later set wins.
    /// <see cref="Current"/> is built here again after every registration.
    /// </summary>
    private static LocalizationRegistry Create(ImmutableArray<Registration> registrations, ImmutableArray<string> appLanguages,
        FrozenDictionary<string, string> appScripts)
    {
        var byLanguage = new Dictionary<string, List<Registration>>(StringComparer.OrdinalIgnoreCase);

        foreach (var registration in registrations)
        {
            // Read from the type again, so a set the build described after it registered is filed under that language.
            var language = GetLanguageCode(registration.Type);

            if (!byLanguage.TryGetValue(language, out var list))
                byLanguage[language] = list = [];

            list.Add(registration);
        }

        var setsByLanguage = new Dictionary<string, SetsOfLanguage>(byLanguage.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in byLanguage)
            setsByLanguage[pair.Key] = new SetsOfLanguage([.. pair.Value]);

        var registry = new LocalizationRegistry(registrations, appLanguages, appScripts, setsByLanguage);
        return registry;
    }

    internal TContract? GetByCulture<TContract>(CultureInfo culture)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(culture);

        var contract = (TContract?)this.GetByCulture(typeof(TContract), culture);
        return contract;
    }

    // Through the lookup a text reads too (Resolve), so it's worked out once per culture name.
    internal ILocalizationSet? GetByCulture(Type contract, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(culture);

        var set = FindSet(this.Resolve(culture), contract);
        return set;
    }
}
