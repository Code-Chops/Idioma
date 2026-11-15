namespace CodeChops.Idioma;

/// <summary>
/// A text for users that follows their language, such as a button label or a validation message. Wherever you use
/// it, in markup, in string interpolation, or as a <see cref="string"/>, it shows the wording for
/// <see cref="CultureInfo.CurrentUICulture"/>.
/// </summary>
/// <remarks>
/// <para>
/// Start by declaring your languages, the default one first:
/// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages("en", "nl")]</see>. Then write each text in
/// one of two ways. Either way, a text that lacks a language does not build:
/// </para>
/// <list type="bullet">
/// <item>Inline: <c>LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")</c>.</item>
/// <item>In one class per language, for a library or a larger group of texts: see <see cref="ITranslationSet"/>.</item>
/// </list>
/// <para>
/// For wording that differs in a region, list the region next to its language:
/// <c>[assembly: SupportedLanguages("en", "nl", "en-GB")]</c>. Every text then needs British wording too: an inline
/// text takes <c>enGB: "Colour"</c>, and a class per language gets a class for <c>en-GB</c>. British users see that
/// wording, and other English users keep the <c>en</c> wording.
/// </para>
/// <para>
/// A text shows the first wording it finds:
/// </para>
/// <list type="number">
/// <item>The wording for the user's UI culture, or else for its parent culture: an <c>en</c> wording serves
/// <c>en-US</c> and <c>en-GB</c> alike.</item>
/// <item>The wording for your app's default language. A culture your app does not declare starts here, even for a
/// library's text that has that culture's language, so no screen ends up in two languages.</item>
/// <item>The wording for the text's own default language: that of the project that wrote it. This is its
/// <see cref="DefaultText"/>, unless a registered <see cref="ITranslationSet">set</see> for that language rewords
/// it.</item>
/// </list>
/// <para>
/// To show the wording for another culture, such as the language of an email's recipient, call
/// <see cref="ToString(CultureInfo)"/>.
/// </para>
/// <para>
/// For a value inside the wording, write it into every language as you create the text:
/// <c>LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")</c>. In a class per language such a text
/// is a method that takes the value, <c>ShopTranslations.Greeting(customer.Name)</c>, which every language implements
/// with the same parameters: see <see cref="ITranslationSet"/>.
/// </para>
/// <para>
/// A member that takes a text also takes a <see cref="string"/>, for a value that has no translations, such as a
/// customer's name: <c>Title = customer.Name</c>. Wording you write as a string, such as <c>Title = "Orders"</c>, is a
/// build error (<c>LOC011</c>), so a text cannot end up in one language by accident.
/// </para>
/// <para>
/// To check what a text shows, compare it with a string: <c>text == "Afrekenen"</c>.
/// <see cref="Equals(LocalizableText)"/> compares the texts themselves instead, whatever wording they show.
/// </para>
/// <para>
/// A text survives JSON, such as a Blazor component parameter that crosses a render mode: it carries every wording it
/// can show, so it still follows the user's language after it is read back.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [assembly: SupportedLanguages("en", "nl")]
///
/// public static class ShopTexts
/// {
///     public static LocalizableText Checkout { get; } = LocalizableText.Inline(en: "Checkout", nl: "Afrekenen");
///     public static LocalizableText Greeting(string name) =>
///         LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!");
/// }
///
/// // For a user whose UI culture is nl-NL: "Afrekenen" and "Hallo, Anna!".
/// string checkout = ShopTexts.Checkout;
/// string greeting = ShopTexts.Greeting(customer.Name);
/// </code>
/// </example>
// The converter also keeps a text from JSON's default shape, which drops the translations on reading (their setter is
// private) and cannot write a ResourceText at all (its contract is a Type).
[JsonConverter(typeof(Internals.LocalizableTextJsonConverter))]
public partial record LocalizableText
{
    /// <summary>
    /// The wording for the current UI culture: what markup (<c>@ShopTexts.Checkout</c>) and string interpolation show.
    /// </summary>
    public sealed override string ToString()
    {
        if (this.HasOnlyDefaultText)
            return this.DefaultText;

        var registry = TranslationsRegistry.Current;

        var text = this.FindText(CultureInfo.CurrentUICulture, registry, registry.AppLanguages);
        return text;
    }

    /// <summary>
    /// The wording shown when the text has none for the user's language: that of the default language, or the only
    /// wording of a text without translations.
    /// </summary>
    public string DefaultText { get; }

    // A text made from a plain string, or declared by key alone: without translations of its own and without a
    // contract no step of the order above can find it other wording, in any culture, so it need not walk them.
    private bool HasOnlyDefaultText => this.TranslationPairs.Length is 0 && this is not ResourceText { Contract: not null };

