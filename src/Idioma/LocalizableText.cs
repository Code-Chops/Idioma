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
/// one of two ways, and the build makes sure that it has every language:
/// </para>
/// <list type="bullet">
/// <item>In one line: <c>LocalizableText.Translated(en: "Checkout", nl: "Afrekenen")</c>. The source generator
/// writes this method with one required argument per language, so leaving a language out does not compile.</item>
/// <item>In one class per language, for a library or a larger group of texts: see <see cref="ITranslations"/>. You
/// then use the texts of the generated class, such as <c>ShopTranslations.Checkout</c>.</item>
/// </list>
/// <para>
/// A user sees the wording for their UI culture, or else for its parent culture: an <c>nl</c> wording serves
/// <c>nl-NL</c> and <c>nl-BE</c> alike. A culture your app does not declare, or one without a wording, gets your app's
/// default language, the texts of the libraries it uses included, or else the text's own default language. To show the
/// wording for another culture, such as the language of an email's recipient, call <see cref="GetText(CultureInfo)"/>.
/// </para>
/// <para>
/// For a value inside the wording, write a placeholder in every language (<c>"Hello, {0}!"</c>) and fill it in with
/// <see cref="Format(ReadOnlySpan{object})"/>. The build makes sure every language has the same placeholders
/// (<c>LOC016</c>).
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
/// can show, so it shows the same for every culture after it is read back, also after a switch of language.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [assembly: SupportedLanguages("en", "nl")]
///
/// public static class ShopTexts
/// {
///     public static LocalizableText Checkout { get; } = LocalizableText.Translated(en: "Checkout", nl: "Afrekenen");
///     public static LocalizableText Greeting { get; } =
///         LocalizableText.Translated(en: "Hello, {0}!", nl: "Hallo, {0}!");
/// }
///
/// // For a user whose UI culture is nl-NL: "Afrekenen" and "Hallo, Anna!".
/// string checkout = ShopTexts.Checkout;
/// string greeting = ShopTexts.Greeting.Format(customer.Name);
/// </code>
/// </example>
// The converter also keeps a text from JSON's default shape, which drops the translations on reading (their setter is
// private) and cannot write a ResourceText at all (its contract is a Type).
[JsonConverter(typeof(LocalizableTextJsonConverter))]
public partial record LocalizableText
{
    /// <summary>
    /// The wording for the current UI culture: what markup (<c>@ShopTexts.Checkout</c>) and string interpolation show.
    /// </summary>
    public sealed override string ToString()
    {
        // Decided before the current UI culture and the registry are read: most texts of an app are plain strings, and
        // on WebAssembly reading those two costs such a text more than everything else it does. The check costs a text
        // of a set a few nanoseconds under the JIT, measured, against most of a microsecond saved per plain text on
        // WebAssembly.
        if (this.HasOnlyDefaultText)
            return this.DefaultText;

        var registry = TranslationsRegistry.Current;

        return this.FindText(CultureInfo.CurrentUICulture, registry, registry.AppLanguages);
    }

    /// <summary>
    /// The wording a culture gets when no language of the text serves it: the default language's wording, or the only
    /// wording of a text without translations.
    /// </summary>
    public string DefaultText { get; }

    /// <summary>
    /// A text with one wording for every culture, for a value that has no translations, such as a customer's name. You
    /// rarely need this, as a <see cref="string"/> converts to a text by itself.
    /// </summary>
    /// <remarks>
    /// In an assembly that declares <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, passing wording
    /// you write in the source is a build error (<c>LOC011</c>): write <c>LocalizableText.Translated</c> instead.
    /// </remarks>
    /// <param name="defaultText">The wording every culture gets.</param>
    /// <exception cref="NotSupportedException">
    /// When a type of your own derives from <see cref="LocalizableText"/>. A text is one of the two types of this
    /// package: keep what you want to add next to the text, in a type of your own that holds it.
    /// </exception>
    public LocalizableText(string defaultText)
    {
        ArgumentNullException.ThrowIfNull(defaultText);

        this.ThrowIfDerived();

        this.DefaultText = defaultText;
    }

    /// <summary>
    /// A copy of a text, as a <c>with</c> expression makes it. You never need to call this.
    /// </summary>
    /// <param name="original">The text to copy.</param>
    /// <exception cref="NotSupportedException">
    /// When a type of your own derives from <see cref="LocalizableText"/>. A text is one of the two types of this
    /// package: keep what you want to add next to the text, in a type of your own that holds it.
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

    /// <summary>
    /// Whether this text shows the given wording for the current UI culture: <c>text == "Afrekenen"</c> holds for a
    /// Dutch user. The comparison is by character, so letter case counts. To compare two texts instead, whatever
    /// wording they show, use <see cref="Equals(LocalizableText)"/>.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> text shows nothing, so it equals no wording, the empty string included. To test for
    /// <see langword="null"/>, write <c>text is null</c> or <c>text == null</c>. A <see langword="null"/> string
    /// compares as the empty string.
    /// </remarks>
    // The last sentence is the price of the span, which cannot tell a null string from an empty one. An overload taking
    // a string would tell them apart, and make text == null ambiguous.
    public static bool operator ==(LocalizableText? left, ReadOnlySpan<char> right) => left is not null && left.ToString().AsSpan().SequenceEqual(right);

