using System.Diagnostics;

namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// <b>Don't use this type directly! This code is called by source-generated code.</b>
/// </para>
/// <para>
/// Write a text as <c>LocalizedText.Inline</c>, or in a set per language (see <see cref="ILocalizationSet"/>).
/// </para>
/// </summary>
// Public, because generated code lies in the consumer's assembly and can only call what is public. A class of its own,
// so these entry points don't show among the members of LocalizedText and LocalizationRegistry.
[Obsolete("Don't use this class directly. Only generated code should call this. Write a text as LocalizedText.Inline, or in a set per language.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class GeneratedCode
{
    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// The position, among the languages of the assembly, of the language whose wording a standalone text shows for
    /// the current UI culture. A standalone text is written with <c>LocalizedText.Inline</c>, or with
    /// <c>[Localized]</c> on a member. Its generated code has a wording for every language of
    /// <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, and picks the one at that position.
    /// </para>
    /// </summary>
    /// <remarks>
    /// A culture with wording gets it, or else that of a culture it falls back to. A culture none of the app's
    /// languages serves gets the wording in the app's default language, else the first.
    /// </remarks>
    /// <param name="languages">
    /// The languages of the assembly, each a culture name such as <c>en</c> or <c>en-GB</c>, in any letter case, the
    /// default language first. The generated code passes the same array on every call.
    /// </param>
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable MA0109 // Consider adding an overload with a Span<T> or Memory<T>
    public static int GetLanguageIndexForStandaloneText(string[] languages)
#pragma warning restore MA0109
    {
        Debug.Assert(languages is not null);

        var index = StandaloneLanguage.IndexOfShownLanguage(languages);
        return index;
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// The set that words a text without parameters for <see cref="CultureInfo.CurrentUICulture"/>, or
    /// <see langword="null"/> where none is kept yet. A property of the generated class reads its wording from it, and
    /// words the text with <see cref="ResolveWording{TContract, TDefaultSet}(int, int, string, Func{TContract, string})"/>
    /// otherwise:
    /// <code>
    /// GetLocalizationSet&lt;IShopLocalization&gt;(1, 3)?.Checkout
    ///     ?? ResolveWording&lt;IShopLocalization, ShopLocalizationEn&gt;(1, 3, "Checkout", static set => set.Checkout)
    /// </code>
    /// </para>
    /// </summary>
    /// <remarks>
    /// Nothing is allocated.
    /// </remarks>
    /// <typeparam name="TContract">The contract the text belongs to.</typeparam>
    /// <param name="textIndex">The position of the text among the texts of its generated class.</param>
    /// <param name="textCount">The number of texts the generated class has.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TContract? GetLocalizationSet<TContract>(int textIndex, int textCount)
        where TContract : class
    {
        var sets = ResolvedSets<TContract>.Get(CultureInfo.CurrentUICulture, textCount).Sets;

        var set = (uint)textIndex < (uint)sets.Length ? sets[textIndex] : null;
        return set;
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// The set that words a text with parameters for <see cref="CultureInfo.CurrentUICulture"/>, or
    /// <see langword="null"/> where none is kept yet. A method of the generated class calls the set's method with its
    /// values, and words the text with <c>ResolveWording</c> otherwise:
    /// <code>
    /// GetLocalizationSetForValues&lt;IShopLocalization&gt;(2, 3)?.Greeting(name)
    ///     ?? ResolveWording&lt;IShopLocalization, ShopLocalizationEn, string&gt;(2, 3, "Greeting", static (set, name) => set.Greeting(name), name)
    /// </code>
    /// </para>
    /// </summary>
    /// <remarks>
    /// The set's method formats the values as it runs, for <see cref="CultureInfo.CurrentCulture"/>. A set whose method
    /// has to run in the culture of a fallback language isn't given here. <c>ResolveWording</c> runs it in that culture.
    /// </remarks>
    /// <typeparam name="TContract">The contract the text belongs to.</typeparam>
    /// <param name="textIndex">The position of the text among the texts of its generated class.</param>
    /// <param name="textCount">The number of texts the generated class has.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TContract? GetLocalizationSetForValues<TContract>(int textIndex, int textCount)
        where TContract : class
    {
        var sets = ResolvedSets<TContract>.Get(CultureInfo.CurrentUICulture, textCount).SetsForValues;

        var set = (uint)textIndex < (uint)sets.Length ? sets[textIndex] : null;
        return set;
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// The wording of a text of a localization contract for <see cref="CultureInfo.CurrentUICulture"/>, for a text
    /// whose set <see cref="GetLocalizationSet{TContract}"/> doesn't hold. It finds the set that words the text, and
    /// keeps it where the same set words the text on every call.
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
    /// <param name="textIndex">The position of the text among the texts of its generated class.</param>
    /// <param name="textCount">The number of texts the generated class has.</param>
    /// <param name="name">The name of the text, such as <c>Checkout</c>.</param>
    /// <param name="selector">Picks the text from a set.</param>
    /// <exception cref="InvalidOperationException">
    /// When the set that words the text gives <see langword="null"/> as the wording. Return a string, such as an empty
    /// one where a value is missing. Also when the constructor of the set of the default language throws, or that set
    /// has no language code (<c>LOC208</c>).
    /// </exception>
    // A property is never kept as a fallback, so there's none to look for. LocalizationRegistry.KeepSet says why.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet>(int textIndex, int textCount, string name,
        Func<TContract, string?> selector)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(name is not null);
        Debug.Assert(selector is not null);

        var resolved = ResolvedSets<TContract>.Get(CultureInfo.CurrentUICulture, textCount);

        var wording = LookUp<TContract, TDefaultSet>(resolved, textIndex, name, selector, takesValues: false);
        return wording;
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// The wording of a text of a localization contract that takes values, for
    /// <see cref="CultureInfo.CurrentUICulture"/>, for a text whose set
    /// <see cref="GetLocalizationSetForValues{TContract}"/> doesn't give. The values come as one: the value itself for
    /// a text with one parameter, and a tuple of them for more:
    /// <code>
    /// ResolveWording&lt;IShopLocalization, ShopLocalizationEn, (string, int)&gt;(2, 3, "Order",
    ///     static (set, values) => set.Order(values.Item1, values.Item2), (name, count))
    /// </code>
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text falls back as a text without parameters does
    /// (<see cref="ResolveWording{TContract, TDefaultSet}(int, int, string, Func{TContract, string})"/>).
    /// </para>
    /// <para>
    /// A set that words the text on every call in a language the text falls back to is called in a scope of that
    /// language, without a lookup (<see cref="ResolvedSets{TContract}.GetFallback"/>). Any other text is looked up in
    /// the registry (<see cref="LocalizationRegistry.Word"/>), which keeps its set where the same set words it on every
    /// call.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContract">
    /// The contract the text belongs to. Every registered set implementing it provides the text.
    /// </typeparam>
    /// <typeparam name="TDefaultSet">The contract's set of the default language, such as <c>ShopLocalizationEn</c>.</typeparam>
    /// <typeparam name="TValues">The type of the value, or the tuple of the values.</typeparam>
    /// <param name="textIndex">The position of the text among the texts of its generated class.</param>
    /// <param name="textCount">The number of texts the generated class has.</param>
    /// <param name="name">The name of the text, such as <c>Greeting</c>.</param>
    /// <param name="selector">
    /// Picks the text from a set of the contract, with the values. It's handed those values, so it holds none itself
    /// and one delegate serves every call.
    /// </param>
    /// <param name="values">The value, or the tuple of the values.</param>
    /// <exception cref="InvalidOperationException">
    /// When the set that words the text gives <see langword="null"/> as the wording. Also when the constructor of the
    /// set of the default language throws, or that set has no language code (<c>LOC208</c>).
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet, TValues>(int textIndex, int textCount, string name,
        Func<TContract, TValues, string?> selector, TValues values)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(name is not null);
        Debug.Assert(selector is not null);

        var resolved = ResolvedSets<TContract>.Get(CultureInfo.CurrentUICulture, textCount);

        if (resolved.GetFallback(textIndex) is { } fallback)
        {
            // Where the language has no culture on this machine, no scope is entered, as in the lookup.
            using var scope = new WordingScope(fallback.Culture);

            var fallbackWording = selector(fallback.Set, values) ?? throw LocalizationRegistry.CreateNullWordingException(fallback.Set, name);
            return fallbackWording;
        }

        var wording = LookUpWithValues<TContract, TDefaultSet, TValues>(resolved, textIndex, name, selector, values);
        return wording;
    }

    // A method of its own, because C# creates the closure over a method's parameters as that method starts, also where
    // the lambda is never reached. In ResolveWording, a text kept as a fallback would allocate it on every call.
    private static string LookUpWithValues<TContract, TDefaultSet, TValues>(ResolvedSets<TContract> resolved, int textIndex,
        string name, Func<TContract, TValues, string?> selector, TValues values)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        var wording = LookUp<TContract, TDefaultSet>(resolved, textIndex, name, set => selector(set, values), takesValues: true);
        return wording;
    }

    /// <summary>
    /// Words a text of a contract for the user of this thread, from the sets of the whole app. Every
    /// <c>ResolveWording</c> ends here.
    /// </summary>
    private static string LookUp<TContract, TDefaultSet>(ResolvedSets<TContract> resolved, int textIndex, string name,
        Func<TContract, string?> select, bool takesValues)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        var lookup = resolved.Lookup;

        var wording = lookup.Registry.Word(
            lookup: lookup,
            name: name,
            select: select,
            defaultSetType: typeof(TDefaultSet),
            getDefaultSet: static () => GetDefaultSet<TDefaultSet>(),
            takesValues: takesValues,
            resolved: resolved,
            textIndex: textIndex);

        return wording;
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// Checks that no values were passed for a text without parameters, and returns its wording. The method <c>GetTextByKey</c> of
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
    public static string ResolveKeyedWording<TContract>(string text, string key, ReadOnlySpan<object?> values)
        where TContract : class
    {
        if (!values.IsEmpty)
            throw new ArgumentException($"The text '{key}' of '{typeof(TContract).Name}' takes no values, but {values.Length} {(values.Length is 1 ? "was" : "were")} passed. Pass the key alone.", nameof(values));

        return text;
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// Takes the value of one parameter of a text from the values that were passed. The method <c>GetTextByKey</c> of a
    /// generated class calls this for each parameter of a text with parameters:
    /// <c>ShopLocalization.GetTextByKey("Greeting", customer.Name)</c>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// A value isn't converted. So an <see cref="int"/> isn't accepted for a parameter of type <see cref="long"/>.
    /// <see langword="null"/> is accepted only for a parameter that may be <see langword="null"/>: a nullable value type,
    /// or a reference type that <paramref name="acceptsNull"/> says is nullable.
    /// </remarks>
    /// <typeparam name="TValue">The type of the parameter.</typeparam>
    /// <param name="key">The key the text was asked for by, which is its name.</param>
    /// <param name="values">The values that were passed, in the order of the parameters.</param>
    /// <param name="parameterIndex">The position of the parameter.</param>
    /// <param name="parameterCount">The number of parameters the text has.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="acceptsNull">
    /// Whether the parameter may be <see langword="null"/>, as its nullable annotation says. The type argument can't say
    /// it, because <see langword="string"/> and <c>string?</c> are the same type at run time. <see langword="true"/> for a
    /// parameter without an annotation, as in code without nullable reference types.
    /// </param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="values"/> doesn't hold one value per parameter, or the value at
    /// <paramref name="parameterIndex"/> isn't of type <typeparamref name="TValue"/>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TValue GetKeyedValue<TValue>(string key, ReadOnlySpan<object?> values, int parameterIndex, int parameterCount, string parameterName, bool acceptsNull)
    {
        if (values.Length != parameterCount)
            throw new ArgumentException($"The text '{key}' takes {parameterCount} {(parameterCount is 1 ? "value" : "values")}, but {values.Length} {(values.Length is 1 ? "was" : "were")} passed. Pass one for each of its parameters, in their order, after the key.", nameof(values));

        var value = values[parameterIndex];

        if (value is not TValue typed)
        {
            if (value is null)
            {
                if (acceptsNull && default(TValue) is null)
                    return default!;

                throw new ArgumentException($"The text '{key}' takes a value of type '{typeof(TValue)}' for '{parameterName}', which isn't nullable, but null was passed. Pass a value that isn't null.", nameof(values));
            }

            throw new ArgumentException($"The text '{key}' takes a value of type '{typeof(TValue)}' for '{parameterName}', but a value of type '{value.GetType()}' was passed. Pass a value of that type, because a value isn't converted.", nameof(values));
        }

        return typed;
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// Creates the exception that the method <c>GetTextByKey</c> of a generated class throws for a key that is
    /// <see langword="null"/>. An unknown key gives <see langword="null"/> there, and throws nothing.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The type argument only keeps a text of the generated class from hiding this method. A text has none, so a name
    /// with one isn't looked for among the texts.
    /// </remarks>
    /// <typeparam name="TContract">The contract of the generated class.</typeparam>
    /// <param name="key">The key that was passed, which is <see langword="null"/>. Its name is that of the parameter of <c>GetTextByKey</c>.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Global
    public static ArgumentNullException CreateNullKeyException<TContract>(string? key)
        where TContract : class
    {
        Debug.Assert(key is null);

        var exception = new ArgumentNullException(nameof(key));
        return exception;
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// Sets the app's <see cref="LocalizationRegistry.SupportedLanguages"/> when an application starts, to what its
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see> declares. Only the first call
    /// counts, so an application loaded later, such as the app a test project tests, can't change them.
    /// </para>
    /// <para>
    /// That first call also tells which assembly is the app. Its texts of an enum win over those of every library
    /// (<see cref="RegisterEnumTexts{TEnum, TContract}"/>).
    /// </para>
    /// </summary>
    /// <param name="registrar">The registrar of the application, which tells its assembly.</param>
    /// <param name="defaultLanguage">The default language: a culture name such as <c>en</c> or <c>en-GB</c>.</param>
    /// <param name="otherLanguages">The other languages, each a culture name.</param>
    /// <exception cref="ArgumentException">
    /// When a language isn't a culture name, such as an empty string or <c>en_GB</c>. Pass a culture name such as
    /// <c>en</c> or <c>en-GB</c>. Also when a language is listed twice, as <c>en</c> and <c>EN</c> are.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="registrar"/> or a language is <see langword="null"/>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterLanguages(Type registrar, string defaultLanguage, params ReadOnlySpan<string> otherLanguages)
    {
        ArgumentNullException.ThrowIfNull(registrar);

        LocalizationRegistry.RegisterLanguages(registrar.Assembly, defaultLanguage, otherLanguages);
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// Describes a set as the build read it, before the set registers. That's its language, such as <c>nl</c> for
    /// <c>ShopLocalizationNl</c>, or <c>en-GB</c> for <c>ShopLocalizationEnGb</c> in an assembly that declares
    /// <c>en-GB</c>. For a set that derives from another, it's also the texts its own class words, so the registry knows
    /// which texts the set rewords without calling it. A set has one code, described once.
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
    /// culture name such as <c>en-GB</c>. Also when the set already has another code, because a set's code is described
    /// once.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="ownTexts"/> is <see langword="null"/>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void DescribeSet<TSet>(string? languageCode, params string[] ownTexts)
        where TSet : class, ILocalizationSet
    {
        LocalizationRegistry.DescribeSet<TSet>(languageCode, ownTexts);
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// Registers the sets of an assembly, which the registry creates when it's first read: when the first text is
    /// shown. So a set whose constructor throws throws from there, and not before the app's <c>Main</c> runs, where
    /// nothing could catch it. Each set is filed under the language its type is described with (<see cref="DescribeSet"/>),
    /// or under the one it gives once it's created.
    /// </para>
    /// </summary>
    /// <param name="asDefaults">
    /// Whether the sets give the wording of a contract in their own assembly. Every other set wins over them, such as a
    /// set from <see cref="LocalizationRegistry.Register(ReadOnlySpan{ILocalizationSet})"/>.
    /// </param>
    /// <param name="sets">Each set's type, with what creates it, in the order they apply.</param>
    /// <exception cref="ArgumentNullException">When a type or what creates it is <see langword="null"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterSets(bool asDefaults, params ReadOnlySpan<(Type Type, Func<ILocalizationSet> Create)> sets)
    {
        LocalizationRegistry.RegisterSets(sets, asDefaults);
    }

    /// <summary>
    /// <para>
    /// <b>Don't use this method directly! This code is called by source-generated code.</b>
    /// </para>
    /// <para>
    /// Registers the function that finds the texts of an assembly's members marked <c>[Localized]</c>, so that
    /// <c>LocalizedText.GetTextByMember</c> finds them. The assembly's generated registrar calls this once.
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
    public static void RegisterMemberTexts(string[] languages, Func<Type, string?, string[]?> find)
#pragma warning restore MA0109
    {
        MemberTexts.Register(languages, find);
    }
}
