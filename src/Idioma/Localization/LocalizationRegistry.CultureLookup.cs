namespace CodeChops.Idioma;

/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// How a culture or a language code resolves in a registry: the names it falls back through, most specific first,
    /// and the sets of the registry for each name that has any. See <see cref="Resolve"/> and
    /// <see cref="ResolveLanguage"/>.
    /// </summary>
    internal sealed class CultureLookup
    {
        /// <summary>
        /// The registry this was worked out in. A lookup of another registry holds other sets.
        /// </summary>
        public LocalizationRegistry Registry { get; }

        /// <summary>
        /// The names to try, most specific first, as <see cref="CultureFallback"/> walks them.
        /// </summary>
        public string[] Names { get; }

        /// <summary>
        /// The sets of each name that has any, in the order of <see cref="Names"/>.
        /// </summary>
        public SetsOfLanguage[] Levels { get; }

        /// <summary>
        /// The culture this was worked out for, which another culture of the same name gets too. For a language code,
        /// the culture of the language, or <see langword="null"/> where this machine has none, as under invariant
        /// globalization (see <see cref="CultureName.GetCulture"/>).
        /// </summary>
        public CultureInfo? Culture { get; }

        /// <summary>
        /// The language code this was worked out for, or <see langword="null"/> for a culture.
        /// </summary>
        public string? Language { get; }

        /// <summary>
        /// Whether a text shows the culture's own wording: the registry has no app languages, or one of them serves the
        /// culture. Else the culture gets the default app language.
        /// </summary>
        public bool ShowsCulture { get; }

        /// <summary>
        /// Per contract, the sets found so far for its texts (<see cref="ResolvedSets{TContract}"/>). An object,
        /// because the contract is a type argument only where it's asked for.
        /// </summary>
        private ConcurrentDictionary<Type, object> ResolvedByContract { get; } = new();

        public CultureLookup(LocalizationRegistry registry, CultureFallback fallback, CultureInfo? culture, string? language)
        {
            var setsByLanguage = registry.SetsByLanguage;
            var names = new List<string>(capacity: 4);
            var levels = new List<SetsOfLanguage>(capacity: 4);

            foreach (var name in fallback)
            {
                names.Add(name);

                if (setsByLanguage.TryGetValue(name, out var sets))
                    levels.Add(sets);
            }

            this.Registry = registry;
            this.Names = [.. names];
            this.Levels = [.. levels];
            this.Culture = culture;
            this.Language = language;

            // By the names the lookup walks, so that this gate and the lookup agree on which language serves the culture,
            // also under invariant globalization, where en-GB (if it can be created: PredefinedCulturesOnly off) has no
            // parent culture and en still serves it.
            var appLanguages = registry.AppLanguages;
            this.ShowsCulture = appLanguages.IsDefaultOrEmpty || appLanguages.Any(this.IsServedBy);
        }

        /// <summary>
        /// Whether a language serves the culture: the culture, or one it falls back to, is that language or a region of
        /// it.
        /// </summary>
        public bool IsServedBy(string language)
        {
            foreach (var name in this.Names)
                if (IsLanguageOrRegion(name, language))
                    return true;

            return false;
        }

        /// <summary>
        /// The sets found so far for the texts of a contract, for a user of this culture. See
        /// <see cref="ResolvedSets{TContract}"/>.
        /// </summary>
        /// <param name="textCount">The number of texts the contract has.</param>
        public ResolvedSets<TContract> GetResolvedSets<TContract>(int textCount)
            where TContract : class
        {
            var resolved = this.ResolvedByContract.TryGetValue(typeof(TContract), out var known)
                ? known
                : this.ResolvedByContract.GetOrAdd(typeof(TContract), new ResolvedSets<TContract>(this, textCount));

            return (ResolvedSets<TContract>)resolved;
        }
    }
}
