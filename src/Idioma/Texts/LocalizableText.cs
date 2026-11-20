namespace CodeChops.Idioma;

/// <summary>
/// A text for users that follows their language, such as a button label or a validation message. It shows the wording
/// for <see cref="CultureInfo.CurrentUICulture"/> wherever you use it: in markup, in string interpolation, or as a
/// <see cref="string"/>.
/// </summary>
/// <remarks>
/// <para>
/// Start by declaring your languages, the default one first:
/// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages("en", "nl")]</see>. A text that lacks one of
/// them doesn't build. Write each text in one of two ways:
/// </para>
/// <list type="bullet">
/// <item>Inline: <c>LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")</c>.</item>
/// <item>In one class per language, for a library or a larger group of texts (see <see cref="ILocalizationSet"/>).</item>
/// </list>
/// <para>
/// For wording that differs in a region, list the region next to its language:
/// <c>[assembly: SupportedLanguages("en", "nl", "en-GB")]</c>. Every text then needs British wording too. An inline
/// text takes <c>enGb: "Colour"</c>, and a class per language gets a class for <c>en-GB</c>. British users see that
/// wording, and other English users keep the <c>en</c> wording.
/// </para>
/// <para>
/// A text shows the first wording it finds:
/// </para>
/// <list type="number">
/// <item>The wording for the user's UI culture, or else for its parent culture. An <c>en</c> wording serves
/// <c>en-US</c> and <c>en-GB</c> alike.</item>
/// <item>The wording for your app's default language. A culture your app doesn't declare starts here, even for a
/// library's text that has that culture's language. So no screen ends up in two languages.</item>
/// <item>The wording for the text's own default language, that of the project that wrote it. This is its
/// <see cref="DefaultText"/>, unless a registered <see cref="ILocalizationSet">set</see> for that language rewords
/// it.</item>
/// </list>
/// <para>
/// To show the wording for another culture, such as the language of an email's recipient, call
/// <see cref="ToString(CultureInfo)"/>. An app that keeps the user's culture somewhere other than
/// <see cref="CultureInfo.CurrentUICulture"/> sets <see cref="LocalizationRegistry.CurrentUICultureProvider"/>, and
/// every text follows that instead.
/// </para>
/// <para>
/// To put a value in the wording, write it into every language as you create the text:
/// <c>LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")</c>. In a class per language, such a text
/// is a method that takes the value, such as <c>ShopLocalization.Greeting(customer.Name)</c>. Every language
/// implements it with the same parameters (see <see cref="ILocalizationSet"/>).
/// </para>
/// <para>
/// A member that takes a text also takes a <see cref="string"/>, for a value that has no translations, such as a
/// customer's name: <c>Title = customer.Name</c>. Wording you write as a string, such as <c>Title = "Orders"</c>, is a
/// build error (<c>LOC500</c>), so a text can't end up in one language by accident.
/// </para>
/// <para>
/// A text equals anything that shows the same wording for the current UI culture: a string or another text. For a
/// Dutch user, <c>text == "Afrekenen"</c> holds, and so does
/// <c>text == LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")</c>. See <see cref="Equals(LocalizableText)"/>.
/// </para>
/// <para>
/// A text of a set also has a <see cref="FullKey"/>, its full name, for an app that looks texts up in resource files
/// itself. To give a text of your own a key, create it with <see cref="Keyed"/>.
/// </para>
/// <para>
/// A text survives JSON, such as a Blazor component parameter that crosses a render mode. It carries every wording it
/// can show, so it still follows the user's language after it's read back.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [assembly: SupportedLanguages("en", "nl")]
///
/// public static class ShopTexts
/// {
///     public static LocalizableText Checkout { get; } = LocalizableText.Inline(en: "Checkout", nl: "Afrekenen");
///     public static LocalizableText Greeting(string name) => LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!");
/// }
///
/// // For a user whose UI culture is nl-NL: "Afrekenen" and "Hallo, Anna!".
/// string checkout = ShopTexts.Checkout;
/// string greeting = ShopTexts.Greeting(customer.Name);
/// </code>
/// </example>
// The converter also keeps a text from JSON's default shape, which drops the translations on reading (their setter is
// private) and can't write a text of a set at all (its contract is a Type).
[JsonConverter(typeof(Internals.LocalizableTextJsonConverter))]
public sealed partial class LocalizableText : IEquatable<LocalizableText>, IFormattable
{
    /// <summary>
    /// The wording for the current UI culture. Markup (<c>@ShopTexts.Checkout</c>) and string interpolation show it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The current UI culture is <see cref="CultureInfo.CurrentUICulture"/>, unless your app sets
    /// <see cref="LocalizationRegistry.CurrentUICultureProvider"/>.
    /// </para>
    /// <para>
    /// A text of a set that takes values, such as <c>ShopLocalization.Total(amount)</c>, formats them for
    /// <see cref="CultureInfo.CurrentCulture"/>. Where the provider gives a culture, the text formats them for that
    /// culture instead, because the thread's cultures then aren't the user's.
    /// </para>
    /// </remarks>
    public override string ToString()
    {
        if (this.HasOnlyDefaultText)
            return this.DefaultText;

        // A text among the values of a text being worded on this thread shows as that text does: for its culture, from
        // its sets, and with the format it chose for values.
        var wording = WordingScope.Current;

        // Shown by a property of a set that is worded for a culture asked for: this text shows for that culture too.
        // The thread's cultures aren't that culture there. A text that takes values sets them, because its set's method
        // runs in the culture it words.
        if (wording.Followed is { } followed && wording.FollowedCulture is { } followedCulture)
        {
            var followedRegistry = followed.Registry;

            return this.TakesValues
                ? this.ShowWithValues(followedCulture, followedRegistry, followedRegistry.AppLanguages, wording.FindValuesCulture(followedCulture))
                : this.FindText(followedCulture, followedRegistry, followedRegistry.AppLanguages);
        }

        if (wording.Culture is { } wordingCulture)
            return this.FindText(wordingCulture, wording.Registry, wording.AppLanguages);

        var registry = LocalizationRegistry.Current;

        // A provider gives the user's culture where the thread's cultures aren't the user's, as on a server. So it also
        // gives the format of the values. Without one, the thread's own cultures are the user's.
        var providedCulture = LocalizationRegistry.CurrentUICultureProvider?.Invoke();
        var culture = providedCulture ?? CultureInfo.CurrentUICulture;

        if (!this.TakesValues)
            return this.FindText(culture, registry, registry.AppLanguages);

        var text = this.ShowWithValues(culture, registry, registry.AppLanguages, valuesCulture: providedCulture);
        return text;
    }

