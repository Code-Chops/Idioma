namespace CodeChops.Idioma.Generators;

internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// Holds the contracts written by hand in this assembly to its languages, as a generated contract is: each must have
    /// a set for every language. Reported at the attribute, like a referenced contract, since the gap is a language the
    /// attribute lists. Only contracts this assembly has a set of are seen; a contract with a set whose language the
    /// build can't read is skipped. An excluded set counts for the language it serves, but no contract is seen through
    /// it alone.
    /// </summary>
    private static void AnalyzeOwnContracts(
        ImmutableArray<DiscoveredLocalization> sets,
        ImmutableArray<DiscoveredLocalization> excluded,
        ImmutableArray<DiscoveredLocalization> unreadable,
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
            if (unreadable.Any(set => set.OwnContractNames.AsImmutableArray().Contains(contract, StringComparer.Ordinal)))
                continue;

            var members = sets.Where(set => set.OwnContractNames.AsImmutableArray().Contains(contract, StringComparer.Ordinal)).ToArray();

            var excludedMembers = excluded.Where(set => set.OwnContractNames.AsImmutableArray().Contains(contract, StringComparer.Ordinal)).ToArray();

            var separator = contract.LastIndexOf('.');
            var contractNamespace = separator < 0 ? "" : contract.Substring(startIndex: 0, separator);
            var simpleName = contract.Substring(separator + 1);
            var baseName = GetContractBaseName(contract);

            // A set reported for listing ILocalizationSet instead of this contract already says what to do about its
            // language.
            var misdeclared = sets.Where(other => other.Language is not null
                && String.Equals(other.Namespace, contractNamespace, StringComparison.Ordinal)
                && IsMissingContract(other, defaultLanguage)
                && String.Equals(LocalizationDiscovery.DeriveBaseName(other.Name, other.Language), baseName, StringComparison.Ordinal)).ToArray();

            foreach (var code in declared.Codes)
            {
                // A set of the language itself, as for a generated contract.
                if (members.Any(set => IsSetOf(set, code))
                    || excludedMembers.Any(set => IsSetOf(set, code))
                    || misdeclared.Any(set => IsSetOf(set, code)))
                {
                    continue;
                }

                // Public where one of the contract's sets is, which the new set then matches.
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
