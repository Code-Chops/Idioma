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
    /// Picks the wording of the generated <c>LocalizableText.Inline</c> for the current UI culture: the position,
    /// among the languages of the assembly, of the one the text shows. <c>LocalizableText.Inline</c> demands every
    /// language of <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, and returns the wording at that
    /// position.
    /// </para>
    /// </summary>
    /// <remarks>
    /// A culture with wording gets it, or else that of a culture above it. A culture none of the app's languages
    /// serves gets the wording in the app's default language, else the first.
    /// </remarks>
    /// <param name="languages">
    /// The languages of the assembly, each a culture name such as <c>en</c> or <c>en-GB</c>, in any letter case, the
    /// default language first. The generated code passes the same array on every call.
    /// </param>
    /// <exception cref="ArgumentNullException">When <paramref name="languages"/> is <see langword="null"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    // An array, and not a span: the generated code passes the one array it keeps, which is told apart by reference.
#pragma warning disable MA0109 // Consider adding an overload with a Span<T> or Memory<T>
    public static int FindLanguage(string[] languages)
#pragma warning restore MA0109
    {
        ArgumentNullException.ThrowIfNull(languages);

        var index = InlineLanguage.Find(languages);
        return index;
    }

    /// <summary>
    /// The set of a contract's default language, which gives the texts of the generated class their default wording.
    /// It's one instance per set, created when a text first needs it.
    /// </summary>
    /// <remarks>
    /// The set isn't created as the generated class is first used. So a set whose constructor throws doesn't fail that
    /// class for good, with an exception that names only the class. The text that needs the set throws, and so does
    /// every text after it.
    /// </remarks>
    /// <typeparam name="TSet">The set of the default language, such as <c>ShopLocalizationEn</c>.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// When the set's constructor throws, with that exception inside. The message names the set.
    /// </exception>
    internal static TSet GetDefaultSet<TSet>()
        where TSet : class, ILocalizationSet, new()
    {
        var set = DefaultSet<TSet>.Instance ?? DefaultSet<TSet>.Create();
        return set;
    }

    /// <summary>
    /// The one instance of a set of a default language (<see cref="GetDefaultSet{TSet}"/>).
    /// </summary>
    private static class DefaultSet<TSet>
        where TSet : class, ILocalizationSet, new()
    {
        internal static TSet? Instance;

        // Not kept where the constructor throws, so the next text tries again and says the same. Two threads may each
        // create the set, and the first one to finish gives every text its instance.
        internal static TSet Create()
        {
            var created = (TSet)LocalizationRegistry.CreateSet(typeof(TSet), static () => New());

            var set = Interlocked.CompareExchange(ref Instance, created, comparand: null) ?? created;
            return set;
        }

        // new TSet() of a type parameter creates the set through the runtime, which puts what the constructor throws
        // inside an exception of its own. Taken out again, so that the exception that names the set holds the set's
        // own, as for a set the registry creates.
        private static TSet New()
        {
            try
            {
                return new TSet();
            }
            catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is { } thrown)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(thrown);
                throw;
            }
        }
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// The set that words a text without parameters for the current UI culture, or <see langword="null"/> where none
    /// is kept yet. A property of the generated class reads its wording from it, and words the text with
    /// <see cref="WordSetText{TContract, TDefaultSet}(int, int, string, Func{TContract, string})"/> otherwise:
    /// <code>
    /// FindSet&lt;IShopLocalization&gt;(1, 3)?.Checkout
    ///     ?? WordSetText&lt;IShopLocalization, ShopLocalizationEn&gt;(1, 3, "Checkout", static set => set.Checkout)
    /// </code>
    /// </para>
    /// </summary>
    /// <remarks>
    /// The current UI culture is <see cref="CultureInfo.CurrentUICulture"/>, unless the app sets
    /// <see cref="LocalizationRegistry.CurrentUICultureProvider"/>. Nothing is allocated.
    /// </remarks>
    /// <typeparam name="TContract">The contract the text belongs to.</typeparam>
    /// <param name="text">The position of the text among the texts of its generated class.</param>
    /// <param name="count">The number of texts the generated class has.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TContract? FindSet<TContract>(int text, int count)
        where TContract : class
    {
        CultureInfo culture;
        CultureInfo? provided = null;
        var isAsked = false;

        // Inside a text being worded, its culture, which a provider would otherwise override. Without a provider that
        // culture is the thread's, so the scope isn't read.
        if (LocalizationRegistry.CurrentUICultureProvider is not { } provider)
        {
            culture = CultureInfo.CurrentUICulture;
        }
        else if (WordingScope.Current is { } entered)
        {
            culture = entered;
        }
        else
        {
            provided = LocalizationRegistry.AskProvider(provider);
            isAsked = true;
            culture = provided ?? CultureInfo.CurrentUICulture;
        }

        var sets = ResolvedSets<TContract>.Find(culture, count).Sets;
        var set = (uint)text < (uint)sets.Length ? sets[text] : null;

        // WordSetText words the text next, and takes this answer rather than ask the provider again, which may read a
        // request or a user's profile.
        if (set is null && isAsked)
            WordingScope.KeepProvidedCulture(provided);

        return set;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// The set that words a text with parameters for the current UI culture, or <see langword="null"/> where none is
    /// kept yet. A method of the generated class calls the set's method with its values, and words the text with
    /// <c>WordSetText</c> otherwise:
    /// <code>
    /// FindSetForValues&lt;IShopLocalization&gt;(2, 3)?.Greeting(name)
    ///     ?? WordSetText&lt;IShopLocalization, ShopLocalizationEn, string&gt;(2, 3, "Greeting", static (set, name) => set.Greeting(name), name)
    /// </code>
    /// </para>
    /// </summary>
    /// <remarks>
    /// The set's method formats the values as it runs, for <see cref="CultureInfo.CurrentCulture"/>. Where
    /// <see cref="LocalizationRegistry.CurrentUICultureProvider"/> gives a culture the thread doesn't have, this gives
    /// <see langword="null"/>, and <c>WordSetText</c> runs the method in that culture.
    /// </remarks>
    /// <typeparam name="TContract">The contract the text belongs to.</typeparam>
    /// <param name="text">The position of the text among the texts of its generated class.</param>
    /// <param name="count">The number of texts the generated class has.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TContract? FindSetForValues<TContract>(int text, int count)
        where TContract : class
    {
        CultureInfo culture;

        if (LocalizationRegistry.CurrentUICultureProvider is not { } provider)
        {
            culture = CultureInfo.CurrentUICulture;
        }
        else if (WordingScope.Current is { } entered)
        {
            // Inside a text being worded, which set the thread's cultures for its own.
            culture = entered;
        }
        else
        {
            var provided = LocalizationRegistry.AskProvider(provider);

            // The method must run in the provided culture, which takes a scope where the thread has another.
            // WordSetText enters it, with this answer of the provider.
            if (provided is not null && (!ReferenceEquals(provided, CultureInfo.CurrentCulture) || !provided.Equals(CultureInfo.CurrentUICulture)))
            {
                WordingScope.KeepProvidedCulture(provided);
                return null;
            }

            culture = provided ?? CultureInfo.CurrentUICulture;

            var keptSets = ResolvedSets<TContract>.Find(culture, count).SetsForValues;
            var keptSet = (uint)text < (uint)keptSets.Length ? keptSets[text] : null;

            if (keptSet is null)
                WordingScope.KeepProvidedCulture(provided);

            return keptSet;
        }

        var sets = ResolvedSets<TContract>.Find(culture, count).SetsForValues;

        var set = (uint)text < (uint)sets.Length ? sets[text] : null;
        return set;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// The wording of a text of a localization contract for the current UI culture, for a text whose set
    /// <see cref="FindSet{TContract}"/> doesn't hold. It finds the set that words the text, and keeps it where the same
    /// set words the text on every call.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The text gives the first wording it finds: that of the set for the current UI culture, then that of the set for
    /// the app's default language, then that of the set for the language of <typeparamref name="TDefaultSet"/>, and
    /// last that of <typeparamref name="TDefaultSet"/> itself, which is created when a text first needs it.
    /// </remarks>
    /// <typeparam name="TContract">
    /// The contract the text belongs to. Every registered set implementing it provides the text.
    /// </typeparam>
    /// <typeparam name="TDefaultSet">The contract's set of the default language, such as <c>ShopLocalizationEn</c>.</typeparam>
    /// <param name="text">The position of the text among the texts of its generated class.</param>
    /// <param name="count">The number of texts the generated class has.</param>
    /// <param name="name">The name of the text, such as <c>Checkout</c>.</param>
    /// <param name="selector">Picks the text from a set.</param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="name"/> or <paramref name="selector"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When every set gives <see langword="null"/> as the wording. Return a string from the set of the default
    /// language, such as an empty one where a value is missing. Also when the constructor of that set throws, or it has
    /// no language code (<c>LOC208</c>).
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string WordSetText<TContract, TDefaultSet>(int text, int count, string name, Func<TContract, string?> selector)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var wording = Word<TContract, TDefaultSet>(text, count, name, selector, takesValues: false);
        return wording;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// The wording of a text of a localization contract that takes values, for the current UI culture, for a text whose
    /// set <see cref="FindSetForValues{TContract}"/> doesn't give. There's an overload per number of values.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The set's method runs in the culture the text is worded for. Where
    /// <see cref="LocalizationRegistry.CurrentUICultureProvider"/> gives a culture, the values are formatted for that
    /// culture too. A text takes at most 8 values (<c>LOC400</c>).
    /// </para>
    /// <para>
    /// The text falls back as a text without parameters does
    /// (<see cref="WordSetText{TContract, TDefaultSet}(int, int, string, Func{TContract, string})"/>).
    /// </para>
    /// </remarks>
    /// <typeparam name="TContract">
    /// The contract (interface) the text belongs to. Every registered set implementing it provides the text.
    /// </typeparam>
    /// <typeparam name="TDefaultSet">The contract's set of the default language, such as <c>ShopLocalizationEn</c>.</typeparam>
    /// <typeparam name="TValue1">The type of the first value.</typeparam>
    /// <param name="text">The position of the text among the texts of its generated class.</param>
    /// <param name="count">The number of texts the generated class has.</param>
    /// <param name="name">The name of the text, such as <c>Greeting</c>.</param>
    /// <param name="selector">
    /// Picks the text from a set of the contract, with the values. It's handed those values, so it holds none itself
    /// and one delegate serves every call.
    /// </param>
    /// <param name="value1">The first value.</param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="name"/> or <paramref name="selector"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When every set gives <see langword="null"/> as the wording. Also when the constructor of the set of the default
    /// language throws, or that set has no language code (<c>LOC208</c>).
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string WordSetText<TContract, TDefaultSet, TValue1>(
        int text, int count, string name,
        Func<TContract, TValue1, string?> selector,
        TValue1 value1)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var wording = Word<TContract, TDefaultSet>(text, count, name, set => selector(set, value1), takesValues: true);
        return wording;
    }

    /// <inheritdoc cref="WordSetText{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string WordSetText<TContract, TDefaultSet, TValue1, TValue2>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, string?> selector,
        TValue1 value1, TValue2 value2)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var wording = Word<TContract, TDefaultSet>(text, count, name, set => selector(set, value1, value2), takesValues: true);
        return wording;
    }

    /// <inheritdoc cref="WordSetText{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string WordSetText<TContract, TDefaultSet, TValue1, TValue2, TValue3>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var wording = Word<TContract, TDefaultSet>(text, count, name, set => selector(set, value1, value2, value3), takesValues: true);
        return wording;
    }

    /// <inheritdoc cref="WordSetText{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string WordSetText<TContract, TDefaultSet, TValue1, TValue2, TValue3, TValue4>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var wording = Word<TContract, TDefaultSet>(text, count, name, set => selector(set, value1, value2, value3, value4), takesValues: true);
        return wording;
    }

    /// <inheritdoc cref="WordSetText{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string WordSetText<TContract, TDefaultSet, TValue1, TValue2, TValue3, TValue4, TValue5>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var wording = Word<TContract, TDefaultSet>(text, count, name, set => selector(set, value1, value2, value3, value4, value5), takesValues: true);
        return wording;
    }

    /// <inheritdoc cref="WordSetText{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string WordSetText<TContract, TDefaultSet, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5, TValue6 value6)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var wording = Word<TContract, TDefaultSet>(text, count, name, set => selector(set, value1, value2, value3, value4, value5, value6), takesValues: true);
        return wording;
    }

    /// <inheritdoc cref="WordSetText{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string WordSetText<TContract, TDefaultSet, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5, TValue6 value6, TValue7 value7)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var wording = Word<TContract, TDefaultSet>(text, count, name, set => selector(set, value1, value2, value3, value4, value5, value6, value7), takesValues: true);
        return wording;
    }

    /// <inheritdoc cref="WordSetText{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string WordSetText<TContract, TDefaultSet, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7, TValue8>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7, TValue8, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5, TValue6 value6, TValue7 value7, TValue8 value8)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        ArgumentNullException.ThrowIfNull(selector);

        var wording = Word<TContract, TDefaultSet>(text, count, name, set => selector(set, value1, value2, value3, value4, value5, value6, value7, value8), takesValues: true);
        return wording;
    }

    /// <summary>
    /// Words a text of a contract for the reader of this thread, from the sets of the whole app. Every
    /// <c>WordSetText</c> ends here.
    /// </summary>
    private static string Word<TContract, TDefaultSet>(int text, int count, string name, Func<TContract, string?> select, bool takesValues)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(select);

        var culture = WordingScope.FindReaderCulture(out var providedCulture, takesKept: true);
        var resolved = ResolvedSets<TContract>.Find(culture, count);
        var lookup = resolved.Lookup;

        var wording = lookup.Registry.Word(
            lookup,
            culture,
            valuesCulture: providedCulture,
            name,
            select,
            defaultSetType: typeof(TDefaultSet),
            getDefaultSet: static () => GetDefaultSet<TDefaultSet>(),
            takesValues,
            resolved,
            text);

        return wording;
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
    /// <param name="languages">
    /// The languages of the assembly, each a culture name such as <c>en</c> or <c>en-GB</c>, the default language
    /// first.
    /// </param>
    /// <param name="find">
    /// Gives the wording of a member per language, in the order of <paramref name="languages"/>, by the type that
    /// declares the member and the member's name, or the type's own wording for a name that is <see langword="null"/>.
    /// It gives <see langword="null"/> for what has no text. A type with type parameters is passed as its definition.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="languages"/> or <paramref name="find"/> is <see langword="null"/>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable MA0109 // Consider adding an overload with a Span<T> or Memory<T> - The languages are kept.
    public static void RecordMemberTexts(string[] languages, Func<Type, string?, string[]?> find)