    /// <summary>
    /// The wording for a given culture, instead of the one a text shows by default. By default that's the current UI
    /// culture, which <see cref="LocalizationRegistry.CurrentUICultureProvider"/> can configure. Use this for a culture
    /// such as the language of an email's recipient:
    /// <c>ShopTexts.Checkout.ToString(CultureInfo.GetCultureInfo("nl"))</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A culture without a wording falls back as the current UI culture does (see <see cref="LocalizableText"/>).
    /// </para>
    /// <para>
    /// A text of a set that takes values, such as <c>ShopLocalization.Total(amount)</c>, is worded as for a user of the
    /// culture. Its values are formatted for that culture too, so a date or a number reads as the recipient writes it.
    /// A text among its values shows its wording for that culture. For the current UI culture, this gives what
    /// <see cref="ToString()"/> gives, with the values formatted as it formats them.
    /// </para>
    /// </remarks>
    /// <param name="culture">The culture to get the wording for.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="culture"/> is <see langword="null"/>.</exception>
    public string ToString(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var text = this.ToString(culture, LocalizationRegistry.Current);
        return text;
    }

    /// <summary>
    /// The wording for a culture, looked up in the sets of the given registry instead of those of the whole app. Use
    /// it for a part of an app that keeps its own <see cref="ILocalizationSet">sets</see>.
    /// </summary>
    /// <remarks>
    /// Create such a registry with
    /// <see cref="LocalizationRegistry.Create(IEnumerable{ILocalizationSet})">LocalizationRegistry.Create</see>. The
    /// languages you pass to it, not those of your app, decide which languages show and which one a culture without
    /// wording gets. Without languages, the registry shows every language it has a set for. A text among the values of a
    /// text that takes them is looked up in the same sets.
    /// </remarks>
    /// <param name="culture">The culture to get the wording for.</param>
    /// <param name="registry">
    /// The sets to look in, or <see langword="null"/> to use only the text's own translations, with your app's
    /// languages.
    /// </param>
    /// <exception cref="ArgumentNullException">When <paramref name="culture"/> is <see langword="null"/>.</exception>
    public string ToString(CultureInfo culture, LocalizationRegistry? registry)
    {
        ArgumentNullException.ThrowIfNull(culture);

        // Without a registry there are only the text's own translations, which are the app's: its languages decide.
        var text = this.ToString(culture, registry, registry?.AppLanguages ?? LocalizationRegistry.SupportedLanguages);
        return text;
    }

