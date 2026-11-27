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
/// string? checkout = LocalizationRegistry.GetLocalizationSet&lt;IShopLocalization&gt;(dutch)?.Checkout;
/// </code>
/// </example>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// The sets of your whole app: every set that registered itself, and every set you registered. A text looks up its
    /// wording here. So this is what <c>@ShopLocalization.Checkout</c> in markup shows.
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
    /// registered. For one contract and language, a later set wins for every text it changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sets declared next to their contract come first. The sets that reword another assembly's texts and the sets
    /// you registered come after them.
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
    /// The languages your app has at least one set for, such as <c>en</c> and <c>nl</c>, as each set's
    /// <see cref="ILocalizationSet.LocalizationLanguageCode"/> gives them. That includes the languages of the libraries
    /// your app uses. So it can list more languages than <see cref="SupportedLanguages"/>, which are the ones your users
    /// see.
    /// </summary>
    /// <remarks>
    /// Each language is written as culture names are, whatever letter case its sets use. For example, <c>en-GB</c> for
    /// a set whose code is <c>EN-gb</c>. The languages are in ordinal order, so <c>en</c> comes before <c>en-GB</c>.
    /// </remarks>
    public static ImmutableArray<string> RegisteredLanguages => Current.SetLanguages;

    /// <summary>
    /// The sets of this registry, in the order they apply. <see cref="Sets"/> gives those of <see cref="Current"/>.
    /// Reading this property creates every set that isn't created yet.
    /// </summary>
    internal ImmutableArray<ILocalizationSet> AllSets
    {
        get
        {
            // Built once all could be created. A constructor that throws runs again at the next read.
            if (field is { IsDefault: false } sets)
                return sets;

            sets = [.. this.SetRegistrations.Select(static registration => registration.Set)];
            field = sets;

            return sets;
        }
    }

    /// <summary>
    /// The registered sets, in the order they apply, each created when a text of its contract is first shown.
    /// </summary>
    private ImmutableArray<Registration> SetRegistrations { get; }

    /// <summary>
    /// The languages this registry has at least one set for, each written as culture names are, in ordinal order.
    /// <see cref="RegisteredLanguages"/> gives those of <see cref="Current"/>.
    /// </summary>
    internal ImmutableArray<string> SetLanguages { get; }

    /// <summary>
    /// The languages a text read from this registry shows, the default language first. In <see cref="Current"/>, these
    /// are your app's <see cref="SupportedLanguages"/>. In a registry a test creates, they're the languages passed to
    /// <see cref="Create(IEnumerable{ILocalizationSet}, string, ReadOnlySpan{string})"/>. Empty when none were given.
    /// </summary>
    /// <remarks>
    /// A culture none of these languages serves gets the wording in the first of them, for every text. A registry
    /// without languages shows every language it has a set for. There, a culture without wording gets each text's own
    /// default language.
    /// </remarks>
    // Internal: for Current it repeats SupportedLanguages, and only a test creates a registry with other languages.
    internal ImmutableArray<string> AppLanguages { get; }

    /// <summary>
    /// The registered sets of each language in <see cref="SetLanguages"/>, matched in any letter case.
    /// </summary>
    // Never changed once the registry has it, so threads read it without a lock. Each culture reads it once, as its
    // lookup is worked out (CultureLookup).
    private Dictionary<string, SetsOfLanguage> SetsByLanguage { get; }

    private LocalizationRegistry(ImmutableArray<Registration> registrations, ImmutableArray<string> appLanguages,
        Dictionary<string, SetsOfLanguage> setsByLanguage)
    {
        ArgumentNullException.ThrowIfNull(setsByLanguage);

        this.SetRegistrations = registrations;
        this.AppLanguages = appLanguages;
        this.SetsByLanguage = setsByLanguage;
        // Sorted, because a dictionary gives its keys in an order of its own, which a caller can't rely on.
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
    /// <c>LocalizationRegistry.GetLocalizationSet&lt;IShopLocalization&gt;(culture)?.Checkout</c>. Unlike a text, this
    /// doesn't fall back to a default language. A culture without a set gives <see langword="null"/>. Among several
    /// sets for one language, this returns the last one in <see cref="Sets"/>.
    /// </para>
    /// <para>
    /// The set's wording isn't always what a user sees, because a text can take its wording from another set. For
    /// example, a rewording registered for <c>en</c> also shows for <c>en-GB</c>, for the texts the <c>en-GB</c> set
    /// doesn't change. This method still returns the <c>en-GB</c> set. For the wording a user of the culture sees, read
    /// the text of the generated class inside <see cref="UseCulture"/>.
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
    /// A registry of exactly the given sets, apart from the sets of the whole app, for a test that needs exact sets. The
    /// texts of a generated class, such as <c>ShopLocalization.Checkout</c>, always read <see cref="Current"/>.
    /// </summary>
    /// <param name="sets">
    /// The sets, in the order they apply. For one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="sets"/> or one of the sets is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that isn't a culture name. Name the set after its language
    /// (<c>ShopLocalizationDe</c>), or have its <see cref="ILocalizationSet.LocalizationLanguageCode"/> return a
    /// culture name such as <c>en-GB</c>.
    /// </exception>
    // Internal: a registry of your own could only give back the sets you passed it, as no public member reads a text
    // from it.
    internal static LocalizationRegistry Create(IEnumerable<ILocalizationSet> sets)
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
    /// (<c>ShopLocalizationDe</c>), or have its <see cref="ILocalizationSet.LocalizationLanguageCode"/> return a
    /// culture name such as <c>en-GB</c>.
    /// </exception>
    // The tests of the fallback order read through LocalizationRegistry.Word.
    internal static LocalizationRegistry Create(IEnumerable<ILocalizationSet> sets, string defaultLanguage,
        params ReadOnlySpan<string> otherLanguages)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(defaultLanguage);

        return Create(sets, ToCultureNames(defaultLanguage, otherLanguages));
    }

    /// <summary>
    /// The languages as one list, the default first, each written as culture names are.
    /// </summary>
    // The parameter names are those of both members that take languages: the internal Create, which the tests call,
    // and RegisterLanguages.
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
            throw new ArgumentException($"'{language}' isn't a culture name. Pass one such as en or en-GB.", parameterName);

        var culture = CultureName.Normalize(language);
        return culture;
    }

    /// <summary>
    /// A registry of the given sets, filed by the language each set gives, with the given app languages. Both internal
    /// <c>Create</c> overloads end here, which the tests call.
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
    // Each set's LocalizationLanguageCode is read here, as it registers. It's consumer code, and every lookup afterwards
    // goes by the language the set is filed under.
    private static LocalizationRegistry Create(IEnumerable<ILocalizationSet> sets, ImmutableArray<string> appLanguages)
    {
        ArgumentNullException.ThrowIfNull(sets);

        var registrations = ImmutableArray.CreateBuilder<Registration>();

        foreach (var set in sets)
        {
            ArgumentNullException.ThrowIfNull(set, nameof(sets));
            registrations.Add(Registration.Of(set));
        }

        return Create(registrations.ToImmutable(), appLanguages);
    }

    /// <summary>
    /// The language a registered set is filed under. A set that exists already is filed under the code it gives now.
    /// A set yet to be created is filed under the language the build described for its type, or else the language it
    /// registered with.
    /// </summary>
    // Read again for a set that exists, such as one registered as an instance: the code of a set described after it
    // registered (DescribeSet) is the described one from then on. A code that is no culture name any more, or that throws,
    // keeps the set under the language it registered with. It would otherwise fail every text of the app, far from what
    // changed its code.
    //
    // A set yet to be created took the language of its name when it registered before its description, such as gb for
    // ShopLocalizationEnGb. Its description is read again here, without creating it, so that the registry DescribeSet
    // builds anew files it under en-GB.
    private static string GetFilingLanguage(Registration registration)
    {
        if (!registration.IsCreated)
            return GetDescribedLanguageCode(registration.Type) ?? registration.Language;

        try
        {
            return registration.Set.LocalizationLanguageCode is { } current && CultureName.IsValid(current)
                ? current
                : registration.Language;
        }
        catch (Exception)
        {
            return registration.Language;
        }
    }

    /// <summary>
    /// A registry of sets in the order they apply, each filed under its language (<see cref="GetFilingLanguage"/>),
    /// without creating it. Within a language they keep that order, so a later set wins. The registry of the whole app
    /// (<see cref="Current"/>) is built here again after every registration.
    /// </summary>
    private static LocalizationRegistry Create(ImmutableArray<Registration> registrations, ImmutableArray<string> appLanguages)
    {
        var byLanguage = new Dictionary<string, List<Registration>>(StringComparer.OrdinalIgnoreCase);

        foreach (var registration in registrations)
        {
            var language = CultureName.Normalize(GetFilingLanguage(registration));

            if (!byLanguage.TryGetValue(language, out var list))
                byLanguage[language] = list = [];

            list.Add(registration);
        }

        var setsByLanguage = new Dictionary<string, SetsOfLanguage>(byLanguage.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in byLanguage)
            setsByLanguage[pair.Key] = new SetsOfLanguage([.. pair.Value]);

        var registry = new LocalizationRegistry(registrations, appLanguages, setsByLanguage);
        return registry;
    }

    /// <summary>
    /// The set of this registry implementing <typeparamref name="TContract"/> for a culture, or else for its parent
    /// culture. <see cref="GetLocalizationSet{TContract}(CultureInfo)"/> reads <see cref="Current"/> through it.
    /// </summary>
    /// <exception cref="ArgumentNullException">When <paramref name="culture"/> is <see langword="null"/>.</exception>
    internal TContract? GetByCulture<TContract>(CultureInfo culture)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(culture);

        var contract = (TContract?)this.GetByCulture(typeof(TContract), culture);
        return contract;
    }

    /// <summary>
    /// The set of this registry implementing a contract for a culture, or else for its parent culture. Among several
    /// sets for one language, the last in <see cref="Sets"/> wins. <see cref="GetLocalizationSet(Type, CultureInfo)"/>
    /// reads <see cref="Current"/> through it.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="contract"/> or <paramref name="culture"/> is <see langword="null"/>.
    /// </exception>
    // Through the lookup a text of the culture reads too (Resolve), so the culture's names and their sets are worked
    // out once per culture name, and not on every call.
    internal ILocalizationSet? GetByCulture(Type contract, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(culture);

        foreach (var sets in this.Resolve(culture).Levels)
            if (sets.Of(contract) is [.., var last])
                return last.Set;

        return null;
    }

    /// <summary>
    /// A set's language code, read once to file the set under it, or to fall back to the language of the set a text
    /// was declared with. Throws when it isn't a culture name.
    /// </summary>
    private static string ReadLanguageCode(ILocalizationSet set)
    {
        var languageCode = set.LocalizationLanguageCode;

        // Checked here, as the lookup tables can't hold a null key, and every text reads them: an unchecked null
        // would make each one throw, far from the registration that caused it. A code that is no culture name (en_GB,
        // or en with a space after it) would file the set under a language no culture reaches, without a word.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (languageCode is null || !CultureName.IsValid(languageCode))
        {
            var given = languageCode is null ? "null" : $"'{languageCode}'";

            throw new InvalidOperationException($"'{set.GetType()}' gives {given} as its {nameof(ILocalizationSet)}.{nameof(ILocalizationSet.LocalizationLanguageCode)}. Return a culture name, such as en or en-GB.");
        }

        return languageCode;
    }
}
