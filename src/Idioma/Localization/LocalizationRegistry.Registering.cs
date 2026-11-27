using System.Reflection;

namespace CodeChops.Idioma;

// The part of LocalizationRegistry that keeps what is registered: the sets of the whole app, the app's languages, and
// how the build described each set. A plain comment: the compiler merges the XML docs of every partial
// declaration, and the type's summary lives in LocalizationRegistry.cs.
//
// Nothing here takes a lock. The registrations are one object that never changes, and a registration swaps it for a
// new one in one step. The registry of the whole app is built from it at the first read.
/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// What <see cref="Current"/> is built from: every registered set, and the app's languages.
    /// </summary>
    // A field, because the swap takes it by reference. A swap that needs a value captures it, rather than handing it to
    // the overload that takes an argument. That overload is generic over the argument's type, and for a struct,
    // ReadyToRun can't compile it, so every app would JIT it at its start.
    private static Registrations Registered = new(defaults: [], overrides: [], languages: [], application: null);

    /// <summary>
    /// How the source generator described the sets it found, per type: the language the build read from its name,
    /// and, for a set deriving from another, the texts its own class words. Lock-free to read, because every
    /// LocalizationLanguageCode a set doesn't implement reads it, and so does every lookup in a set that derives from
    /// another.
    /// </summary>
    // The language, so that a set keeps it when its type is renamed after the build, and a set named after a longer
    // code its assembly declares, such as ShopLocalizationEnGb for en-GB, isn't read by its last two letters. The texts,
    // so that the registry knows which texts a set rewords without calling it (GetWordingSet).
    private static ConcurrentDictionary<Type, SetDescription> SetDescriptions { get; } = new();

    /// <summary>
    /// The assembly of the app, whose registrar registered the languages first (<see cref="RegisterLanguages"/>), or
    /// <see langword="null"/> before that. Its texts of an enum win over those of every library
    /// (<see cref="Internals.GeneratedCode.RegisterEnumTexts{TEnum, TContract}"/>).
    /// </summary>
    internal static Assembly? ApplicationAssembly => Volatile.Read(ref Registered).Application;

    // The types of the sets this thread is creating, the outermost first, or null on a thread that has created none.
    // A constructor runs on one thread, so the types tell a set that needs itself while it's created. Read only where a
    // set is created, so no text pays for it.
    [ThreadStatic]
    private static List<Type>? SetsBeingCreated;

    // The exception this thread threw last for a set that needs itself, so that the sets being created around it hand
    // it on as it is.
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
    /// The set wins over the sets registered before it, for every text it changes. See <see cref="ILocalizationSet"/>.
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
    /// When a set has no language code, or one that isn't a culture name. Name the set after its language
    /// (<c>ShopLocalizationDe</c>), or have its <see cref="ILocalizationSet.LocalizationLanguageCode"/> return a
    /// culture name such as <c>en-GB</c>.
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
    /// Each set wins over the sets registered before it, for every text it changes. See <see cref="ILocalizationSet"/>.
    /// Registering a set of the same type and language again replaces the earlier one.
    /// </para>
    /// <para>
    /// A set shows for a language your app declares, and for a region of one. A set for <c>en-AU</c> shows to
    /// Australian users of an app that declares <c>en</c>, because <c>en</c> serves <c>en-AU</c>. A set for any other
    /// language never shows, because a culture outside <see cref="SupportedLanguages"/> gets the app's default language.
    /// Only <see cref="GetLocalizationSet{TContract}(CultureInfo)"/> returns such a set.
    /// </para>
    /// </remarks>
    /// <param name="sets">
    /// The sets, in the order they apply. For one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <exception cref="ArgumentNullException">When one of the sets is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that isn't a culture name. Name the set after its language
    /// (<c>ShopLocalizationDe</c>), or have its <see cref="ILocalizationSet.LocalizationLanguageCode"/> return a
    /// culture name such as <c>en-GB</c>.
    /// </exception>
    public static void Register(params ReadOnlySpan<ILocalizationSet> sets)
    {
        Add(sets, asDefaults: false);
    }

    /// <summary>
    /// Adds a library's own sets that aren't registered automatically, such as a set that needs constructor arguments.
    /// They give the wording its texts show until an app rewords them. The generated code registers every other set of
    /// a library this way itself.
    /// </summary>
    /// <remarks>
    /// A set from <see cref="Register(ReadOnlySpan{ILocalizationSet})"/> wins over these for every text it changes,
    /// whichever registers first. Registering a set of the same type and language again replaces the earlier one.
    /// </remarks>
    /// <param name="sets">
    /// The sets, in the order they apply. For one contract and language, a later set wins for every text it changes.
    /// </param>
    /// <exception cref="ArgumentNullException">When one of the sets is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language code, or one that isn't a culture name. Name the set after its language
    /// (<c>ShopLocalizationDe</c>), or have its <see cref="ILocalizationSet.LocalizationLanguageCode"/> return a
    /// culture name such as <c>en-GB</c>.
    /// </exception>
    public static void RegisterLibrarySets(params ReadOnlySpan<ILocalizationSet> sets)
    {
        Add(sets, asDefaults: true);
    }

    /// <summary>
    /// Sets the app's <see cref="SupportedLanguages"/>, for <see cref="Internals.GeneratedCode.RegisterLanguages"/>, and
    /// stores the <paramref name="application"/> they come from as the app (<see cref="ApplicationAssembly"/>). Only
    /// the first call counts.
    /// </summary>
    internal static void RegisterLanguages(Assembly application, string defaultLanguage, params ReadOnlySpan<string> otherLanguages)
    {
        ArgumentNullException.ThrowIfNull(application);

        // Checked as a registry of your own checks its languages, and here rather than at the first text shown.
        var languages = ToCultureNames(defaultLanguage, otherLanguages);

        // The app's own registrar runs before any assembly it loads, and calls this before the registrars of the
        // assemblies it references, so the first call is the app's. The app and its languages are set in one swap, so
        // two apps that start at once can't each set one of them. The texts of an enum chosen before are chosen again
        // with the app's first (GeneratedCode.GetEnumText). The swap gives a new registry instance, so that a cache a
        // consumer keys on it drops wording resolved before.
        // ReSharper disable once HeapView.CanAvoidClosure - The overload that takes the languages, a struct, is left to the JIT under ReadyToRun.
        ImmutableInterlocked.Update(
            ref Registered,
            registered => registered.Application is null ? registered.WithApplication(application, languages) : registered);
    }

    /// <summary>
    /// Stores what the build read about a set, for <see cref="Internals.GeneratedCode.DescribeSet"/>: its language, or
    /// <see langword="null"/> for a set whose <see cref="ILocalizationSet.LocalizationLanguageCode"/> the build couldn't
    /// read, and the texts its own class words. A set has one code. Describing it with the same code again changes nothing, and
    /// another code is refused.
    /// </summary>
    internal static void DescribeSet<TSet>(string? languageCode, string[] ownTexts)
        where TSet : class, ILocalizationSet
    {
        ArgumentNullException.ThrowIfNull(ownTexts);

        // Checked here: a code that is no culture name would otherwise fail every text of the app once the lookup
        // tables are built again, far from this call.
        if (languageCode is not null)
            languageCode = ToCultureName(languageCode, nameof(languageCode));

        // A set registered before its description is filed under the language of its name, and words every text itself, until
        // the lookup tables are built again. So the registrations are swapped for a copy, which builds them anew.
        var description = new SetDescription(languageCode, ownTexts);
        var existing = SetDescriptions.GetOrAdd(typeof(TSet), description);

        if (!ReferenceEquals(existing, description))
        {
            // The same code again, as when the generated registration runs twice, leaves the set as it is.
            var described = existing.LanguageCode;

            if (String.Equals(described, languageCode, StringComparison.Ordinal))
                return;

            throw new ArgumentException($"'{typeof(TSet).Name}' already has the language code '{described ?? "null"}', so it can't take '{languageCode ?? "null"}'. A set's language doesn't change.", nameof(languageCode));
        }

        ImmutableInterlocked.Update(ref Registered, static registered => registered.Copy());
    }

    /// <summary>
    /// The language code a set's type is described with, or else for the nearest class it derives from that has one. So a
    /// rewording of a British set serves British English too. <see langword="null"/> when none has one.
    /// </summary>
    internal static string? GetDescribedLanguageCode(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (SetDescriptions.TryGetValue(current, out var description) && description.LanguageCode is { } languageCode)
                return languageCode;

        return null;
    }

    /// <summary>
    /// Adds sets that were created already, for <see cref="Register(ReadOnlySpan{ILocalizationSet})"/> and
    /// <see cref="RegisterLibrarySets"/>. With <paramref name="asDefaults"/>, they give a library's own wording, as the
    /// sets the generated code registers through <see cref="Internals.GeneratedCode.RegisterSets"/> do. Every other set
    /// wins over those.
    /// </summary>
    private static void Add(ReadOnlySpan<ILocalizationSet> sets, bool asDefaults)
    {
        // No swap for no sets, because a new registry drops every lookup and set the old one kept.
        if (sets.IsEmpty)
            return;

        // Read here so that a set without a language code fails at its registration, not at the first text rendered
        // afterwards. And read once, before the swap: a language code is consumer code, and the swap may run again.
        var entries = new Registration[sets.Length];
        for (var i = 0; i < sets.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(sets[i], nameof(sets));
            entries[i] = Registration.Of(sets[i]);
        }

        // ReSharper disable once HeapView.CanAvoidClosure - The overload that takes both values, as a tuple, is left to the JIT under ReadyToRun.
        ImmutableInterlocked.Update(ref Registered, registered => registered.With(entries, asDefaults));
    }

    /// <summary>
    /// Adds sets that the registry creates when a text of their contract is first shown, for
    /// <see cref="Internals.GeneratedCode.RegisterSets"/>. Each is filed under the language the build described its
    /// type (<see cref="DescribeSet"/>), without creating it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When a set has no language, because the build described it with none and its name gives none.
    /// </exception>
    // Not created here, because this runs in a module initializer, before Main. A set whose constructor throws there
    // takes the app down where nothing can catch it, also an app that never shows the set's texts. Created when the
    // first text of its contract is shown, it throws from there, where the app can log it, and only for the texts of
    // its contract.
    //
    // The build registers only a set whose language it describes (LOC208), so the registry never creates a set to file
    // it. A set without a description, which only code written by hand registers here, takes the language its name gives, as
    // its LocalizationLanguageCode does.
    internal static void RegisterSets(ReadOnlySpan<(Type Type, Func<ILocalizationSet> Create)> sets, bool asDefaults)
    {
        var entries = new Registration[sets.Length];
        for (var i = 0; i < sets.Length; i++)
        {
            var (type, create) = sets[i];
            ArgumentNullException.ThrowIfNull(type, nameof(sets));
            ArgumentNullException.ThrowIfNull(create, nameof(sets));

            entries[i] = Registration.ToCreate(type, create, ILocalizationSet.GetLanguageCode(type));
        }

        // ReSharper disable once HeapView.CanAvoidClosure - The overload that takes both values, as a tuple, is left to the JIT under ReadyToRun.
        ImmutableInterlocked.Update(ref Registered, registered => registered.With(entries, asDefaults));
    }

    /// <summary>
    /// Creates a set the registry registered by its type, and says which set it was if its constructor throws.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When the set's constructor throws, with that exception inside. Also when the constructor, or a field
    /// initializer, reads a text that needs the set itself.
    /// </exception>
    // A set that reads a text of its own contract as it's created needs itself for that text. Nothing is kept until a
    // set exists, so the read created the set again, without end, until the stack overflowed and the process died.
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
