using System.Reflection;

namespace CodeChops.Idioma;

/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// A registered set, with the language it had when it registered. A set the registry creates itself is created
    /// when a text of its contract is first shown.
    /// </summary>
    // A class, for the reason ContractSet is one. As a tuple, it left nine methods of ImmutableArray to the JIT at the
    // start of every app.
    internal sealed class Registration
    {
        private readonly Lazy<ILocalizationSet> _set;

        public Type Type { get; }

        /// <summary>
        /// The language the set registered with.
        /// </summary>
        public string Language { get; }

        /// <summary>
        /// The set, which is created at the first read. A set whose constructor threw is created again at the next read,
        /// so a constructor that failed once, such as on a setting that wasn't loaded yet, doesn't fail its texts for
        /// good.
        /// </summary>
        public ILocalizationSet Set => this._set.Value;

        /// <summary>
        /// Whether the set exists: one registered as an instance, or one the registry created already.
        /// </summary>
        public bool IsCreated => this._set.IsValueCreated;

        private Registration(Type type, Lazy<ILocalizationSet> set, string language)
        {
            this.Type = type;
            this._set = set;
            this.Language = language;
        }

        public static Registration Of(ILocalizationSet set)
        {
            var registration = new Registration(set.GetType(), new Lazy<ILocalizationSet>(set), ReadLanguageCode(set));
            return registration;
        }

        // PublicationOnly keeps no exception, so a failed constructor runs again at the next read. Two threads may each
        // create the set, and the first one to finish gives every text its instance, as for the set of a default
        // language (GeneratedCode.DefaultSet).
        public static Registration ToCreate(Type type, Func<ILocalizationSet> create, string language)
        {
            var registration = new Registration(type, new Lazy<ILocalizationSet>(() => CreateSet(type, create), LazyThreadSafetyMode.PublicationOnly), language);
            return registration;
        }
    }

    /// <summary>
    /// The registered sets, and the app with its languages, with the registry built from them. It never changes, so a
    /// caller always sees sets, an app and languages that belong together, and its registry holds exactly these.
    /// </summary>
    private sealed class Registrations(
        ImmutableArray<Registration> defaults,
        ImmutableArray<Registration> overrides,
        ImmutableArray<string> languages,
        Assembly? application)
    {
        // Each set with the language it had when it registered, so that a registration can replace an earlier one of
        // the same type and language without calling into the sets, which are consumer code.
        private ImmutableArray<Registration> Defaults { get; } = defaults;
        private ImmutableArray<Registration> Overrides { get; } = overrides;

        /// <summary>
        /// The app's languages, the default first. Empty until the app's registrar gives them.
        /// </summary>
        public ImmutableArray<string> Languages { get; } = languages;

        /// <summary>
        /// The assembly of the app, whose registrar gave the <see cref="Languages"/>, or <see langword="null"/> where
        /// none did.
        /// </summary>
        public Assembly? Application { get; } = application;

        /// <summary>
        /// The registry of these sets and languages. Built at the first read, and the same instance from then on.
        /// </summary>
        public LocalizationRegistry Registry => this._registry ?? this.CreateRegistry();
        private LocalizationRegistry? _registry;

        /// <summary>
        /// These registrations with sets added to the library's own sets or to the sets that reword them.
        /// </summary>
        public Registrations With(Registration[] entries, bool asDefaults)
        {
            var tier = asDefaults ? this.Defaults : this.Overrides;

            // The last registration of each type and language, in the order they came. So a set registered again moves
            // to the end and wins, and registering again (a second service provider, a test) can't grow the list without
            // bound. Matched on the language each set registered with: a set whose code was described only after it
            // registered (DescribeSet) is kept once next to a later registration of its type, and no more than once.
            // Walked once, from the end: removing each entry from the tier by itself took the square of the number of
            // sets, 22 ms for 2,000 of them (measured 2026-11-19).
            var kept = new Registration[tier.Length + entries.Length];
            var start = kept.Length;
            var seen = new HashSet<Registration>(kept.Length, SameTypeAndLanguage.Instance);

            for (var i = entries.Length - 1; i >= 0; i--)
                if (seen.Add(entries[i]))
                    kept[--start] = entries[i];

            for (var i = tier.Length - 1; i >= 0; i--)
                if (seen.Add(tier[i]))
                    kept[--start] = tier[i];

            tier = ImmutableArray.Create(kept, start, kept.Length - start);

            var registrations = asDefaults
                ? new Registrations(tier, this.Overrides, this.Languages, this.Application)
                : new Registrations(this.Defaults, tier, this.Languages, this.Application);

            return registrations;
        }

        /// <summary>
        /// These registrations with the app and its languages.
        /// </summary>
        public Registrations WithApplication(Assembly application, ImmutableArray<string> appLanguages)
        {
            var registrations = new Registrations(this.Defaults, this.Overrides, appLanguages, application);
            return registrations;
        }

        /// <summary>
        /// The same sets, app and languages, with a registry that is yet to be built.
        /// </summary>
        public Registrations Copy()
        {
            var copy = new Registrations(this.Defaults, this.Overrides, this.Languages, this.Application);
            return copy;
        }

        private LocalizationRegistry CreateRegistry()
        {
            // Building reads the language code of each set that exists, which is consumer code. A code that reads a
            // text, or the registry, asks for the registry being built, which built it again, without end, until the
            // stack overflowed and the process died. Thrown instead, the build files that set under the language it
            // registered with (GetFilingLanguage).
            if (IsBuildingRegistry)
                throw new InvalidOperationException(
                    $"A set's {nameof(ILocalizationSet)}.{nameof(ILocalizationSet.LocalizationLanguageCode)} reads a text or the registry, which needs the sets of the app that are being built from that code. "
                    + "Return a culture name without reading a text, such as en-GB.");

            var registrations = this.Defaults.AddRange(this.Overrides);
            LocalizationRegistry created;

            // Nothing is held meanwhile, so two threads may build at once. The first one done gives the registry every
            // caller gets, so that a cache a consumer keys on the instance sees one.
            IsBuildingRegistry = true;

            try
            {
                created = Create(registrations, this.Languages);
            }
            finally
            {
                IsBuildingRegistry = false;
            }

            return Interlocked.CompareExchange(ref this._registry, created, comparand: null) ?? created;
        }

        /// <summary>
        /// Whether this thread builds a registry (<see cref="CreateRegistry"/>). Per thread, because a build runs on one,
        /// and another thread may build at the same time.
        /// </summary>
        [ThreadStatic]
        private static bool IsBuildingRegistry;

        /// <summary>
        /// Tells registrations of the same type and language apart from the others, the language in any letter case.
        /// </summary>
        private sealed class SameTypeAndLanguage : IEqualityComparer<Registration>
        {
            public static SameTypeAndLanguage Instance { get; } = new();

            public bool Equals(Registration? x, Registration? y)
            {
                var areSame = x is not null
                              && y is not null
                              && x.Type == y.Type
                              && String.Equals(x.Language, y.Language, StringComparison.OrdinalIgnoreCase);

                return areSame;
            }

            public int GetHashCode(Registration registration)
            {
                var hash = (registration.Type.GetHashCode() * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(registration.Language);
                return hash;
            }
        }
    }
}
