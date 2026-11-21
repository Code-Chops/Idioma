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
    /// A call with the same strings as an earlier one gets the text that call created, where it's still kept. The same
    /// means the same string objects, not only the same characters. A string literal is the same object on every call,
    /// so a call with literals finds its text. A text isn't kept when part of its wording is a new string, as wording
    /// with a value is, because no later call could find it. Wording of 3,900 characters or more isn't kept either. A
    /// string that outlived a garbage collection before the call is kept like a literal, also when it holds a value.
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
    /// <code>
    /// GeneratedCode.CreateSetText&lt;IShopLocalization&gt;("MyApp.Localization.ShopLocalization.Checkout",
    ///     static set => set.Checkout, DefaultSet&lt;ShopLocalizationEn&gt;.Instance)
    /// </code>
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
    /// implement <see cref="ILocalizationSet.LocalizationLanguageCode"/>. Name the set after its language (<c>ShopLocalizationEn</c>),
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
    /// Creates the text of an enum value that isn't a member of its enum, such as a combination of flags. The method
    /// <c>GetTextByValue</c> of a generated class calls this for a value that no text of its
    /// <see cref="ILocalizationSet{TEnum}">set</see> is named after.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The text shows the value as <see cref="Enum.ToString()"/> writes it, in every language. That's its number, or
    /// the names of its flags.
    /// </remarks>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <param name="value">The value.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateUndefinedEnumText<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var text = new LocalizableText(value.ToString());
        return text;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Creates the text of a member marked <c>[Localized]</c>, such as a property or a member of an enum. The
    /// generated code holds the text, so this is called once per member.
    /// </para>
    /// </summary>
    /// <param name="fullKey">
    /// The full name of the member, such as <c>Shop.Domain.Address.City</c>. It's the text's
    /// <see cref="LocalizableText.FullKey"/>.
    /// </param>
    /// <param name="key">The name of the member, such as <c>City</c>. It's the text's <see cref="LocalizableText.Key"/>.</param>
    /// <param name="wordings">
    /// The wording per language, the default language first. Each is a language (a culture name such as <c>en</c> or
    /// <c>en-GB</c>) and its wording.
    /// </param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="fullKey"/> is empty or white-space, or <paramref name="wordings"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="fullKey"/>, <paramref name="key"/>, a language, or the wording of a language is
    /// <see langword="null"/>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText CreateMemberText(string fullKey, string key, params ReadOnlySpan<(string Language, string Text)> wordings)
    {
        var text = LocalizableText.CreateMemberText(fullKey, key, wordings);
        return text;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Records the function that finds the texts of an assembly's members marked <c>[Localized]</c>, so that
    /// <c>LocalizableText.GetTextByMember</c> finds them. The assembly's generated registrar calls this once.
    /// </para>
    /// </summary>
    /// <param name="find">
    /// Gives the text of a member by the type that declares it and the member's name, or the type's own text for a
    /// name that is <see langword="null"/>. It gives <see langword="null"/> for what has no text. A type with type
    /// parameters is passed as its definition.
    /// </param>
    /// <exception cref="ArgumentNullException">When <paramref name="find"/> is <see langword="null"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RecordMemberTexts(Func<Type, string?, LocalizableText?> find)
    {
        LocalizableText.RecordMemberTexts(find);
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Checks that no values were passed for a text without parameters, and returns the text. The method <c>GetTextByKey</c> of
    /// a generated class calls this for such a text: <c>ShopLocalization.GetTextByKey("Checkout")</c>.
    /// </para>
    /// </summary>
    /// <typeparam name="TContract">The contract the text belongs to.</typeparam>
    /// <param name="text">The text the key names.</param>
    /// <param name="key">The key the text was asked for by. See <see cref="LocalizableText.Key"/>.</param>
    /// <param name="values">The values that were passed.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="values"/> holds a value, because the text takes none.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static LocalizableText GetKeyedText<TContract>(LocalizableText text, string key, ReadOnlySpan<object?> values)
        where TContract : class
    {
        if (!values.IsEmpty)
            throw new ArgumentException($"The text '{key}' of '{typeof(TContract).Name}' takes no values, but {values.Length} {(values.Length is 1 ? "was" : "were")} passed. Pass the key alone.", nameof(values));

        return text;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Takes the value of one parameter of a text from the values that were passed. The method <c>GetTextByKey</c> of a
    /// generated class calls this for each parameter of a text with parameters:
    /// <c>ShopLocalization.GetTextByKey("Greeting", customer.Name)</c>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// A value isn't converted, with one exception. A <see cref="string"/> for a parameter of type
    /// <see cref="LocalizableText"/> becomes a text with that one wording, as it does in a call of the text's method.
    /// </para>
    /// <para>
    /// So an <see cref="int"/> isn't accepted for a parameter of type <see cref="long"/>. <see langword="null"/> is
    /// accepted for a parameter whose type allows it.
    /// </para>
    /// </remarks>
    /// <typeparam name="TValue">The type of the parameter.</typeparam>
    /// <param name="key">The key the text was asked for by. See <see cref="LocalizableText.Key"/>.</param>
    /// <param name="values">The values that were passed, in the order of the parameters.</param>
    /// <param name="index">The position of the parameter.</param>
    /// <param name="count">The number of parameters the text has.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="values"/> doesn't hold one value per parameter, or the value at
    /// <paramref name="index"/> isn't of type <typeparamref name="TValue"/>, nor a string for a text.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TValue GetKeyedValue<TValue>(string key, ReadOnlySpan<object?> values, int index, int count, string parameterName)
    {
        if (values.Length != count)
            throw new ArgumentException($"The text '{key}' takes {count} {(count is 1 ? "value" : "values")}, but {values.Length} {(values.Length is 1 ? "was" : "were")} passed. Pass one for each of its parameters, in their order, after the key.", nameof(values));

        var value = values[index];

        if (value is TValue typed)
            return typed;

        if (value is null && default(TValue) is null)
            return default!;

        // The one conversion a call of the text's method makes itself, so a key from a database can come with the
        // string it has. Past the type test above, so a value of the right type pays nothing for it.
        if (value is string wording && typeof(TValue) == typeof(LocalizableText))
            return (TValue)(object)new LocalizableText(wording);

        var passed = value is null ? "null was" : $"a value of type '{value.GetType()}' was";

        throw new ArgumentException(
            $"The text '{key}' takes a value of type '{typeof(TValue)}' for '{parameterName}', but {passed} passed. Pass a value of that type, because a value isn't converted.",
            nameof(values));
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Creates the exception that the method <c>GetTextByKey</c> of a generated class throws for a key that none of its texts
    /// has.
    /// </para>
    /// </summary>
    /// <typeparam name="TContract">The contract of the generated class.</typeparam>
    /// <param name="key">The key that was passed.</param>
    /// <param name="className">The name of the generated class, such as <c>ShopLocalization</c>.</param>
    /// <returns>
    /// An <see cref="ArgumentNullException"/> when <paramref name="key"/> is <see langword="null"/>, and a
    /// <see cref="KeyNotFoundException"/> for any other key.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Exception CreateUnknownKeyException<TContract>(string? key, string className)
        where TContract : class
    {
        if (key is null)
            return new ArgumentNullException(nameof(key));

        var exception = new KeyNotFoundException(
            $"'{className}' has no text with the key '{key}'. A key is the name of a text of '{typeof(TContract).Name}', in the same letter case.");

        return exception;
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
    /// <see cref="ILocalizationSet.LocalizationLanguageCode"/>.
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

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Registers the sets of an assembly, which the registry creates when it's first read: when the first text is
    /// shown. So a set whose constructor throws throws from there, and not before the app's <c>Main</c> runs, where
    /// nothing could catch it. Each set is filed under the language <see cref="RecordSet"/> recorded for its type, or
    /// under the one it gives once it's created.
    /// </para>
    /// </summary>
    /// <param name="asDefaults">
    /// Whether the sets give a library's own wording, as <see cref="LocalizationRegistry.RegisterDefaults"/> takes
    /// them, rather than reword it, as <see cref="LocalizationRegistry.Register(ReadOnlySpan{ILocalizationSet})"/>
    /// takes them.
    /// </param>
    /// <param name="sets">Each set's type, with what creates it, in the order they apply.</param>
    /// <exception cref="ArgumentNullException">When a type or what creates it is <see langword="null"/>.</exception>
    // Added after 0.9, next to LocalizationRegistry.RegisterDefaults and Register, which the code generated by earlier
    // versions calls, and which stay for it.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterSets(bool asDefaults, params ReadOnlySpan<(Type Type, Func<ILocalizationSet> Create)> sets)
    {
        LocalizationRegistry.RegisterSets(sets, asDefaults);
    }
}