    /// <summary>
    /// Whether this text shows another wording than the given one for the current UI culture.
    /// </summary>
    public static bool operator !=(LocalizableText? left, ReadOnlySpan<char> right) => !(left == right);

    /// <summary>
    /// Whether this text shows the given wording for the current UI culture: <c>"Afrekenen" == text</c> holds for a
    /// Dutch user. The comparison is by character, so letter case counts. A <see langword="null"/> text equals no
    /// wording, the empty string included.
    /// </summary>
    public static bool operator ==(ReadOnlySpan<char> left, LocalizableText? right) => right == left;

    /// <summary>
    /// Whether this text shows another wording than the given one for the current UI culture.
    /// </summary>
    public static bool operator !=(ReadOnlySpan<char> left, LocalizableText? right) => !(right == left);

    /// <summary>
    /// The wording for the current UI culture with its placeholders filled in:
    /// <c>ShopTexts.Greeting.Format(customer.Name)</c> turns <c>"Hello, {0}!"</c> into <c>"Hello, Anna!"</c>.
    /// </summary>
    /// <remarks>
    /// Values are formatted for <see cref="CultureInfo.CurrentCulture"/>, and a placeholder can carry a format, as in
    /// <c>{0:N2}</c> or <c>{0:d}</c>. The wording is a format string whatever the number of arguments: <c>{{</c> and
    /// <c>}}</c> show as single braces, also when you pass no arguments. To show the wording as written, use the text
    /// itself instead of calling this.
    /// </remarks>
    /// <param name="arguments">
    /// The values for the placeholders: the first fills <c>{0}</c>, the second <c>{1}</c>, and so on.
    /// </param>
    /// <returns>
    /// The wording with its placeholders filled in.
    /// </returns>
    /// <exception cref="FormatException">
    /// When the wording for the current UI culture is no valid format string, or has a placeholder without an argument.
    /// Give every language the same placeholders, pass a value for each, and write a literal brace as <c>{{</c> or
    /// <c>}}</c>.
    /// </exception>
    public string Format(params ReadOnlySpan<object?> arguments)
    {
        return String.Format(CultureInfo.CurrentCulture, this.ToString(), arguments);
    }

    /// <summary>
    /// The wording for a given culture with its placeholders filled in, such as a greeting in the language of an
    /// email's recipient: <c>ShopTexts.Greeting.Format(CultureInfo.GetCultureInfo("nl-NL"), customer.Name)</c>.
    /// </summary>
    /// <remarks>
    /// The culture gives both the wording, as <see cref="GetText(CultureInfo)"/> does, and the format of the values:
    /// a date or a number reads as the recipient writes it. As with <see cref="Format(ReadOnlySpan{object})"/>, the
    /// wording is a format string whatever the number of arguments.
    /// </remarks>
    /// <param name="culture">The culture to get the wording for, and to format the values for.</param>
    /// <param name="arguments">
    /// The values for the placeholders: the first fills <c>{0}</c>, the second <c>{1}</c>, and so on.
    /// </param>
    /// <returns>
    /// The wording with its placeholders filled in.
    /// </returns>
    /// <exception cref="FormatException">
    /// When the wording for the culture is no valid format string, or has a placeholder without an argument. Give every
    /// language the same placeholders, pass a value for each, and write a literal brace as <c>{{</c> or <c>}}</c>.
    /// </exception>
    public string Format(CultureInfo culture, params ReadOnlySpan<object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return String.Format(culture, this.GetText(culture), arguments);
    }

    /// <summary>
    /// The wording for a given culture instead of the current UI culture, such as the language of an email's recipient:
    /// <c>ShopTexts.Checkout.GetText(CultureInfo.GetCultureInfo("nl"))</c>. As for the current UI culture, a culture
    /// without a wording gets its parent culture's; a culture your app does not declare, or one without any wording,
    /// gets your app's default language, else the text's own default language.
    /// </summary>
    /// <param name="culture">The culture to get the wording for.</param>
    /// <returns>
    /// The wording for the culture.
    /// </returns>
    public string GetText(CultureInfo culture)
    {
        return this.GetText(culture, TranslationsRegistry.Current);
    }

