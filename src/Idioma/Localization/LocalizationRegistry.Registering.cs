using System.Diagnostics;
using System.Reflection;

namespace CodeChops.Idioma;

// The part of LocalizationRegistry that keeps what's registered: the sets, the app's languages, and how the build
// described each set.
//
// Nothing here takes a lock. The registrations are one object that never changes, and a registration swaps it for a
// new one in one step.
/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// What <see cref="Current"/> is built from: every registered set, and the app's languages.
    /// </summary>
    // A field, because the swap takes it by reference. A swap captures a value rather than use the overload that takes
    // an argument, because ReadyToRun can't precompile that one for a struct.
    private static Registrations Registered = Registrations.Empty;

    /// <summary>
    /// The language the build read for each set (<see cref="DescribeSet"/>). Lock-free to read, because filing a set
    /// reads it.
    /// </summary>
    // The registry reads no name, so a set keeps its language when its type is renamed after the build.
    private static ConcurrentDictionary<Type, string> DescribedLanguages { get; } = new();

    /// <summary>
    /// The language of each set type read so far (<see cref="GetLanguageCode"/>), because filing a set and wording a
    /// text that falls back read it again.
    /// </summary>
    private static ConcurrentDictionary<Type, string> LanguagesByType { get; } = new();

    /// <summary>
    /// Counts the descriptions, so a language read while one arrived isn't kept (<see cref="GetLanguageCode"/>).
    /// </summary>
    private static int DescriptionCount;

    /// <summary>
    /// The assembly whose registrar registered the languages first, or <see langword="null"/> before that. Its texts of
    /// an enum win over those of every library (<see cref="Internals.GeneratedCode.RegisterEnumTexts{TEnum, TContract}"/>).
    /// </summary>
    internal static Assembly? ApplicationAssembly => Volatile.Read(ref Registered).Application;

    // The types of the sets this thread is creating, the outermost first, so a set that needs itself is told apart.
    [ThreadStatic]
    private static List<Type>? SetsBeingCreated;

    // So the sets being created around it hand this exception on as it is.
    [ThreadStatic]
    private static InvalidOperationException? ThrownForSetNeedingItself;

    /// <summary>
    /// Adds a set that isn't registered automatically to the sets of your whole app.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A set isn't registered automatically when:
    /// </para>
    /// <list type="bullet">
    /// <item>It's marked
    /// <see cref="ExcludeFromLocalizationDiscoveryAttribute">[ExcludeFromLocalizationDiscovery]</see>.</item>
    /// <item>It's in an assembly loaded at run time whose code nothing calls, such as a plugin's set for one of your
    /// app's languages.</item>
    /// </list>
    /// <para>
    /// For each contract and language, the set registered last this way wins whole, over every set that registered
    /// itself. See <see cref="ILocalizationSet"/>.
    /// </para>
    /// <para>
    /// A set shows for a language your app declares, and for a region of one. A set for <c>en-AU</c> shows to
    /// Australian users of an app that declares <c>en</c>, because <c>en</c> serves <c>en-AU</c>. A set for any other
    /// language never shows, because a culture outside <see cref="SupportedLanguages"/> gets the app's default language.
    /// Only <see cref="GetLocalizationSet{TContract}(CultureInfo)"/> returns such a set.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSet">The set.</typeparam>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language. The build reads it from a name that ends in the language
    /// (<c>ShopLocalizationDe</c>), of a top-level class or a class nested in a public or internal class. A class you
    /// can't rename gets a language by deriving from it with a name that gives one:
    /// <c>public class ShopLocalizationDe : ThirdPartyGermanTexts;</c>.
    /// </exception>
    public static void Register<TSet>()
        where TSet : class, ILocalizationSet, new()
    {
        Register(new TSet());
    }

    /// <summary>
    /// Adds sets you created yourself to the sets of your whole app, such as a set that needs constructor arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For each contract and language, the set registered last this way wins whole, over every set that registered
    /// itself. See <see cref="ILocalizationSet"/>. Registering a set of the same type and language again replaces the
    /// earlier one.
    /// </para>
    /// <para>
    /// A set shows for a language your app declares, and for a region of one. A set for <c>en-AU</c> shows to
    /// Australian users of an app that declares <c>en</c>, because <c>en</c> serves <c>en-AU</c>. A set for any other
    /// language never shows, because a culture outside <see cref="SupportedLanguages"/> gets the app's default language.
    /// Only <see cref="GetLocalizationSet{TContract}(CultureInfo)"/> returns such a set.
    /// </para>
    /// </remarks>
    /// <param name="sets">
    /// The sets, in the order they apply. For one contract and language, the last set wins.
    /// </param>
    /// <exception cref="ArgumentNullException">When one of the sets is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language. The build reads it from a name that ends in the language
    /// (<c>ShopLocalizationDe</c>), of a top-level class or a class nested in a public or internal class. A class you
    /// can't rename gets a language by deriving from it with a name that gives one:
    /// <c>public class ShopLocalizationDe : ThirdPartyGermanTexts;</c>.
    /// </exception>
    public static void Register(params ReadOnlySpan<ILocalizationSet> sets)
    {
        Add(sets, RegistrationTier.ByHand);
    }

    /// <summary>
    /// Adds a library's own sets that aren't registered automatically, such as a set that needs constructor arguments.
    /// They give the wording its texts show until an app rewords them. The generated code registers every other set of
    /// a library this way itself.
    /// </summary>
    /// <remarks>
    /// Every rewording wins over these whole, whichever registers first: a set of
    /// <see cref="Register(ReadOnlySpan{ILocalizationSet})"/>, and a set of another assembly that derives from one of
    /// them. Registering a set of the same type and language again replaces the earlier one.
    /// </remarks>
    /// <param name="sets">
    /// The sets, in the order they apply. For one contract and language, the last set wins.
    /// </param>
    /// <exception cref="ArgumentNullException">When one of the sets is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language. The build reads it from a name that ends in the language
    /// (<c>ShopLocalizationDe</c>), of a top-level class or a class nested in a public or internal class. A class you
    /// can't rename gets a language by deriving from it with a name that gives one:
    /// <c>public class ShopLocalizationDe : ThirdPartyGermanTexts;</c>.
    /// </exception>
    public static void RegisterLibrarySets(params ReadOnlySpan<ILocalizationSet> sets)
    {
        Add(sets, RegistrationTier.LibrarySets);
    }

    /// <summary>
    /// Sets the app's <see cref="SupportedLanguages"/>, the default script of each, and <see cref="ApplicationAssembly"/>.
    /// Only the first call counts.
    /// </summary>
    /// <param name="application">The app's assembly.</param>
    /// <param name="languages">The app's languages, the default first, each cased as culture names are.</param>
    /// <param name="scripts">
    /// The default script of each language, such as <c>Hant</c> for <c>zh-TW</c>, or <see langword="null"/> where the
    /// build knows none. In the order of <paramref name="languages"/>.
    /// </param>
    // The codes come from the generated code, which the build has checked (LOC102), so they're taken as they are.
    internal static void RegisterLanguages(Assembly application, ReadOnlySpan<string> languages, ReadOnlySpan<string?> scripts)
    {
        ArgumentNullException.ThrowIfNull(application);
        Debug.Assert(!languages.IsEmpty && languages.Length == scripts.Length);

        var appLanguages = ImmutableArray.Create(languages);
        var scriptsByLanguage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < languages.Length; i++)
            if (scripts[i] is { } script)
                scriptsByLanguage[languages[i]] = script;

        var appScripts = scriptsByLanguage.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        // The app's registrar runs first, so the first call is the app's. The app and its languages are set in one swap,
        // so two apps that start at once can't each set one. The swap gives a new registry, so a cache keyed on it drops
        // wording resolved before.
        // ReSharper disable once HeapView.CanAvoidClosure - The overload that takes the languages, a struct, is left to the JIT under ReadyToRun.
        ImmutableInterlocked.Update(
            ref Registered,
            registered => registered.Application is null ? registered.WithApplication(application, appLanguages, appScripts) : registered);
    }

    /// <summary>
    /// Stores the language the build read for a set, and the default script of that language. The same code again
    /// changes nothing, and another code is refused.
    /// </summary>
    /// <param name="script">
    /// The default script of the language, such as <c>Cyrl</c> for <c>sr-RS</c>, or <see langword="null"/> where the
    /// build knows none. A script the app's registrar gave for the language stays.
    /// </param>
    // The code comes from the generated code, which the build has checked and cased as culture names are.
    internal static void DescribeSet<TSet>(string languageCode, string? script = null)
        where TSet : class, ILocalizationSet
    {
        ArgumentNullException.ThrowIfNull(languageCode);

        if (!DescribedLanguages.TryAdd(typeof(TSet), languageCode))
        {
            // The same code again, as when the generated registration runs twice, leaves the set as it is.
            var described = DescribedLanguages[typeof(TSet)];

            if (String.Equals(described, languageCode, StringComparison.Ordinal))
                return;

            throw new ArgumentException($"'{typeof(TSet).Name}' already has the language code '{described}', so it can't take '{languageCode}'. A set's language doesn't change.", nameof(languageCode));
        }

        // Whole, because a description changes the language of the types that derive from the set too. Counted first,
        // so a read that started before this description doesn't keep its language after the clear.
        Interlocked.Increment(ref DescriptionCount);
        LanguagesByType.Clear();

        // A set registered before its description is filed under the language it inherited, so a copy rebuilds the
        // lookup tables. A copy shares every part and builds its registry at the first read, so it costs less than
        // looking for such a set, and the registration that follows a description makes a new one anyway.
        // ReSharper disable once HeapView.CanAvoidClosure - The overload that takes an argument, a struct here, is left to the JIT under ReadyToRun.
        ImmutableInterlocked.Update(ref Registered, current => script is null ? current.Copy() : current.WithScript(languageCode, script));
    }

    /// <summary>
    /// The language of a set's type, written as culture names are. It's the language the build described for the set,
    /// or for the nearest class it derives from. The registry reads no name, so a renamed class keeps its language, and
    /// no code of the set runs.
    /// </summary>
    /// <exception cref="InvalidOperationException">When neither the type nor a class it derives from is described.</exception>
    // A type that throws isn't kept, so it throws again at the next read.
    internal static string GetLanguageCode(Type setType)
    {
        if (LanguagesByType.TryGetValue(setType, out var known))
            return known;

        while (true)
        {
            var count = Volatile.Read(ref DescriptionCount);
            var language = ReadLanguageCode(setType);
            LanguagesByType[setType] = language;

            // A description that arrived meanwhile may have cleared the languages before this one was kept, and it may
            // be this type's own. So the language is read again.
            if (Volatile.Read(ref DescriptionCount) == count)
                return language;

            LanguagesByType.TryRemove(setType, out _);
        }
    }

    private static string ReadLanguageCode(Type setType)
    {
        for (var current = setType; current is not null; current = current.BaseType)
            if (DescribedLanguages.TryGetValue(current, out var described))
                return described;

        throw new InvalidOperationException(
            $"'{setType}' has no language. The build reads a set's language from its name, so end the name in the language code, as in ShopLocalizationDe. "
            + "The class must be top-level, or nested in a public or internal class, and its project must reference Idioma.");
    }

    private static void Add(ReadOnlySpan<ILocalizationSet> sets, RegistrationTier tier)
    {
        // No swap for no sets, because a new registry drops every lookup and set the old one kept.
        if (sets.IsEmpty)
            return;

        // Before the swap, so a set without a language fails at its registration, not at the first text shown afterwards.
        var entries = new Registration[sets.Length];
        for (var i = 0; i < sets.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(sets[i], nameof(sets));
            entries[i] = Registration.Of(sets[i], tier);
        }

        ImmutableInterlocked.Update(ref Registered, static (registered, entries) => registered.With(entries), entries);
    }

    /// <summary>
    /// Adds sets that the registry creates when a text of their contract is first shown, each filed under its
    /// described language without creating it.
    /// </summary>
    /// <param name="sets">Each set's type, with what creates it.</param>
    /// <param name="tier">Where the sets come from, as the build read it.</param>
    /// <exception cref="InvalidOperationException">
    /// When a set's type gives no language (<see cref="GetLanguageCode"/>).
    /// </exception>
    // Not created here, because this runs in a module initializer, before Main, where nothing can catch what a
    // constructor throws. Created at the first text, it throws where the app can log it.
    internal static void RegisterSets(ReadOnlySpan<(Type Type, Func<ILocalizationSet> Create)> sets, RegistrationTier tier)
    {
        var entries = new Registration[sets.Length];

        for (var i = 0; i < sets.Length; i++)
        {
            var (type, create) = sets[i];
            ArgumentNullException.ThrowIfNull(type, nameof(sets));
            ArgumentNullException.ThrowIfNull(create, nameof(sets));

            entries[i] = Registration.ToCreate(type, create, tier);
        }

        ImmutableInterlocked.Update(ref Registered, static (registered, entries) => registered.With(entries), entries);
    }

    /// <summary>
    /// Creates a set, and names the set if its constructor throws.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When the set's constructor throws, with that exception inside. Also when the constructor, or a field
    /// initializer, reads a text that needs the set itself, which would otherwise recurse without end.
    /// </exception>
    internal static ILocalizationSet CreateSet(Type type, Func<ILocalizationSet> create)
    {
        var beingCreated = SetsBeingCreated ??= [];

        if (beingCreated.Contains(type))
        {
            var needsItself = new InvalidOperationException($"'{type}' reads a text while it's being created, and that text needs '{type}' itself. Read the text in a member with =>, not in the constructor or a field initializer. For example: public string Title => ShopLocalization.Brand;");
            ThrownForSetNeedingItself = needsItself;
            throw needsItself;
        }

        ILocalizationSet? set;
        beingCreated.Add(type);

        try
        {
            set = create();
        }
        catch (Exception exception) when (!ReferenceEquals(exception, ThrownForSetNeedingItself))
        {
            throw new InvalidOperationException(
                $"'{type}' threw when it was created, so the texts of its contract can't be shown. The registry creates a set when a text of its contract is first shown, and tries again at the next one. See the inner exception for why it threw, and make its constructor succeed.",
                exception);
        }
        finally
        {
            beingCreated.RemoveAt(beingCreated.Count - 1);

            if (beingCreated.Count is 0)
                ThrownForSetNeedingItself = null;
        }

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract - The consumer creates the set.
        return set ?? throw new InvalidOperationException($"What creates '{type}' gave null instead of a set.");
    }
}