#pragma warning restore MA0109
    {
        MemberTexts.Record(languages, find);
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
    /// <param name="text">The wording of the text the key names.</param>
    /// <param name="key">The key the text was asked for by, which is its name.</param>
    /// <param name="values">The values that were passed.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="values"/> holds a value, because the text takes none.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string GetKeyedText<TContract>(string text, string key, ReadOnlySpan<object?> values)
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
    /// A value isn't converted. So an <see cref="int"/> isn't accepted for a parameter of type <see cref="long"/>.
    /// <see langword="null"/> is accepted for a parameter whose type allows it.
    /// </remarks>
    /// <typeparam name="TValue">The type of the parameter.</typeparam>
    /// <param name="key">The key the text was asked for by, which is its name.</param>
    /// <param name="values">The values that were passed, in the order of the parameters.</param>
    /// <param name="index">The position of the parameter.</param>
    /// <param name="count">The number of parameters the text has.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="values"/> doesn't hold one value per parameter, or the value at
    /// <paramref name="index"/> isn't of type <typeparamref name="TValue"/>.
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
    /// Sets the app's <see cref="LocalizationRegistry.SupportedLanguages"/> when an application starts, to what its
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see> declares. Only the first call
    /// counts, so an application loaded later, such as the app a test project tests, can't change them.
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
    /// <see cref="ILocalizationSet.LocalizationLanguageCode"/> with something other than a constant the build can read.
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
