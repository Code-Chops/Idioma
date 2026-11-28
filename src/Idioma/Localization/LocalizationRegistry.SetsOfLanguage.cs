namespace CodeChops.Idioma;

/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// The sets of one language, and per contract the one that wins.
    /// </summary>
    // Found once per contract and kept, so a lookup doesn't test every set of the language on every text. Never dropped,
    // because a registry never changes.
    internal sealed class SetsOfLanguage(ImmutableArray<Registration> registrations)
    {
        /// <summary>
        /// In order of precedence, so the last one wins.
        /// </summary>
        private ImmutableArray<Registration> Registrations { get; } = registrations;

        // Null for a contract that no set of this language implements. Never written to once added.
        private ConcurrentDictionary<Type, ILocalizationSet?> ByContract { get; } = new();

        /// <summary>
        /// The set of this language that words every text of a contract. It's the last that implements it, so a
        /// rewording wins whole.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// When the constructor of that set throws, with that exception inside. Nothing is kept then, so the next text
        /// tries again.
        /// </exception>
        public ILocalizationSet? Of(Type contract)
        {
            var set = this.ByContract.TryGetValue(contract, out var known)
                ? known
                : this.ByContract.GetOrAdd(contract, static (contract, setsOfLanguage) => setsOfLanguage.Get(contract), this);

            return set;
        }

        // Created here, so a constructor that throws fails only its own contract's texts, and the sets it wins over are
        // never created.
        private ILocalizationSet? Get(Type contract)
        {
            var registrations = this.Registrations;

            for (var i = registrations.Length - 1; i >= 0; i--)
                if (contract.IsAssignableFrom(registrations[i].Type))
                    return registrations[i].Set;

            return null;
        }
    }
}