    /// <summary>
    /// The wording for a culture, looked up in the sets of the given registry instead of those of the whole app: for a
    /// part of an app that keeps its own <see cref="ITranslations"/> sets. Create such a registry with
    /// <see cref="TranslationsRegistry.Create(IEnumerable{ITranslations})">TranslationsRegistry.Create</see>.
    /// </summary>
    /// <remarks>
    /// The registry also decides which languages show and which one a culture without wording gets: its
    /// <see cref="TranslationsRegistry.AppLanguages"/>, not those of your app. Without a registry, your app's
    /// languages decide.
    /// </remarks>
    /// <param name="culture">The culture to get the wording for.</param>
    /// <param name="registry">
    /// The sets to look in, or <see langword="null"/> to use only the text's own translations.
    /// </param>
    /// <returns>
    /// The wording for the culture.
    /// </returns>
    public string GetText(CultureInfo culture, TranslationsRegistry? registry)
    {
        // Without a registry there are only the text's own translations, which are the app's: its languages decide.
        return this.GetText(culture, registry, registry is null ? TranslationsRegistry.SupportedLanguages : registry.AppLanguages);
    }

    /// <summary>
    /// The wording for a culture, for the given app languages (the default first) instead of those of the registry or
    /// the app.
    /// </summary>
    internal string GetText(CultureInfo culture, TranslationsRegistry? registry, ImmutableArray<string> appLanguages)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return this.HasOnlyDefaultText ? this.DefaultText : this.FindText(culture, registry, appLanguages);
    }

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

        return (showsCulture ? this.GetTranslation(culture) : null)
               ?? (showsCulture && registry is not null ? this.FindTranslation(registry, new CultureFallback(culture)) : null)
               ?? (appDefaultLanguage is null ? null : this.GetTranslation(appDefaultLanguage, registry))
               ?? (registry is null ? null : this.FindDefaultTranslation(registry))
               ?? this.DefaultText;
    }

    /// <summary>
    /// Whether one of the app's languages serves a culture: the culture, or one it falls back to, is that language or a
    /// region of it.
    /// </summary>
    private static bool IsServed(CultureInfo culture, ImmutableArray<string> appLanguages)
    {
        // The names the lookups walk, so that this gate and the lookups agree on which language serves a culture, also
        // under invariant globalization, where nl-BE has no parent culture and nl still serves it.
        foreach (var name in new CultureFallback(culture))
            foreach (var language in appLanguages)
                if (name.StartsWith(language, StringComparison.OrdinalIgnoreCase) && (name.Length == language.Length || name[language.Length] == '-'))
                    return true;

        return false;
    }

    // A text made from a plain string, or declared by key alone: without translations of its own and without a
    // contract no step of the order above can find it other wording, in any culture, so it need not walk them.
    private bool HasOnlyDefaultText => this.TranslationPairs.Length is 0 && this is not ResourceText { Contract: not null };

    /// <summary>
    /// The wording the registered sets give this text for the first of the names that has a set, or
    /// <see langword="null"/> when none does.
    /// </summary>
    // A text with only inline translations belongs to no contract, so no set can phrase it; ResourceText overrides
    // this and the one below.
    private protected virtual string? FindTranslation(TranslationsRegistry registry, CultureFallback names) => null;

    /// <summary>
    /// The wording the set registered for the text's own default language gives it, or <see langword="null"/> when
    /// the text has no such language or no set does.
    /// </summary>
    private protected virtual string? FindDefaultTranslation(TranslationsRegistry registry) => null;

    /// <summary>
    /// The wording for a language code: the text's own translation for it or a language above it, else that of the set
    /// registered for it or a language above it (see <see cref="FindTranslation"/>).
    /// </summary>
    private string? GetTranslation(string language, TranslationsRegistry? registry)
    {
        // Walked by code rather than through CultureInfo.Parent: under invariant globalization (a NativeAOT console, a
        // trimmed WebAssembly app) only the invariant culture can be created. Dropping the last subtag (en-GB, then en)
        // is also how the build checks that a set serves each of the app's languages.
        foreach (var name in new CultureFallback(language))
            if (this.FindOwnTranslation(name) is { } translation)
                return translation;

        return registry is null
            ? null
            : this.FindTranslation(registry, new CultureFallback(language));
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

        foreach (var language in this.Translations.Keys)
            Add(language);

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
            if (!builder.ContainsKey(language) && this.GetTranslation(language, registry) is { } wording)
                builder[language] = wording;
        }

        // Whether two culture names start with the same language: nl-BE and nl, or zh-TW and zh-Hant.
        static bool HasSameLanguage(string name, string other)
        {
            var language = name.AsSpan(0, name.IndexOf('-') is var end and >= 0 ? end : name.Length);
            var otherLanguage = other.AsSpan(0, other.IndexOf('-') is var otherEnd and >= 0 ? otherEnd : other.Length);

            return language.Equals(otherLanguage, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The wording this text shows for a culture none of its languages serves, for a copy that needs no sets: that of
    /// the set registered for its own default language, else its <see cref="DefaultText"/>.
    /// </summary>
    internal string GetDefaultWording(TranslationsRegistry registry)
    {
        return this.FindDefaultTranslation(registry) ?? this.DefaultText;
    }
}