    /// <summary>
    /// A text with one wording for every culture, for a value that has no translations, such as a customer's name. You
    /// rarely need this, as a <see cref="string"/> converts to a text by itself.
    /// </summary>
    /// <remarks>
    /// In an assembly that declares two or more languages with
    /// <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, passing wording you write in the source is a
    /// build error (<c>LOC011</c>): write <c>LocalizableText.Inline</c> instead.
    /// </remarks>
    /// <param name="defaultText">The wording every culture gets. It may be empty, as a value read at run time can be.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="defaultText"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    /// When a type of your own derives from <see cref="LocalizableText"/>: hold a text in a member of your type
    /// instead.
    /// </exception>
    public LocalizableText(string defaultText)
    {
        // Null alone: an empty or blank value read at run time, such as a customer's name, is a text too, and so is
        // empty wording passed to LocalizableText.Inline.
        ArgumentNullException.ThrowIfNull(defaultText);

        this.ThrowIfDerived();

        this.DefaultText = defaultText;
    }

    /// <summary>
    /// A copy of a text, as a <c>with</c> expression makes it. You never need to call this.
    /// </summary>
    /// <param name="original">The text to copy.</param>
    /// <exception cref="NotSupportedException">
    /// When a type of your own derives from <see cref="LocalizableText"/>: hold a text in a member of your type
    /// instead.
    /// </exception>
    // Written out instead of left to the compiler for the check alone: a derived record can call the copy constructor
    // of a record, which would otherwise be a way around the check of the constructor above.
    protected LocalizableText(LocalizableText original)
    {
        ArgumentNullException.ThrowIfNull(original);

        this.ThrowIfDerived();

        this.DefaultText = original.DefaultText;
        this._translationPairs = original._translationPairs;
        this._translations = original._translations;
    }

    // A record cannot be closed to other assemblies while its constructor stays public, and a derived text would break
    // without a word: JSON reads the converter from the exact type, so it would be written in the default shape,
    // without its translations, and fail to read back. Checked on the type, which costs two comparisons.
    private void ThrowIfDerived()
    {
        if (this.GetType() != typeof(LocalizableText) && this.GetType() != typeof(ResourceText))
            throw new NotSupportedException($"{this.GetType()} cannot derive from {nameof(LocalizableText)}: hold a {nameof(LocalizableText)} in a member instead.");
    }

    /// <summary>
    /// The wording for the current UI culture, so a text goes wherever a <see cref="string"/> is expected:
    /// <c>string label = ShopTexts.Checkout;</c>. A <see langword="null"/> text gives <see langword="null"/>.
    /// </summary>
    [return: NotNullIfNotNull(nameof(text))]
    public static implicit operator string?(LocalizableText? text) => text?.ToString();

    #region Equality checks
    /// <summary>
    /// Whether this text shows the given wording for the current UI culture: <c>text == "Afrekenen"</c> holds for a
    /// Dutch user.
    /// </summary>
    /// <remarks>
    /// The comparison is by character, so letter case counts. To compare two texts instead, whatever wording they
    /// show, use <see cref="Equals(LocalizableText)"/>. A <see langword="null"/> text equals no wording, the empty
    /// string included: to test for <see langword="null"/>, write <c>text is null</c>. A <see langword="null"/>
    /// string compares as the empty string.
    /// </remarks>
    // The last sentence is the price of the span, which cannot tell a null string from an empty one. An overload taking
    // a string would tell them apart, and make text == null ambiguous.
    public static bool operator ==(LocalizableText? left, ReadOnlySpan<char> right) => left is not null && left.ToString().AsSpan().SequenceEqual(right);

    /// <summary>
    /// Whether this text shows another wording than the given one for the current UI culture. A
    /// <see langword="null"/> text shows no wording, so this holds for it whatever the wording given.
    /// </summary>
    public static bool operator !=(LocalizableText? left, ReadOnlySpan<char> right) => !(left == right);

    /// <summary>
    /// Whether this text shows the given wording for the current UI culture: <c>"Afrekenen" == text</c> holds for a
    /// Dutch user. It compares as <c>text == "Afrekenen"</c> does.
    /// </summary>
    public static bool operator ==(ReadOnlySpan<char> left, LocalizableText? right) => right == left;

    /// <summary>
    /// Whether this text shows another wording than the given one for the current UI culture. A
    /// <see langword="null"/> text shows no wording, so this holds for it whatever the wording given.
    /// </summary>
    public static bool operator !=(ReadOnlySpan<char> left, LocalizableText? right) => !(right == left);
    #endregion

