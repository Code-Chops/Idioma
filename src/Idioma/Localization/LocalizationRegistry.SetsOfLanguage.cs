namespace CodeChops.Idioma;

/// <inheritdoc cref="LocalizationRegistry"/>
public sealed partial class LocalizationRegistry
{
    /// <summary>
    /// The sets of one language, and per contract the ones implementing it.
    /// </summary>
    // A language holds the sets of every contract of the app, and a text needs only those of its own: found once per
    // contract and kept, so that a lookup does not test every set of the language against the contract, on every text
    // of every render. Never dropped, as a registry never changes: a registration builds a new one. A plugin's contract
    // is the exception: kept only by a registry that holds the plugin's sets, because only that one is built anew when
    // the plugin unloads.
    internal sealed class SetsOfLanguage(ImmutableArray<Registration> registrations, bool holdsCollectibleSets)
    {
        private ImmutableArray<Registration> Registrations { get; } = registrations;

        // Whether the registry holds a plugin's sets (LocalizationRegistry.HoldsCollectibleSets). One that doesn't keeps
        // nothing for a plugin's contract, because it isn't built anew when the plugin goes.
        private bool HoldsCollectibleSets { get; } = holdsCollectibleSets;

        // Never written to once added, so an array is as safe here as an immutable one.
        private ConcurrentDictionary<Type, ContractSet[]> ByContract { get; } = new();

        /// <summary>
        /// The sets of this language that implement a contract, in the order they apply, each with the set it derives
        /// from. Empty when none implements it.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// When the constructor of such a set throws, with that exception inside. Nothing is kept then, so the next text
        /// of the contract tries again.
        /// </exception>
        public ContractSet[] Of(Type contract)
        {
            if (contract.IsCollectible && !this.HoldsCollectibleSets)
                return this.Get(contract);

            var contractSets = this.ByContract.TryGetValue(contract, out var known)
                ? known
                : this.ByContract.GetOrAdd(contract, static (contract, setsOfLanguage) => setsOfLanguage.Get(contract), this);

            return contractSets;
        }

        // Each set is created here, at the first text of its contract in this language, so a set whose constructor
        // throws fails the texts of its own contract alone.
        private ContractSet[] Get(Type contract)
        {
            var implementing = this.Registrations.Where(registration => contract.IsAssignableFrom(registration.Type)).ToImmutableArray();
            var found = new ContractSet[implementing.Length];

            for (var i = 0; i < implementing.Length; i++)
            {
                var type = implementing[i].Type;
                var basis = implementing.LastOrDefault(other => type.IsSubclassOf(other.Type));

                found[i] = new ContractSet(implementing[i].Set, basis?.Set);
            }

            return found;
        }
    }
}
