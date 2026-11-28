namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationSetsAnalysis"/>
internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// The sets other than the default-language set that implement its generated contract, in any language.
    /// </summary>
    // The generated contract doesn't exist yet, so a set lists it as an unresolved name
    // (LocalizationDiscovery.FindGeneratedContract). A set derived from the default set implements it too.
    private sealed class Implementers
    {
        private ImmutableArray<DiscoveredLocalization> Sets { get; }

        /// <summary>
        /// The indexes of the sets that list each contract, in order.
        /// </summary>
        private Dictionary<string, List<int>> ByContract { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The indexes of the sets that derive from each class, in order.
        /// </summary>
        private Dictionary<string, List<int>> ByAncestor { get; } = new(StringComparer.Ordinal);

        public Implementers(ImmutableArray<DiscoveredLocalization> sets, GeneratedContracts generatedContracts)
        {
            this.Sets = sets;

            for (var index = 0; index < sets.Length; index++)
            {
                foreach (var baseType in sets[index].UnresolvedBaseTypes)
                    if (LocalizationDiscovery.FindGeneratedContract(baseType, generatedContracts) is { } contract)
                        Add(this.ByContract, contract, index);

                foreach (var ancestor in sets[index].AncestorNames)
                    Add(this.ByAncestor, ancestor, index);
            }
        }

        public DiscoveredLocalization[] Of(DiscoveredLocalization defaultSet, string contractFullName)
        {
            this.ByContract.TryGetValue(contractFullName, out var listing);
            this.ByAncestor.TryGetValue(defaultSet.MetadataName, out var deriving);

            IEnumerable<int> indexes = (listing, deriving) switch
            {
                (null, null) => [],
                (not null, null) => listing,
                (null, not null) => deriving,
                _ => listing.Union(deriving).OrderBy(static index => index),
            };

            var implementers = indexes
                .Select(index => this.Sets[index])
                .Where(other => !String.Equals(other.MetadataName, defaultSet.MetadataName, StringComparison.Ordinal))
                .ToArray();

            return implementers;
        }

        private static void Add(Dictionary<string, List<int>> indexes, string key, int index)
        {
            if (!indexes.TryGetValue(key, out var list))
                indexes.Add(key, list = []);

            // A set that lists a contract twice, itself and through a class it derives from, counts once.
            if (list.Count is 0 || list[list.Count - 1] != index)
                list.Add(index);
        }
    }
}
