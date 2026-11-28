namespace CodeChops.Idioma;

/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// How a culture or a language code resolves in a registry: the names it falls back through, most specific first,
    /// and the sets for each name that has any. See <see cref="Resolve"/> and <see cref="ResolveLanguage"/>.
    /// </summary>
    internal sealed class CultureLookup
    {
        public LocalizationRegistry Registry { get; }

        /// <summary>
        /// As <see cref="CultureFallback"/> walks them.
        /// </summary>
        public string[] Names { get; }

        /// <summary>
        /// The sets of each name that has any, in the order of <see cref="Names"/>.
        /// </summary>
        public SetsOfLanguage[] Levels { get; }

        /// <summary>
        /// For a language code, the culture of the language, or <see langword="null"/> where this machine has none.
        /// </summary>
        public CultureInfo? Culture { get; }

        /// <summary>
        /// <see langword="null"/> for a culture.
        /// </summary>
        public string? Language { get; }

        /// <summary>
        /// Whether a text shows the culture's own wording, because the registry has no app languages or one serves the
        /// culture. Else the culture gets the default app language.
        /// </summary>
        public bool ShowsCulture { get; }

        /// <summary>
        /// Per contract, its <see cref="ResolvedSets{TContract}"/>. An object, because the contract is a type argument
        /// only where it's asked for.
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

            // By the names the lookup walks, so this and the lookup agree on which language serves the culture, also
            // under invariant globalization.
            var appLanguages = registry.AppLanguages;
            this.ShowsCulture = appLanguages.IsDefaultOrEmpty || appLanguages.Any(this.IsServedBy);
        }

        public bool IsServedBy(string language)
        {
            foreach (var name in this.Names)
                if (IsLanguageOrRegion(name, language))
                    return true;

            return false;
        }

        public ResolvedSets<TContract> GetResolvedSets<TContract>()
            where TContract : class
        {
            var resolved = this.ResolvedByContract.TryGetValue(typeof(TContract), out var known)
                ? known
                : this.ResolvedByContract.GetOrAdd(typeof(TContract), new ResolvedSets<TContract>(this));

            return (ResolvedSets<TContract>)resolved;
        }
    }
}