    /// <summary>
    /// The wording for a given culture instead of the current UI culture, such as the language of an email's recipient:
    /// <c>ShopTexts.Checkout.ToString(CultureInfo.GetCultureInfo("nl"))</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A culture without a wording falls back as the current UI culture does: see <see cref="LocalizableText"/>.
    /// </para>
    /// <para>
    /// A text of a set that takes values, such as <c>ShopTranslations.Total(amount)</c>, is worded as for a user of the
    /// culture: its values are formatted for that culture too, so that a date or a number reads as the recipient writes
    /// it, and a text among its values shows its wording for that culture. For the current UI culture this is
    /// <see cref="ToString()"/>, which formats values for <see cref="CultureInfo.CurrentCulture"/>.
    /// </para>
    /// </remarks>
    /// <param name="culture">The culture to get the wording for.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="culture"/> is <see langword="null"/>.</exception>
    public string ToString(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var text = this.ToString(culture, TranslationsRegistry.Current);
        return text;
    }

    /// <summary>
    /// The wording for a culture, looked up in the sets of the given registry instead of those of the whole app: for a
    /// part of an app that keeps its own <see cref="ITranslationSet">sets</see>.
    /// </summary>
    /// <remarks>
    /// Create such a registry with
    /// <see cref="TranslationsRegistry.Create(IEnumerable{ITranslationSet})">TranslationsRegistry.Create</see>. Its
    /// <see cref="TranslationsRegistry.AppLanguages"/> decide which languages show and which one a culture without
    /// wording gets, not the languages of your app.
    /// </remarks>
    /// <param name="culture">The culture to get the wording for.</param>
    /// <param name="registry">
    /// The sets to look in, or <see langword="null"/> to use only the text's own translations, with your app's
    /// languages.
    /// </param>
    /// <exception cref="ArgumentNullException">When <paramref name="culture"/> is <see langword="null"/>.</exception>
    public string ToString(CultureInfo culture, TranslationsRegistry? registry)
    {
        ArgumentNullException.ThrowIfNull(culture);

        // Without a registry there are only the text's own translations, which are the app's: its languages decide.
        var text = this.ToString(culture, registry, registry?.AppLanguages ?? TranslationsRegistry.SupportedLanguages);
        return text;
    }

    /// <summary>
    /// The wording for a culture, for the given app languages (the default first) instead of those of the registry or
    /// the app.
    /// </summary>
    internal string ToString(CultureInfo culture, TranslationsRegistry? registry, ImmutableArray<string> appLanguages)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (this.HasOnlyDefaultText)
            return this.DefaultText;

        if (!this.TakesValues || culture.Equals(CultureInfo.CurrentUICulture))
            return this.FindText(culture, registry, appLanguages);

