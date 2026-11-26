using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace CodeChops.Idioma.Internals;

/// <summary>
/// <para>
/// Don't use this type directly! This code is called by source-generated code.
/// </para>
/// <para>
/// Write a text as <c>LocalizedText.Inline</c>, or in a set per language (see <see cref="ILocalizationSet"/>).
/// </para>
/// </summary>
// Public, because generated code lies in the consumer's assembly and can only call what is public. A class of its own,
// so these entry points don't show among the members of LocalizedText and LocalizationRegistry.
// Obsolete, so a call by hand warns (CS0618). Not as an error, because generated code can only suppress a warning.
[Obsolete("Don't use this class directly. Only generated code should call this. Write a text as LocalizedText.Inline, or in a set per language.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedCode
{
    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// The position, among the languages of the assembly, of the language whose wording a generated text shows for
    /// the current UI culture. The generated code of <c>LocalizedText.Inline</c> and of <c>[Localized]</c> members
    /// has a wording for every language of <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, and
    /// picks the one at that position.
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
    // An array, and not a span: the generated code passes the one array it keeps, which is told apart by reference.
#pragma warning disable MA0109 // Consider adding an overload with a Span<T> or Memory<T>
    public static int GetLanguageIndex(string[] languages)
#pragma warning restore MA0109
    {
        Debug.Assert(languages is not null);

        var index = InlineLanguage.IndexOfShownLanguage(languages);
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
            catch (TargetInvocationException exception) when (exception.InnerException is { } thrown)
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
    /// <see cref="ResolveWording{TContract, TDefaultSet}(int, int, string, Func{TContract, string})"/> otherwise:
    /// <code>
    /// GetSet&lt;IShopLocalization&gt;(1, 3)?.Checkout
    ///     ?? ResolveWording&lt;IShopLocalization, ShopLocalizationEn&gt;(1, 3, "Checkout", static set => set.Checkout)
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
    public static TContract? GetSet<TContract>(int text, int count)
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
            provided = LocalizationRegistry.GetCultureFromScopeOrProvider(provider);
            isAsked = true;
            culture = provided ?? CultureInfo.CurrentUICulture;
        }

        var sets = ResolvedSets<TContract>.Get(culture, count).Sets;
        var set = (uint)text < (uint)sets.Length ? sets[text] : null;

        // ResolveWording words the text next, and takes this answer rather than ask the provider again, which may read
        // a request or a user's profile.
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
    /// <c>ResolveWording</c> otherwise:
    /// <code>
    /// GetSetForValues&lt;IShopLocalization&gt;(2, 3)?.Greeting(name)
    ///     ?? ResolveWording&lt;IShopLocalization, ShopLocalizationEn, string&gt;(2, 3, "Greeting", static (set, name) => set.Greeting(name), name)
    /// </code>
    /// </para>
    /// </summary>
    /// <remarks>
    /// The set's method formats the values as it runs, for <see cref="CultureInfo.CurrentCulture"/>. Where
    /// <see cref="LocalizationRegistry.CurrentUICultureProvider"/> gives a culture the thread doesn't have, this gives
    /// <see langword="null"/>, and <c>ResolveWording</c> runs the method in that culture.
    /// </remarks>
    /// <typeparam name="TContract">The contract the text belongs to.</typeparam>
    /// <param name="text">The position of the text among the texts of its generated class.</param>
    /// <param name="count">The number of texts the generated class has.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TContract? GetSetForValues<TContract>(int text, int count)
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
            var provided = LocalizationRegistry.GetCultureFromScopeOrProvider(provider);

            // The method must run in the provided culture, which takes a scope where the thread has another, or one of its
            // name that formats values otherwise (CultureFormats).
            // ResolveWording enters it, with this answer of the provider.
            if (provided is not null && (!CultureFormats.AreAlike(provided, CultureInfo.CurrentCulture) || !provided.Equals(CultureInfo.CurrentUICulture)))
            {
                WordingScope.KeepProvidedCulture(provided);
                return null;
            }

            culture = provided ?? CultureInfo.CurrentUICulture;

            var keptSets = ResolvedSets<TContract>.Get(culture, count).SetsForValues;
            var keptSet = (uint)text < (uint)keptSets.Length ? keptSets[text] : null;

            if (keptSet is null)
                WordingScope.KeepProvidedCulture(provided);

            return keptSet;
        }

        var sets = ResolvedSets<TContract>.Get(culture, count).SetsForValues;

        var set = (uint)text < (uint)sets.Length ? sets[text] : null;
        return set;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// The wording of a text of a localization contract for the current UI culture, for a text whose set
    /// <see cref="GetSet{TContract}"/> doesn't hold. It finds the set that words the text, and keeps it where the same
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
    /// <exception cref="InvalidOperationException">
    /// When every set gives <see langword="null"/> as the wording. Return a string from the set of the default
    /// language, such as an empty one where a value is missing. Also when the constructor of that set throws, or it has
    /// no language code (<c>LOC208</c>).
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet>(int text, int count, string name, Func<TContract, string?> selector)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(selector is not null);

        var wording = Wording<TContract, TDefaultSet>.Of(text, count, name, selector);
        return wording;
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// The wording of a text of a localization contract that takes values, for the current UI culture, for a text whose
    /// set <see cref="GetSetForValues{TContract}"/> doesn't give. There's an overload per number of values.
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
    /// (<see cref="ResolveWording{TContract, TDefaultSet}(int, int, string, Func{TContract, string})"/>).
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
    /// <exception cref="InvalidOperationException">
    /// When every set gives <see langword="null"/> as the wording. Also when the constructor of the set of the default
    /// language throws, or that set has no language code (<c>LOC208</c>).
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet, TValue1>(
        int text, int count, string name,
        Func<TContract, TValue1, string?> selector,
        TValue1 value1)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(selector is not null);

        var wording = Wording<TContract, TDefaultSet>.OfValues(text, count, name, (selector, value1),
            static (set, call) => call.selector(set, call.value1));

        return wording;
    }

    /// <inheritdoc cref="ResolveWording{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet, TValue1, TValue2>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, string?> selector,
        TValue1 value1, TValue2 value2)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(selector is not null);

        var wording = Wording<TContract, TDefaultSet>.OfValues(text, count, name, (selector, value1, value2),
            static (set, call) => call.selector(set, call.value1, call.value2));

        return wording;
    }

    /// <inheritdoc cref="ResolveWording{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet, TValue1, TValue2, TValue3>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(selector is not null);

        var wording = Wording<TContract, TDefaultSet>.OfValues(text, count, name, (selector, value1, value2, value3),
            static (set, call) => call.selector(set, call.value1, call.value2, call.value3));

        return wording;
    }

    /// <inheritdoc cref="ResolveWording{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet, TValue1, TValue2, TValue3, TValue4>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(selector is not null);

        var wording = Wording<TContract, TDefaultSet>.OfValues(text, count, name, (selector, value1, value2, value3, value4),
            static (set, call) => call.selector(set, call.value1, call.value2, call.value3, call.value4));

        return wording;
    }

    /// <inheritdoc cref="ResolveWording{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet, TValue1, TValue2, TValue3, TValue4, TValue5>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(selector is not null);

        var wording = Wording<TContract, TDefaultSet>.OfValues(text, count, name, (selector, value1, value2, value3, value4, value5),
            static (set, call) => call.selector(set, call.value1, call.value2, call.value3, call.value4, call.value5));

        return wording;
    }

    /// <inheritdoc cref="ResolveWording{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5, TValue6 value6)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(selector is not null);

        var wording = Wording<TContract, TDefaultSet>.OfValues(text, count, name, (selector, value1, value2, value3, value4, value5, value6),
            static (set, call) => call.selector(set, call.value1, call.value2, call.value3, call.value4, call.value5, call.value6));

        return wording;
    }

    /// <inheritdoc cref="ResolveWording{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5, TValue6 value6, TValue7 value7)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(selector is not null);

        var wording = Wording<TContract, TDefaultSet>.OfValues(text, count, name, (selector, value1, value2, value3, value4, value5, value6, value7),
            static (set, call) => call.selector(set, call.value1, call.value2, call.value3, call.value4, call.value5, call.value6, call.value7));

        return wording;
    }

    /// <inheritdoc cref="ResolveWording{TContract, TDefaultSet, TValue1}(int, int, string, Func{TContract, TValue1, string}, TValue1)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string ResolveWording<TContract, TDefaultSet, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7, TValue8>(
        int text, int count, string name,
        Func<TContract, TValue1, TValue2, TValue3, TValue4, TValue5, TValue6, TValue7, TValue8, string?> selector,
        TValue1 value1, TValue2 value2, TValue3 value3, TValue4 value4, TValue5 value5, TValue6 value6, TValue7 value7, TValue8 value8)
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        Debug.Assert(selector is not null);

        var wording = Wording<TContract, TDefaultSet>.OfValues(text, count, name, (selector, value1, value2, value3, value4, value5, value6, value7, value8),
            static (set, call) => call.selector(set, call.value1, call.value2, call.value3, call.value4, call.value5, call.value6, call.value7, call.value8));

        return wording;
    }

    /// <summary>
    /// Words a text of a contract for the reader of this thread, from the sets of the whole app. Every
    /// <c>ResolveWording</c> ends here.
    /// </summary>
    /// <remarks>
    /// A text with parameters whose set is kept is worded by that set in the scope it needs, without a lookup. That's a
    /// scope of the language the text falls back to (<see cref="ResolvedSets{TContract}.FallbackSets"/>), or of a
    /// provider's culture the thread doesn't have. Any other text is looked up in the registry
    /// (<see cref="LocalizationRegistry.Word"/>), which keeps its set where the same set words it on every call.
    /// </remarks>
    /// <typeparam name="TContract">The contract the text belongs to.</typeparam>
    /// <typeparam name="TDefaultSet">The contract's set of the default language.</typeparam>
    // A class of its own, so that a call infers the type of the values from the tuple it passes.
    private static class Wording<TContract, TDefaultSet>
        where TContract : class
        where TDefaultSet : class, ILocalizationSet, TContract, new()
    {
        /// <summary>
        /// The wording of a text without parameters.
        /// </summary>
        // A property is never kept as a fallback, so there's none to look for. LocalizationRegistry.KeepSet says why.
        public static string Of(int text, int count, string name, Func<TContract, string?> select)
        {
            Debug.Assert(name is not null);

            var culture = WordingScope.GetReaderCulture(out var providedCulture, takesKept: true);
            var resolved = ResolvedSets<TContract>.Get(culture, count);

            var wording = LookUp(culture, providedCulture, resolved, text, name, select, takesValues: false);
            return wording;
        }

        /// <summary>
        /// The wording of a text with parameters.
        /// </summary>
        /// <typeparam name="TCall">The selector of the text and the values it was called with, as a tuple.</typeparam>
        /// <param name="text">The position of the text among the texts of its generated class.</param>
        /// <param name="count">The number of texts the generated class has.</param>
        /// <param name="name">The name of the text.</param>
        /// <param name="call">The selector and the values.</param>
        /// <param name="word">
        /// Hands a set the values through the selector. A static lambda, so it holds nothing and allocates nothing.
        /// </param>
        public static string OfValues<TCall>(int text, int count, string name, TCall call, Func<TContract, TCall, string?> word)
        {
            Debug.Assert(name is not null);

            var culture = WordingScope.GetReaderCulture(out var providedCulture, takesKept: true);
            var resolved = ResolvedSets<TContract>.Get(culture, count);
            var setsForValues = resolved.SetsForValues;

            // The set kept for the text, which the generated class couldn't call itself, because a provider gives a
            // culture the thread doesn't have. Its method runs in a scope of that culture.
            if ((uint)text < (uint)setsForValues.Length && setsForValues[text] is { } kept)
            {
                using var scope = new WordingScope(culture, providedCulture);

                if (word(kept, call) is { } keptWording)
                    return keptWording;
            }
            else if (resolved.GetFallback(text) is { } fallback)
            {
                // A method formats its values as it runs, so it runs in a scope wherever a text with values does. Where
                // the language has no culture on this machine, that's the scope of the reader, as in the lookup.
                using var scope = new WordingScope(fallback.Culture ?? culture, providedCulture);

                if (word(fallback.Set, call) is { } fallbackWording)
                    return fallbackWording;
            }

            var wording = LookUpWithValues(culture, providedCulture, resolved, text, name, call, word);
            return wording;
        }

        // A method of its own, because C# creates the closure over a method's parameters as that method starts, also
        // where the lambda is never reached. In OfValues, a text kept as a fallback would allocate it on every call.
        private static string LookUpWithValues<TCall>(CultureInfo culture, CultureInfo? providedCulture, ResolvedSets<TContract> resolved,
            int text, string name, TCall call, Func<TContract, TCall, string?> word)
        {
            var wording = LookUp(culture, providedCulture, resolved, text, name, set => word(set, call), takesValues: true);
            return wording;
        }

        private static string LookUp(CultureInfo culture, CultureInfo? providedCulture, ResolvedSets<TContract> resolved,
            int text, string name, Func<TContract, string?> select, bool takesValues)
        {
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
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// Records the function that finds the texts of an assembly's members marked <c>[Localized]</c>, so that
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
    /// Gives the values of an enum the texts of a set of it, in place of the texts its members are marked
    /// <c>[Localized]</c> with in the enum's own assembly. That assembly's <c>GetText()</c> then gives the set's
    /// wording, in the languages of the set. A set of a library's enum registers this when the assembly that holds
    /// the set starts.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Where two assemblies register texts of one enum, those of the assembly whose sets registered later give them,
    /// whichever registered its texts first. An app's registrar registers the sets of the assemblies it references
    /// first, and its own last. So the app wins over a library it references, and a library over one it references.
    /// Of two that registered no sets, the later registration wins.
    /// </remarks>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <typeparam name="TContract">The contract of the set, which tells the assembly that holds it.</typeparam>
    /// <param name="getText">Gives a value of the enum its text, as <c>GetTextByValue</c> of the set's generated class does.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="getText"/> is <see langword="null"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterEnumTexts<TEnum, TContract>(Func<TEnum, string> getText)
        where TEnum : struct, Enum
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(getText);

        EnumTexts<TEnum>.Add(typeof(TContract).Assembly, getText);
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
    /// </para>
    /// <para>
    /// The text a set gives a value of an enum, where one registered its texts
    /// (<see cref="RegisterEnumTexts{TEnum, TContract}"/>), or <see langword="null"/>. <c>GetText()</c> of an enum whose
    /// members are marked <c>[Localized]</c> asks this first.
    /// </para>
    /// </summary>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <param name="value">The value to give the text of.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string? GetEnumText<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        // Null for nearly every enum, where no set registered its texts.
        if (EnumTexts<TEnum>.Chosen is not { } chosen)
            return null;

        // Chosen again after a registration since, which may change which one wins.
        if (chosen.Count != LocalizationRegistry.RegistrationCount)
            chosen = EnumTexts<TEnum>.Choose();

        var text = chosen.GetText?.Invoke(value);
        return text;
    }

    /// <summary>
    /// Per enum, what gives its values the texts of a set of it (<see cref="RegisterEnumTexts{TEnum, TContract}"/>):
    /// every registration, and the one that wins.
    /// </summary>
    private static class EnumTexts<TEnum>
        where TEnum : struct, Enum
    {
        /// <summary>
        /// The registration that gives the texts, or <see langword="null"/> where none registered. A field per enum,
        /// which a text reads without a lookup: the enum's own <c>GetText()</c> asks on every call.
        /// </summary>
        public static ChosenTexts? Chosen;

        // Every registration, in the order they came.
        private static ImmutableArray<Registration> Registered = [];

        private static int Forgets;

        public static void Add(Assembly assembly, Func<TEnum, string> getText)
        {
            ImmutableInterlocked.Update(ref Registered, static (registered, added) => registered.Add(added), new Registration(assembly, getText));

            // Forgotten when a plugin that gave a lasting enum its texts unloads. An enum of the plugin itself goes with
            // it, and a function kept for it would hold the plugin.
            CollectibleAssemblies.Watch(assembly);

            if (assembly.IsCollectible && !typeof(TEnum).IsCollectible && Interlocked.Exchange(ref Forgets, value: 1) is 0)
                CollectibleAssemblies.AddForgetter(static context => Forget(context));

            LocalizationRegistry.CountRegistration();
            Choose();
        }

        /// <summary>
        /// Chooses the registration that gives the texts: that of the assembly whose sets registered last, and of two
        /// alike, the later one.
        /// </summary>
        public static ChosenTexts Choose()
        {
            // Counted before the registrations are read, so that one that comes in meanwhile makes the next read choose
            // again.
            var count = LocalizationRegistry.RegistrationCount;

            Func<TEnum, string>? getText = null;
            var latest = -1L;

            foreach (var registration in Registered)
            {
                var at = LocalizationRegistry.GetSetsRegisteredAt(registration.Assembly);

                if (at >= latest)
                {
                    getText = registration.GetText;
                    latest = at;
                }
            }

            var chosen = new ChosenTexts(count, getText);
            Chosen = chosen;

            return chosen;
        }

        private static void Forget(AssemblyLoadContext context)
        {
            ImmutableInterlocked.Update(
                ref Registered,
                static (registered, context) => registered.RemoveAll(registration => CollectibleAssemblies.IsIn(registration.Assembly, context)),
                context);

            Choose();
        }

        /// <summary>
        /// A registration: the assembly of the set, and what gives a value its text.
        /// </summary>
        private sealed class Registration(Assembly assembly, Func<TEnum, string> getText)
        {
            public Assembly Assembly { get; } = assembly;
            public Func<TEnum, string> GetText { get; } = getText;
        }

        /// <summary>
        /// The registration that wins, and the count of registrations it was chosen at.
        /// </summary>
        public sealed class ChosenTexts(int count, Func<TEnum, string>? getText)
        {
            public int Count { get; } = count;
            public Func<TEnum, string>? GetText { get; } = getText;
        }
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
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
    /// <see langword="null"/> is accepted only for a parameter that may be <see langword="null"/>: a nullable value type,
    /// or a reference type that <paramref name="acceptsNull"/> says is nullable.
    /// </remarks>
    /// <typeparam name="TValue">The type of the parameter.</typeparam>
    /// <param name="key">The key the text was asked for by, which is its name.</param>
    /// <param name="values">The values that were passed, in the order of the parameters.</param>
    /// <param name="index">The position of the parameter.</param>
    /// <param name="count">The number of parameters the text has.</param>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="acceptsNull">
    /// Whether the parameter may be <see langword="null"/>, as its nullable annotation says. The type argument can't say
    /// it, because <c>string</c> and <c>string?</c> are the same type at run time. <see langword="true"/> for a
    /// parameter without an annotation, as in code without nullable reference types.
    /// </param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="values"/> doesn't hold one value per parameter, or the value at
    /// <paramref name="index"/> isn't of type <typeparamref name="TValue"/>.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TValue GetKeyedValue<TValue>(string key, ReadOnlySpan<object?> values, int index, int count, string parameterName, bool acceptsNull)
    {
        if (values.Length != count)
            throw new ArgumentException($"The text '{key}' takes {count} {(count is 1 ? "value" : "values")}, but {values.Length} {(values.Length is 1 ? "was" : "were")} passed. Pass one for each of its parameters, in their order, after the key.", nameof(values));

        var value = values[index];

        if (value is TValue typed)
            return typed;

        if (value is null)
        {
            if (acceptsNull && default(TValue) is null)
                return default!;

            throw new ArgumentException(
                $"The text '{key}' takes a value of type '{typeof(TValue)}' for '{parameterName}', which isn't nullable, but null was passed. Pass a value that isn't null.",
                nameof(values));
        }

        throw new ArgumentException(
            $"The text '{key}' takes a value of type '{typeof(TValue)}' for '{parameterName}', but a value of type '{value.GetType()}' was passed. Pass a value of that type, because a value isn't converted.",
            nameof(values));
    }

    /// <summary>
    /// <para>
    /// Don't use this method directly! This code is called by source-generated code.
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
    public static ArgumentNullException CreateNullKeyException<TContract>(string? key)
        where TContract : class
    {
        Debug.Assert(key is null);

        var exception = new ArgumentNullException(nameof(key));
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
}
