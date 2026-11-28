using System.Reflection;

namespace CodeChops.Idioma;

/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// A registered set, with the tier it registered in.
    /// </summary>
    // A class, not a struct or tuple, because ReadyToRun can't precompile the framework's generic types for a struct of
    // this library.
    internal sealed class Registration
    {
        private readonly Lazy<ILocalizationSet> _set;

        public Type Type { get; }

        public RegistrationTier Tier { get; }

        /// <summary>
        /// Created at the first read. A constructor that threw runs again at the next read, so a passing failure, such
        /// as a setting that wasn't loaded yet, doesn't fail its texts for good.
        /// </summary>
        public ILocalizationSet Set => this._set.Value;

        // The language is read here, so a set without one fails at its registration, not at the first text shown.
        private Registration(Type type, Lazy<ILocalizationSet> set, RegistrationTier tier)
        {
            _ = GetLanguageCode(type);

            this.Type = type;
            this._set = set;
            this.Tier = tier;
        }

        public static Registration Of(ILocalizationSet set, RegistrationTier tier)
        {
            var registration = new Registration(set.GetType(), new Lazy<ILocalizationSet>(set), tier);
            return registration;
        }

        // PublicationOnly keeps no exception, so a failed constructor runs again. Where two threads race, the first to
        // finish wins.
        public static Registration ToCreate(Type type, Func<ILocalizationSet> create, RegistrationTier tier)
        {
            var registration = new Registration(type, new Lazy<ILocalizationSet>(() => CreateSet(type, create), LazyThreadSafetyMode.PublicationOnly), tier);
            return registration;
        }
    }

    /// <summary>
    /// Where a set comes from. A later tier wins over an earlier one for the same contract and language.
    /// </summary>
    internal enum RegistrationTier
    {
        /// <summary>
        /// A library's own sets: those the generated code registers next to their contract, and those of
        /// <see cref="RegisterLibrarySets"/>.
        /// </summary>
        LibrarySets,

        /// <summary>
        /// A library's rewordings of another assembly's set.
        /// </summary>
        Rewordings,

        /// <summary>
        /// The app's rewordings of another assembly's set, which win over a library's.
        /// </summary>
        ApplicationRewordings,

        /// <summary>
        /// The sets of <see cref="Register(ReadOnlySpan{ILocalizationSet})"/>.
        /// </summary>
        ByHand,
    }

    /// <summary>
    /// The registered sets and the app with its languages, with the registry built from them. It never changes, so a
    /// caller always sees parts that belong together.
    /// </summary>
    private sealed class Registrations(ImmutableArray<Registration> all, ImmutableArray<string> languages,
        FrozenDictionary<string, string> scripts, Assembly? application)
    {
        public static Registrations Empty { get; } = new([], [], FrozenDictionary<string, string>.Empty, application: null);

        /// <summary>
        /// The app's languages, the default first. Empty until the app's registrar gives them.
        /// </summary>
        public ImmutableArray<string> Languages { get; } = languages;

        public Assembly? Application { get; } = application;

        /// <summary>
        /// Every registered set, in the order it registered.
        /// </summary>
        private ImmutableArray<Registration> All { get; } = all;

        /// <summary>
        /// The default script of each language the build knows one for, such as <c>Hant</c> for <c>zh-TW</c>. The
        /// languages are <see cref="Languages"/> and those of the described sets, such as a regional set of a language
        /// the app declares.
        /// </summary>
        private FrozenDictionary<string, string> Scripts { get; } = scripts;

        /// <summary>
        /// Built at the first read, and the same instance from then on.
        /// </summary>
        public LocalizationRegistry Registry => this._registry ?? this.CreateRegistry();
        private LocalizationRegistry? _registry;

        public Registrations With(Registration[] entries)
        {
            // The last registration of each type and tier, in the order they came, so a set registered again moves to
            // the end and wins, and repeated registrations can't grow the list. Walked once from the end, so it stays
            // linear.
            var existing = this.All;
            var kept = new Registration[existing.Length + entries.Length];
            var start = kept.Length;
            var seen = new HashSet<(Type, int)>(kept.Length);

            for (var i = entries.Length - 1; i >= 0; i--)
                if (seen.Add((entries[i].Type, (int)entries[i].Tier)))
                    kept[--start] = entries[i];

            for (var i = existing.Length - 1; i >= 0; i--)
                if (seen.Add((existing[i].Type, (int)existing[i].Tier)))
                    kept[--start] = existing[i];

            var registrations = new Registrations(ImmutableArray.Create(kept, start, kept.Length - start), this.Languages, this.Scripts, this.Application);
            return registrations;
        }

        public Registrations WithApplication(Assembly application, ImmutableArray<string> appLanguages, FrozenDictionary<string, string> appScripts)
        {
            // A library's sets may be described before the app's registrar runs, so their scripts stay.
            var scripts = appScripts;

            if (this.Scripts.Count > 0)
            {
                var merged = new Dictionary<string, string>(this.Scripts, StringComparer.OrdinalIgnoreCase);

                foreach (var (language, script) in appScripts)
                    merged[language] = script;

                scripts = merged.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            }

            var registrations = new Registrations(this.All, appLanguages, scripts, application);
            return registrations;
        }

        /// <summary>
        /// With the default script of a described set's language. A script the language already has stays.
        /// </summary>
        public Registrations WithScript(string language, string script)
        {
            if (this.Scripts.ContainsKey(language))
                return this.Copy();

            var scripts = new Dictionary<string, string>(this.Scripts, StringComparer.OrdinalIgnoreCase) { [language] = script };
            var registrations = new Registrations(this.All, this.Languages, scripts.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase), this.Application);

            return registrations;
        }

        /// <summary>
        /// The same parts, with a registry that's yet to be built.
        /// </summary>
        public Registrations Copy()
        {
            var copy = new Registrations(this.All, this.Languages, this.Scripts, this.Application);
            return copy;
        }

        private LocalizationRegistry CreateRegistry()
        {
            // By tier, so the last of a contract and language wins whatever order the assemblies loaded in. Within a tier,
            // in the order they registered, because OrderBy is a stable sort. By int, because ReadyToRun can't precompile
            // the framework's generic types for an enum of this library.
            ImmutableArray<Registration> registrations = [.. this.All.OrderBy(static registration => (int)registration.Tier)];

            // Two threads may build at once. The first one done wins, so a cache keyed on the instance sees one.
            var created = Create(registrations, this.Languages, this.Scripts);

            return Interlocked.CompareExchange(ref this._registry, created, comparand: null) ?? created;
        }
    }
}