        // A text that takes values is worded by a method of a set, which formats them as it runs: for the current
        // culture, as an interpolated string does. So it runs as for a user of the culture asked for, which then gives
        // the wording, the format of the values, and the wording of a text among them. Not for the current UI
        // culture: there the text shows what ToString() shows, also to a user who reads one language and writes
        // numbers as another does. Put back in any case: the method is the consumer's, and may throw.
        var (currentCulture, currentUICulture) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);

        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            return this.FindText(culture, registry, appLanguages);
        }
        finally
        {
            CultureInfo.CurrentCulture = currentCulture;
            CultureInfo.CurrentUICulture = currentUICulture;
        }
    }

    /// <summary>
    /// Whether a set words this text with values the text keeps: a <see cref="ResourceText"/> created with arguments.
    /// </summary>
    private protected virtual bool TakesValues => false;

    private string FindText(CultureInfo culture, TranslationsRegistry? registry, ImmutableArray<string> appLanguages)
    {
        // The one place that decides the order: the text's own translation for the culture, else the wording of the
        // sets registered for it (where a regional set leaves a text as its language words it, a rewording of that
        // language wins: TranslationsRegistry.FindWording); then the same for the app's default language, so that a
        // library's text falls back to the language the app chose rather than the library's own; then the set
        // registered for the text's own default language, which may reword the default text; else the default text. A
        // host with a source of its own (resource files, say) asks that source first and calls this for the rest.
        // A culture none of the app's languages serves skips the first two: the app's own texts show their default
        // language there, so a library's text must too, even where the library has the culture's language.
        var appDefaultLanguage = appLanguages.IsDefaultOrEmpty ? null : appLanguages[0];
        var showsCulture = appDefaultLanguage is null || IsServed(culture, appLanguages);

        var text = (showsCulture ? this.GetTranslation(culture) : null)
               ?? (showsCulture && registry is not null ? this.FindTranslation(registry, new CultureFallback(culture)) : null)
               ?? (appDefaultLanguage is null ? null : this.GetTranslation(appDefaultLanguage, registry))
               ?? (registry is null ? null : this.FindDefaultTranslation(registry))
               ?? this.DefaultText;

        return text;
    }

    /// <summary>
    /// Whether one of the app's languages serves a culture: the culture, or one it falls back to, is that language or a
    /// region of it.
    /// </summary>
    private static bool IsServed(CultureInfo culture, ImmutableArray<string> appLanguages)
    {
        // The names the lookups walk, so that this gate and the lookups agree on which language serves a culture, also
        // under invariant globalization, where en-GB (if it can be created: PredefinedCulturesOnly off) has no parent
        // culture and en still serves it.
        foreach (var name in new CultureFallback(culture))
            foreach (var language in appLanguages)
                if (name.StartsWith(language, StringComparison.OrdinalIgnoreCase) && (name.Length == language.Length || name[language.Length] is '-'))
                    return true;

        return false;
    }

    /// <summary>
    /// The wording the registered sets give this text for the first of the names that has a set, or
    /// <see langword="null"/> when none does.
    /// </summary>
    // A text with only inline translations belongs to no contract, so no set can phrase it; ResourceText overrides
    // this and the one below.
    private protected virtual string? FindTranslation(TranslationsRegistry registry, CultureFallback names)
    {
        return null;
    }

    /// <summary>
    /// The wording the set registered for the text's own default language gives it, or <see langword="null"/> when
    /// the text has no such language or no set does.
    /// </summary>
    private protected virtual string? FindDefaultTranslation(TranslationsRegistry registry)
    {
        return null;
    }

    /// <summary>
    /// The wording for a language code: the text's own translation for it or a language above it, else that of the set
    /// registered for it or a language above it (see <see cref="FindTranslation"/>).
    /// </summary>
    private string? GetTranslation(string language, TranslationsRegistry? registry)
    {
        // Through the culture of the language where there is one, so that a text falling back to a language shows what
        // a user of it sees: zh-TW reaches a set for zh-Hant, its parent culture, which the name alone (zh-TW, then zh)
        // walks past. By name where there is none: under invariant globalization (a NativeAOT console, a trimmed
        // WebAssembly app) only the invariant culture can be created by default. Dropping the last subtag (en-GB, then
        // en) is also how the build checks that a set serves each of the app's languages.
        var names = CultureFallback.OfLanguage(language);

        foreach (var name in names)
            if (this.FindOwnTranslation(name) is { } translation)
                return translation;

        var text = registry is null
            ? null
            : this.FindTranslation(registry, names);
        return text;
    }

    /// <summary>
    /// The wording this text shows per language, for a copy that needs no sets: its own translations, and for a text of
    /// a contract the wording of every language the registry has a set for that its
    /// <see cref="TranslationsRegistry.AppLanguages"/> can show, as a user of that language sees it.
    /// </summary>
    internal ImmutableDictionary<string, string> GetWordingPerLanguage(TranslationsRegistry registry)
    {
        // Only a text of a contract has wording in the sets; for any other text, the languages of the sets would only
        // repeat its own translations under their regions.
        if (this is not ResourceText { Contract: not null })
            return this.Translations;

        var appLanguages = registry.AppLanguages;
        var builder = NoTranslations.ToBuilder();

        foreach (var (language, wording) in this.Translations)
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

            using var reader = new ReaderScope(this, language);

            if (this.GetTranslation(language, registry) is { } wording)
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
    /// The wording this text shows for a culture none of its languages serves, for a copy that needs no sets: that of
    /// the set registered for its own default language, else its <see cref="DefaultText"/>.
    /// </summary>
    internal string GetDefaultWording(TranslationsRegistry registry)
    {
        using var reader = new ReaderScope(this, (this as ResourceText)?.DefaultLanguage);

        return this.FindDefaultTranslation(registry) ?? this.DefaultText;
    }

    /// <summary>
    /// Makes a language the current UI culture for as long as a text that takes values is worded in that language,
    /// and puts the UI culture back after.
    /// </summary>
    // A text among the values shows its wording for the current UI culture, as the method of the set writes it into
    // the wording. Without this the copy of a text for every language would hold that text in the one language of
    // whoever writes the copy. The other values stay formatted for the culture of the writer: a language is no
    // region, and the region of the reader is what a date or a number is written for. Nothing changes for a text
    // without values, or where this machine has no culture for the language, as under invariant globalization.
    private readonly ref struct ReaderScope
    {
        private CultureInfo? Previous { get; }

        public ReaderScope(LocalizableText text, string? language)
        {
            if (!text.TakesValues || language is null || CultureName.FindCulture(language) is not { } culture || culture.Equals(CultureInfo.CurrentUICulture))
                return;

            this.Previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            if (this.Previous is not null)
                CultureInfo.CurrentUICulture = this.Previous;
        }
    }
}