    /// <summary>
    /// The wording for the culture of a format provider, as <see cref="string.Format(IFormatProvider, string, object)"/>
    /// and <see cref="string.Create(IFormatProvider, ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler)"/>
    /// ask for it. So <c>string.Create(dutch, $"{ShopTexts.Checkout}: {amount}")</c> is Dutch throughout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A culture gives its wording, as <see cref="ToString(CultureInfo)"/> does. The invariant culture has none, so it
    /// gives the wording of your app's default language.
    /// </para>
    /// <para>
    /// These give the wording for the current UI culture, as <see cref="ToString()"/> does:
    /// </para>
    /// <list type="bullet">
    /// <item>No provider, as in a plain interpolated string.</item>
    /// <item><see cref="CultureInfo.CurrentCulture"/>, which formats for the current user. The user's wording goes by
    /// the UI culture, also where the two differ.</item>
    /// <item>A provider that is no culture, such as a <see cref="NumberFormatInfo"/>.</item>
    /// </list>
    /// </remarks>
    /// <param name="format">Not used. A text has no formats.</param>
    /// <param name="formatProvider">The culture to get the wording for.</param>
    // Explicit: called through the interface alone, and as a public overload it would stand next to
    // ToString(CultureInfo, LocalizationRegistry) for a call with two nulls.
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider)
    {
        var text = formatProvider is CultureInfo culture && !ReferenceEquals(culture, CultureInfo.CurrentCulture)
            ? this.ToString(culture)
            : this.ToString();

        return text;
    }

    /// <summary>
    /// The wording for a culture, for the given app languages (the default first) instead of those of the registry or
    /// the app.
    /// </summary>
    internal string ToString(CultureInfo culture, LocalizationRegistry? registry, ImmutableArray<string> appLanguages)
    {
        if (this.HasOnlyDefaultText)
            return this.DefaultText;

        if (!this.TakesValues)
        {
            // A property of a set may show another text, which then shows for this culture too, from the same sets.
            // Without a registry, no set words the text but the one of its default language.
            if (registry is null || !this.IsOfSet)
                return this.FindText(culture, registry, appLanguages);

            var lookup = registry.Resolve(culture);
            using var followed = WordingScope.Follow(lookup, culture);

            return this.FindText(culture, registry, appLanguages, lookup);
        }

        var text = this.ShowWithValues(culture, registry, appLanguages, FindValuesCulture(culture));
        return text;
    }

    /// <summary>
    /// The wording shown when the text has none for the user's language. That's the wording of the text's own default
    /// language, or the only wording of a text without translations.
    /// </summary>
    /// <remarks>
    /// A text of a set asks the set of its default language for it each time it's read. A text that takes values, such
    /// as <c>ShopLocalization.Greeting(customer.Name)</c>, is worded with those values. A text among them shows in the
    /// default language too.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// When the set of the default language gives <see langword="null"/> as the wording. Return a string from it, such
    /// as an empty one where a value is missing. Also when the text takes values and that set has no language code
    /// (<c>LOC400</c>).
    /// </exception>
    public string DefaultText => this._defaultText ?? this.WordDefaultText();
    private readonly string? _defaultText; // Null only for a text of a set, which words its default wording itself (WordDefaultText).

    // A text made from a plain string, or declared by key alone: without translations of its own and without a
    // contract no step of the order above can find it other wording, in any culture, so it need not walk them.
    private bool HasOnlyDefaultText => this.TranslationPairs.Length is 0 && this.Contract is null;

    /// <summary>
    /// A text with one wording for every culture, for a value that has no translations, such as a customer's name. You
    /// rarely need this, because a <see cref="string"/> converts to a text by itself.
    /// </summary>
    /// <remarks>
    /// In an assembly that declares two or more languages with
    /// <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, passing wording you write in the source is a
    /// build error (<c>LOC500</c>). Write <c>LocalizableText.Inline</c> instead.
    /// </remarks>
    /// <param name="defaultText">The wording every culture gets. It may be empty, as a value read at run time can be.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="defaultText"/> is <see langword="null"/>.</exception>
    public LocalizableText(string defaultText)
    {
        // Null alone: an empty or blank value read at run time, such as a customer's name, is a text too, and so is
        // empty wording passed to LocalizableText.Inline.
        ArgumentNullException.ThrowIfNull(defaultText);

        this._defaultText = defaultText;
    }

    /// <summary>
    /// A text of a set, which words its default wording when that's asked for (<see cref="WordDefaultText"/>), not as
    /// it's created.
    /// </summary>
    private LocalizableText(TextOrigin origin)
    {
        this.Origin = origin;
    }

    /// <summary>
    /// A copy of a text, for a copy with other translations.
    /// </summary>
    private LocalizableText(LocalizableText original)
    {
        ArgumentNullException.ThrowIfNull(original);

        this._defaultText = original._defaultText;
        this._translationPairs = original._translationPairs;
        this._translationsByLanguage = original._translationsByLanguage;
        this.Origin = original.Origin;
    }

    /// <summary>
    /// The wording for the current UI culture, so a text goes wherever a <see cref="string"/> is expected:
    /// <c>string label = ShopTexts.Checkout;</c>. A <see langword="null"/> text gives <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The string holds the wording of the moment it's made, so it doesn't follow a later change of language. Keep the
    /// <see cref="LocalizableText"/> in anything that outlives one request, such as a static field, a cache or a view
    /// model, and convert it where you show it. On a server, a string kept in a static field shows every user the
    /// language of the first one.
    /// </remarks>
    [return: NotNullIfNotNull(nameof(text))]
    public static implicit operator string?(LocalizableText? text) => text?.ToString();

    #region Equality checks
    /// <summary>
    /// Whether two texts show the same wording for the current UI culture. See <see cref="Equals(LocalizableText)"/>.
    /// </summary>
    public static bool operator ==(LocalizableText? left, LocalizableText? right) => left is null ? right is null : left.Equals(right);

    /// <summary>
    /// Whether two texts show different wording for the current UI culture. See <see cref="Equals(LocalizableText)"/>.
    /// </summary>
    public static bool operator !=(LocalizableText? left, LocalizableText? right) => !(left == right);

    /// <summary>
    /// Whether this text shows the given wording for the current UI culture. For a Dutch user,
    /// <c>text == "Afrekenen"</c> holds.
    /// </summary>
    /// <remarks>
    /// The comparison is by character, so letter case counts. <c>text.Equals("Afrekenen")</c> compares the same way. A
    /// <see langword="null"/> text equals no wording, not even the empty string. To test for <see langword="null"/>,
    /// write <c>text is null</c>. A <see langword="null"/> string compares as the empty string.
    /// </remarks>
    // The last sentence is the price of the span, which can't tell a null string from an empty one. An overload taking
    // a string would tell them apart, and make text == null ambiguous.
    public static bool operator ==(LocalizableText? left, ReadOnlySpan<char> right) => left is not null && left.Equals(right);

    /// <summary>
    /// Whether this text shows another wording than the given one for the current UI culture. A
    /// <see langword="null"/> text shows no wording, so this holds for it whatever the wording given.
    /// </summary>
    public static bool operator !=(LocalizableText? left, ReadOnlySpan<char> right) => !(left == right);

    /// <summary>
    /// Whether this text shows the given wording for the current UI culture. <c>"Afrekenen" == text</c> compares as
    /// <c>text == "Afrekenen"</c> does, so it holds for a Dutch user.
    /// </summary>
    public static bool operator ==(ReadOnlySpan<char> left, LocalizableText? right) => right == left;

    /// <summary>
    /// Whether this text shows another wording than the given one for the current UI culture. A
    /// <see langword="null"/> text shows no wording, so this holds for it whatever the wording given.
    /// </summary>
    public static bool operator !=(ReadOnlySpan<char> left, LocalizableText? right) => !(right == left);

    /// <summary>
    /// Whether this text shows the given wording for the current UI culture. <c>text.Equals("Afrekenen")</c> compares
    /// as <c>text == "Afrekenen"</c> does, so it holds for a Dutch user.
    /// </summary>
    /// <param name="wording">The wording to compare with. A <see langword="null"/> string compares as the empty string.</param>
    // A span, like the operators: an overload taking a string would make text.Equals(null) ambiguous.
    public bool Equals(ReadOnlySpan<char> wording) => this.ToString().AsSpan().SequenceEqual(wording);

    /// <summary>
    /// Whether this text shows the same wording as another text for the current UI culture. Comparing two texts with
    /// <c>==</c> means the same.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the wording counts, so the key, the contract, and the values don't. For a Dutch user,
    /// <c>ShopLocalization.Checkout</c> equals <c>LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")</c> and a
    /// text made from the string <c>"Afrekenen"</c>. For an English user, it equals only the first, because the second
    /// shows <c>"Afrekenen"</c> to every user. So <c>texts.Contains(wording)</c> holds when one of the texts shows that
    /// wording.
    /// </para>
    /// <para>
    /// The answer follows the user's language, and so does <see cref="GetHashCode"/>. A <see cref="HashSet{T}"/> of
    /// texts, or a <see cref="Dictionary{TKey,TValue}"/> keyed by them, works while the language stays the same. After
    /// a switch, it can't find the texts it holds. Give a collection that outlives a switch, or that users of
    /// different languages share, <see cref="ReferenceEqualityComparer.Instance"/>, so it tells texts apart by object.
    /// </para>
    /// </remarks>
    public bool Equals(LocalizableText? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        var isEqual = String.Equals(this.ToString(), other.ToString(), StringComparison.Ordinal);
        return isEqual;
    }

    /// <summary>
    /// Whether this text shows the same wording for the current UI culture as another text or a string. See
    /// <see cref="Equals(LocalizableText)"/>.
    /// </summary>
    /// <remarks>
    /// So <c>object.Equals(text, "Afrekenen")</c> holds for a Dutch user. The other way round doesn't:
    /// <c>object.Equals("Afrekenen", text)</c> is false, because a string only equals another string.
    /// </remarks>
    public override bool Equals(object? obj)
    {
        var isEqual = obj switch
        {
            LocalizableText text => this.Equals(text),
            string wording => this.Equals(wording.AsSpan()),
            _ => false,
        };

        return isEqual;
    }

    /// <summary>
    /// The hash of the wording for the current UI culture, the same as that wording's own hash as a string. It changes
    /// when the user's language does (see <see cref="Equals(LocalizableText)"/>).
    /// </summary>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.ToString());
    #endregion

    /// <summary>
    /// The wording this text shows per language, for a copy that needs no sets. That's its own translations and, for a
    /// text of a contract, the wording of every language that the registry has a set for and its
    /// <see cref="LocalizationRegistry.AppLanguages"/> can show, as a user of that language sees it.
    /// </summary>
    internal ImmutableDictionary<string, string> GetWordingPerLanguage(LocalizationRegistry registry)
    {
        // Only a text of a contract has wording in the sets; for any other text, the languages of the sets would only
        // repeat its own translations under their regions.
        if (this.Contract is null)
            return this.TranslationsByLanguage;

        var appLanguages = registry.AppLanguages;
        var builder = NoTranslations.ToBuilder();

        foreach (var (language, wording) in this.TranslationsByLanguage)
            builder[language] = wording;

        // Each app language itself, as the default one is what a culture without wording gets, also where only a set
        // of a language above it words the text (en for an app declaring en-GB).
        foreach (var language in appLanguages)
            Add(language);

        // A language no app language can show is left out: a culture none of them serves gets the default language,
        // so a library's thirty languages would travel with every text of an app that declares two. Kept on the
        // language alone (zh-TW for an app declaring zh-Hant), which is more than the app shows and never less: which
        // region falls back to which script only the cultures know, and under invariant globalization there are none.
        foreach (var language in registry.Languages)
            if (appLanguages.IsDefaultOrEmpty || appLanguages.Any(appLanguage => HasSameLanguage(language, appLanguage)))
                Add(language);

        return builder.ToImmutable();

        void Add(string language)
        {
            if (builder.ContainsKey(language))
                return;

            using var reader = this.EnterLanguage(language, registry);

            if (this.GetWording(language, registry) is { } wording)
                builder[language] = wording;
        }

        // Whether two culture names start with the same language: en-GB and en, or zh-TW and zh-Hant.
        static bool HasSameLanguage(string name, string other)
        {
            var language = name.AsSpan(start: 0, name.IndexOf('-', StringComparison.Ordinal) is var end and >= 0
                ? end
                : name.Length);
            var otherLanguage = other.AsSpan(start: 0, other.IndexOf('-', StringComparison.Ordinal) is var otherEnd and >= 0
                ? otherEnd
                : other.Length);

            var result = language.Equals(otherLanguage, StringComparison.OrdinalIgnoreCase);
            return result;
        }
    }

    /// <summary>
    /// The wording this text shows for a culture none of its languages serves, for a copy that needs no sets. That's
    /// the wording of the set registered for its own default language, else its <see cref="DefaultText"/>.
    /// </summary>
    internal string GetDefaultWording(LocalizationRegistry registry)
    {
        using var reader = this.EnterLanguage(this.DefaultLanguage, registry);

        var defaultWording = this.FindDefaultTranslation(registry) ?? this.DefaultText;
        return defaultWording;
    }

    /// <summary>
    /// Whether a language serves a culture, as one of the app's languages does (see
    /// <see cref="IsServed(CultureInfo, ImmutableArray{string})"/>).
    /// </summary>
    private static bool IsServed(CultureInfo culture, string language)
    {
        foreach (var name in new CultureFallback(culture))
            if (IsLanguageOrRegion(name, language))
                return true;

        return false;
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
    /// Whether one of the app's languages serves a culture. It does when the culture, or one it falls back to, is that
    /// language or a region of it.
    /// </summary>
    internal static bool IsServed(CultureInfo culture, ImmutableArray<string> appLanguages)
    {
        // The names the lookups walk, so that this gate and the lookups agree on which language serves a culture, also
        // under invariant globalization, where en-GB (if it can be created: PredefinedCulturesOnly off) has no parent
        // culture and en still serves it.
        foreach (var name in new CultureFallback(culture))
            foreach (var language in appLanguages)
                if (IsLanguageOrRegion(name, language))
                    return true;

        return false;
    }

    private string FindText(CultureInfo culture, LocalizationRegistry? registry, ImmutableArray<string> appLanguages,
        LocalizationRegistry.CultureLookup? lookup = null)
    {
        // The one place that decides the order: the text's own translation for the culture, else the wording of the
        // sets registered for it (where a regional set leaves a text as its language words it, a rewording of that
        // language wins: LocalizationRegistry.FindWording); then the same for the app's default language, so that a
        // library's text falls back to the language the app chose rather than the library's own; then the set
        // registered for the text's own default language, which may reword the default text; else the default text. A
        // host with a source of its own (resource files, say) asks that source first and calls this for the rest.
        // A culture none of the app's languages serves skips the first two: the app's own texts show their default
        // language there, so a library's text must too, even where the library has the culture's language.
        // The registry keeps how the culture resolves, so a render works that out once and not per text. Whether the
        // app's languages serve it is kept for the registry's own app languages, which nearly every call passes.
        lookup ??= registry?.Resolve(culture);
        var appDefaultLanguage = appLanguages.IsDefaultOrEmpty ? null : appLanguages[0];
        var showsCulture = appDefaultLanguage is null || (lookup is not null && appLanguages == registry!.AppLanguages
            ? lookup.ShowsCulture
            : IsServed(culture, appLanguages));

        var text = (showsCulture ? this.FindOwnTranslation(culture, lookup) : null)
               ?? (showsCulture && lookup is not null ? this.FindTranslation(registry!, lookup) : null)
               ?? (appDefaultLanguage is null ? null : this.GetWording(appDefaultLanguage, registry))
               ?? (registry is null ? null : this.FindDefaultTranslation(registry))
               ?? this.DefaultText;

        return text;
    }

    /// <summary>
    /// The text's own wording for a culture or a culture above it, along the names a registry kept for it where there
    /// is one (see <see cref="GetWording(CultureInfo)"/>).
    /// </summary>
    private string? FindOwnTranslation(CultureInfo culture, LocalizationRegistry.FallbackLookup? lookup)
    {
        if (lookup is null)
            return this.GetWording(culture);

        // A text of a set has none, so it skips the names.
        if (this._translationPairs.Length is 0)
            return null;

        foreach (var name in lookup.Names)
            if (this.FindOwnTranslation(name) is { } translation)
                return translation;

        return null;
    }

    /// <summary>
    /// The wording for a language code. That's the text's own translation for it or a language above it, else the
    /// wording of the set registered for it or a language above it (see <see cref="FindTranslation"/>).
    /// </summary>
    private string? GetWording(string language, LocalizationRegistry? registry)
    {
        // Through the culture of the language where there is one, so that a text falling back to a language shows what
        // a user of it sees: zh-TW reaches a set for zh-Hant, its parent culture, which the name alone (zh-TW, then zh)
        // walks past. By name where there is none: under invariant globalization (a NativeAOT console, a trimmed
        // WebAssembly app) only the invariant culture can be created by default. Dropping the last subtag (en-GB, then
        // en) is also how the build checks that a set serves each of the app's languages. A registry keeps those names
        // per language, with its sets for each.
        var lookup = registry?.ResolveLanguage(language);

        if (this._translationPairs.Length is not 0)
        {
            if (lookup is not null)
            {
                foreach (var name in lookup.Names)
                    if (this.FindOwnTranslation(name) is { } translation)
                        return translation;
            }
            else
            {
                foreach (var name in CultureFallback.OfLanguage(language))
                    if (this.FindOwnTranslation(name) is { } translation)
                        return translation;
            }
        }

        // A text among the values shows in this language too, where the user reads another, as for a fallback. A text
        // without values enters no scope here, on the path of every text a user of an unsupported culture sees. A text
        // its property shows falls back as it does, so both end in this language anyway.
        using var scope = this.TakesValues ? this.EnterLanguageUnlessServed(language, registry) : default;

        var text = lookup is null
            ? null
            : this.FindTranslation(registry!, lookup);

        return text;
    }

    /// <summary>
    /// The default wording of a text of a set, which the set of its default language gives each time it's read.
    /// </summary>
    // Not kept once worded: a value is formatted for the culture of the moment, and a text the wording shows follows
    // the culture of its reader. Kept, the first reader's culture would stay in it for every reader after.
    private string WordDefaultText()
    {
        var origin = this.Origin!;
        var set = origin.DefaultSet!;
        var language = origin.DefaultLanguage;

        // From the sets of the whole app, where no text being worded gives others.
        using var scope = this.EnterLanguageUnlessServed(language, LocalizationRegistry.Current);

        var wording = this.Select(set);

        // A member of a set may hand on a value that's null at run time, such as a nickname from a database, though
        // it returns a string. Nothing is left to fall back to here, so the text says so rather than show null.
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (wording is null)
            throw new InvalidOperationException(
                $"{set.GetType()} gives null for the text '{this.FullKey}', so the text has no wording in '{language}', its default language. "
                + "Return a string, such as an empty one where a value is missing.");

        return wording;
    }
}
