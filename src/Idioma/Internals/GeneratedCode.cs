namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// Don't use this type directly! This code is called by source-generated code.
/// </para>
/// <para>
/// Write a text as <c>LocalizableText.Inline</c>, or in a set per language (see <see cref="ILocalizationSet"/>).
/// </para>
/// </summary>
// Public, because generated code lies in the consumer's assembly and can only call what is public. A class of its own,
// so these entry points don't show among the members of LocalizableText and LocalizationRegistry.
// Obsolete, so a call by hand warns (CS0618). Not as an error, because generated code can only suppress a warning.
[Obsolete("Don't use this class directly. Only generated code should call this. Write a text as LocalizableText.Inline, or in a set per language.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedCode
{
    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Creates a text with its translations, for the generated <c>LocalizableText.Inline</c>.
    /// <c>LocalizableText.Inline</c> demands every language of
    /// <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// A call with the same strings as an earlier one, the same objects as string literals are, gets the text that call
    /// created, where it's still kept. A text isn't kept when part of its wording is a new string, as wording with a
    /// value is, because no later call could find it. Wording of 40,000 characters or more isn't kept either. A
    /// string that outlived a garbage collection before the call is kept like a literal, also when it holds a value.
    /// </para>
    /// <para>
    /// A language isn't checked for being blank or listed twice, because markup creates an inline text with a value on
    /// every render. The generated <c>LocalizableText.Inline</c> passes each language of
    /// <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see> once, and the build checks those.
    /// </para>
    /// </remarks>
    /// <param name="defaultText">
    /// The wording shown for every culture without a translation. It may be empty, as the wording of any other
    /// language may.
    /// </param>
    /// <param name="translations">
    /// The translations, each a language (a culture name such as <c>en</c> or <c>en-GB</c>, in any letter case) and its
    /// wording. Each language is listed once.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="defaultText"/>, a language, or the wording of a language is <see langword="null"/>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateText(string defaultText, params ReadOnlySpan<(string Language, string Text)> translations)
    {
        var text = LocalizableText.FindOrCreateInline(defaultText, translations);
        return text;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Declares a text of a localization contract, with its default wording taken from a set. The generated class
    /// declares its texts this way:
    /// <c>GeneratedCode.CreateSetText&lt;IShopLocalization&gt;("MyApp.Localization.ShopLocalization.Checkout",
    /// static set => set.Checkout, DefaultSet&lt;ShopLocalizationEn&gt;.Instance)</c>.
    /// <c>DefaultSet&lt;ShopLocalizationEn&gt;.Instance</c> is one instance of the set, which the generated code
    /// creates once.
    /// </para>
    /// </summary>
    /// <typeparam name="TContract">
    /// The contract the text belongs to. Every registered set implementing it provides the text.
    /// </typeparam>
    /// <param name="key">The name of this text in a resource file.</param>
    /// <param name="selector">Picks the text from a set.</param>
    /// <param name="defaultSet">
    /// The set that supplies the default wording, the contract's set of the default language. It implements
    /// <typeparamref name="TContract"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="key"/> is empty or white-space. Pass the name the text has in the resource file. Also when
    /// <paramref name="defaultSet"/> doesn't implement <typeparamref name="TContract"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="key"/>, <paramref name="selector"/>, or <paramref name="defaultSet"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When <paramref name="defaultSet"/> has no language code, because its name doesn't end in one, and it doesn't
    /// implement <see cref="ILocalizationSet.LanguageCode"/>. Name the set after its language (<c>ShopLocalizationEn</c>),
    /// or implement that property.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateSetText<TContract>(string key, Func<TContract, string> selector, ILocalizationSet defaultSet)
        where TContract : class
    {
        var text = LocalizableText.Create(key, selector, defaultSet);
        return text;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Declares a text of a localization contract that takes values, with its default wording taken from a set. A
    /// method of the generated class creates its text this way, on every call, with an overload per number of values:
    /// <c>GeneratedCode.CreateSetText&lt;IShopLocalization, string&gt;("MyApp.Localization.ShopLocalization.Greeting",
    /// static (set, name) => set.Greeting(name), DefaultSet&lt;ShopLocalizationEn&gt;.Instance,
    /// name)</c>.
    /// <c>DefaultSet&lt;ShopLocalizationEn&gt;.Instance</c> is one instance of the set, which the generated code
    /// creates once.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text keeps each value as its own type, so a number isn't boxed until something reads
    /// <see cref="LocalizableText.Arguments"/>. A text takes at most 8 values (<c>LOC300</c>).
    /// </para>
    /// <para>
    /// The set's language code isn't read here. A set without one throws <see cref="InvalidOperationException"/>
    /// when the text's <see cref="LocalizableText.DefaultLanguage"/> is read, such as when the text falls back to its
    /// default wording. The build warns about such a set with <c>LOC400</c>.
    /// </para>
    /// <para>
    /// The key isn't checked for being blank, and the set isn't checked for implementing
    /// <typeparamref name="TContract"/>, because a text that takes values is created on every call. The generated class
    /// always passes a key and a set that are valid.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContract">
    /// The contract (interface) the text belongs to. Every registered set implementing it provides the text.
    /// </typeparam>
    /// <typeparam name="TValue1">The type of the first value.</typeparam>
    /// <param name="key">The name of this text in a resource file.</param>
    /// <param name="selector">
    /// Picks the text from a set of the contract, with the values the text keeps. It's handed those values, so it holds
    /// none itself and one delegate serves every call.
    /// </param>
    /// <param name="defaultSet">
    /// The set that supplies the default wording, the contract's set of the default language. It implements
    /// <typeparamref name="TContract"/>.
    /// </param>
    /// <param name="value1">The first value: the first of the text's <see cref="LocalizableText.Arguments"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="key"/>, <paramref name="selector"/>, or <paramref name="defaultSet"/> is
    /// <see langword="null"/>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateSetText<TContract, TValue1>(
        string key,
        Func<TContract, TValue1, string> selector,
        ILocalizationSet defaultSet,
        TValue1 value1)
        where TContract : class
    {
        var origin = SetTextWithValuesOrigin.Of(
            key: key,
            contract: typeof(TContract),
            selector: selector,
            callSelector: static (select, set, values) => select((TContract)set, values.Item1),
            defaultSet: defaultSet,
            values: new ValueTuple<TValue1>(value1));

        var text = LocalizableText.Create(origin);
        return text;
    }

    /// <inheritdoc cref="CreateSetText{TContract, TValue1}(string, Func{TContract, TValue1, string}, ILocalizationSet, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateSetText<TContract, TValue1, TValue2>(
        string key,
        Func<TContract, TValue1, TValue2, string> selector,
        ILocalizationSet defaultSet,
        TValue1 value1, TValue2 value2)
        where TContract : class
    {
        var origin = SetTextWithValuesOrigin.Of(
            key: key,
            contract: typeof(TContract),
            selector: selector,
            callSelector: static (select, set, values) => select((TContract)set, values.value1, values.value2),
            defaultSet: defaultSet,
            values: (value1, value2));

        var text = LocalizableText.Create(origin);
        return text;
    }

    /// <inheritdoc cref="CreateSetText{TContract, TValue1}(string, Func{TContract, TValue1, string}, ILocalizationSet, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateSetText<TContract, TValue1, TValue2, TValue3>(
        string key,
        Func<TContract, TValue1, TValue2, TValue3, string> selector,
        ILocalizationSet defaultSet,
        TValue1 value1, TValue2 value2, TValue3 value3)
        where TContract : class
    {
        var origin = SetTextWithValuesOrigin.Of(
            key: key,
            contract: typeof(TContract),
            selector: selector,
            callSelector: static (select, set, values) => select((TContract)set, values.value1, values.value2, values.value3),
            defaultSet: defaultSet,
            values: (value1, value2, value3));

        var text = LocalizableText.Create(origin);
        return text;
    }

    /// <inheritdoc cref="CreateSetText{TContract, TValue1}(string, Func{TContract, TValue1, string}, ILocalizationSet, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateSetText<TContract, TValue1, TValue2, TValue3, TValue4>(
        string key,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, string> selector,
        ILocalizationSet defaultSet,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4)
        where TContract : class
    {
        var origin = SetTextWithValuesOrigin.Of(
            key: key,
            contract: typeof(TContract),
            selector: selector,
            callSelector: static (select, set, values) => select((TContract)set, values.value1, values.value2, values.value3, values.value4),
            defaultSet: defaultSet,
            values: (value1, value2, value3, value4));

        var text = LocalizableText.Create(origin);
        return text;
    }

    /// <inheritdoc cref="CreateSetText{TContract, TValue1}(string, Func{TContract, TValue1, string}, ILocalizationSet, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateSetText<TContract, TValue1, TValue2, TValue3, TValue4, TValue5>(
        string key,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, string> selector,
        ILocalizationSet defaultSet,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5)
        where TContract : class
    {
        var origin = SetTextWithValuesOrigin.Of(
            key: key,
            contract: typeof(TContract),
            selector: selector,
            callSelector: static (select, set, values) => select((TContract)set, values.value1, values.value2, values.value3, values.value4, values.value5),
            defaultSet: defaultSet,
            values: (value1, value2, value3, value4, value5));

        var text = LocalizableText.Create(origin);
        return text;
    }

    /// <inheritdoc cref="CreateSetText{TContract, TValue1}(string, Func{TContract, TValue1, string}, ILocalizationSet, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateSetText<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6>(
        string key,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, string> selector,
        ILocalizationSet defaultSet,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5, TValue6 value6)
        where TContract : class
    {
        var origin = SetTextWithValuesOrigin.Of(
            key: key,
            contract: typeof(TContract),
            selector: selector,
            callSelector: static (select, set, values) => select((TContract)set, values.value1, values.value2, values.value3, values.value4, values.value5, values.value6),
            defaultSet: defaultSet,
            values: (value1, value2, value3, value4, value5, value6));

        var text = LocalizableText.Create(origin);
        return text;
    }

    /// <inheritdoc cref="CreateSetText{TContract, TValue1}(string, Func{TContract, TValue1, string}, ILocalizationSet, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateSetText<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7>(
        string key,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7, string> selector,
        ILocalizationSet defaultSet,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5, TValue6 value6, TValue7 value7)
        where TContract : class
    {
        var origin = SetTextWithValuesOrigin.Of(
            key: key,
            contract: typeof(TContract),
            selector: selector,
            callSelector: static (select, set, values) => select((TContract)set, values.value1, values.value2, values.value3, values.value4, values.value5, values.value6, values.value7),
            defaultSet: defaultSet,
            values: (value1, value2, value3, value4, value5, value6, value7));

        var text = LocalizableText.Create(origin);
        return text;
    }

    /// <inheritdoc cref="CreateSetText{TContract, TValue1}(string, Func{TContract, TValue1, string}, ILocalizationSet, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateSetText<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7, TValue8>(
        string key,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7, TValue8, string> selector,
        ILocalizationSet defaultSet,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5, TValue6 value6, TValue7 value7, TValue8 value8)
        where TContract : class
    {
        var origin = SetTextWithValuesOrigin.Of(
            key: key,
            contract: typeof(TContract),
            selector: selector,
            callSelector: static (select, set, values) => select((TContract)set, values.value1, values.value2, values.value3, values.value4, values.value5, values.value6, values.value7, values.value8),
            defaultSet: defaultSet,
            values: (value1, value2, value3, value4, value5, value6, value7, value8));

        var text = LocalizableText.Create(origin);
        return text;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Sets the app's <see cref="LocalizationRegistry.SupportedLanguages"/> when an application that declares
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see> starts. Only the first call counts,
    /// so an application loaded later, such as the app a test project tests, can't change them.
    /// </para>
    /// </summary>
    /// <param name="defaultLanguage">The default language: a culture name such as <c>en</c> or <c>en-GB</c>.</param>
    /// <param name="otherLanguages">The other languages, each a culture name.</param>
    /// <exception cref="ArgumentException">
    /// When a language isn't a culture name, such as an empty string or <c>en_GB</c>. Pass a culture name such as
    /// <c>en</c> or <c>en-GB</c>. Also when a language is listed twice, as <c>en</c> and <c>EN</c> are.
    /// </exception>
    /// <exception cref="ArgumentNullException">When a language is <see langword="null"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterLanguages(string defaultLanguage, params ReadOnlySpan<string> otherLanguages)
    {
        LocalizationRegistry.RegisterLanguages(defaultLanguage, otherLanguages);
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Records what the build read about a set, before the set registers. That's its language, such as <c>nl</c> for
    /// <c>ShopLocalizationNl</c>, or <c>en-GB</c> for <c>ShopLocalizationEnGb</c> in an assembly that declares
    /// <c>en-GB</c>. For a set that derives from another, it's also the texts its own class words, so the registry knows
    /// which texts the set rewords without calling it. A set has one code, recorded once.
    /// </para>
    /// </summary>
    /// <param name="languageCode">
    /// The language: a culture name such as <c>en-GB</c>. <see langword="null"/> for a set that implements
    /// <see cref="ILocalizationSet.LanguageCode"/>.
    /// </param>
    /// <param name="ownTexts">
    /// The names of the texts the set's own class words, such as <c>Checkout</c>, for a set that derives from another.
    /// </param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="languageCode"/> isn't a culture name, such as an empty string or <c>en_GB</c>. Pass a
    /// culture name such as <c>en-GB</c>. Also when the set already has another code, because a set's code is recorded
    /// once.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="ownTexts"/> is <see langword="null"/>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RecordSet<TSet>(string? languageCode, params string[] ownTexts)
        where TSet : class, ILocalizationSet
    {
        LocalizationRegistry.RecordSet<TSet>(languageCode, ownTexts);
    }
}
