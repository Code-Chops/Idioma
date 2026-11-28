namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationSetsAnalysis"/>
internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// Checks that each contract written by hand has a set for every language (<c>LOC201</c>), reported at the
    /// attribute. An excluded set counts for its language, but doesn't make its contract seen on its own.
    /// </summary>
    private static void AnalyzeOwnContracts(
        ImmutableArray<DiscoveredLocalization> sets,
        ImmutableArray<DiscoveredLocalization> excluded,
        DeclaredLanguages declared,
        string defaultLanguage,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        var contracts = sets
            .SelectMany(static set => set.OwnContractNames)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal);

        foreach (var contract in contracts)
        {
            var members = sets.Where(set => set.OwnContractNames.AsImmutableArray().Contains(contract, StringComparer.Ordinal)).ToArray();

            var excludedMembers = excluded.Where(set => set.OwnContractNames.AsImmutableArray().Contains(contract, StringComparer.Ordinal)).ToArray();

            var separator = contract.LastIndexOf('.');
            var contractNamespace = separator < 0 ? "" : contract.Substring(startIndex: 0, separator);
            var simpleName = contract.Substring(separator + 1);
            var baseName = GetContractBaseName(contract);

            // A set reported for its missing contract (LOC205) already says what to do about its language.
            var misdeclared = sets.Where(other => other.Language is not null
                && String.Equals(other.Namespace, contractNamespace, StringComparison.Ordinal)
                && IsMissingContract(other, defaultLanguage)
                && String.Equals(LocalizationDiscovery.DeriveBaseName(other.Name, other.Language), baseName, StringComparison.Ordinal)).ToArray();

            foreach (var code in declared.Codes)
            {
                if (members.Any(set => IsSetOf(set, code))
                    || excludedMembers.Any(set => IsSetOf(set, code))
                    || misdeclared.Any(set => IsSetOf(set, code)))
                {
                    continue;
                }

                // Public when one of the contract's sets is.
                var setName = SuggestSetName(contract, code);
                var (suggestion, newSet) = SuggestSet(setName, SupportedLanguages.SetInterface, simpleName, code, contractNamespace, members.Any(static set => set.IsPublic), members);

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.MissingLanguage,
                    declared.Location,
                    new EquatableArray<string>([contract, code, suggestion]),
                    SuggestedName: setName,
                    NewSet: newSet));
            }
        }
    }
}
