namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationSetsAnalysis"/>
internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// Reports a second set of a contract for the same language in this assembly (<c>LOC203</c>), where neither derives
    /// from the other, because only one of them shows. Generated, handwritten and library contracts all count.
    /// </summary>
    /// <remarks>
    /// A rewording isn't a second set, and neither is a set that isn't registered automatically. Two rewordings of
    /// another assembly's set are the same check, reported as <c>LOC209</c> at the one that doesn't show. A rewording
    /// next to one of a referenced assembly is <c>LOC209</c> too (<see cref="FindRewordingGaps"/>).
    /// </remarks>
    private static void ReportRepeatedLanguages(
        ImmutableArray<DiscoveredLocalization> sets,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        var byContractAndLanguage = new Dictionary<(string Contract, string Language), List<DiscoveredLocalization>>();
        var reportedPairs = new HashSet<(string, string)>();

        foreach (var set in sets)
        {
            if (set is not { Language: { } language, RegistrationProblem: null })
                continue;

            foreach (var contract in GetContracts(set, generatedContracts))
            {
                var key = (contract, language.ToLowerInvariant());

                if (!byContractAndLanguage.TryGetValue(key, out var ofLanguage))
                    byContractAndLanguage.Add(key, ofLanguage = []);

                ofLanguage.Add(set);
            }
        }

        foreach (var entry in byContractAndLanguage.OrderBy(static entry => entry.Key.Contract, StringComparer.Ordinal))
        {
            var (contract, ofLanguage) = (entry.Key.Contract, entry.Value);

            if (ofLanguage.Count < 2)
                continue;

            ReportOwnRewordingsThatDontAddUp(ofLanguage, reportedPairs, findings);

            // Rewordings aren't roots.
            var names = new HashSet<string>(ofLanguage.Select(static set => set.MetadataName), StringComparer.Ordinal);
            var roots = ofLanguage.Where(set => !set.AncestorNames.Any(names.Contains) && !RewordsSetOfOtherAssembly(set)).ToList();

            if (roots.Count < 2)
                continue;

            // The set named after the contract and its language stays, and the others are reported.
            var expectedName = $"{GetContractBaseName(contract)}{SupportedLanguages.ToTypeSuffix(roots[0].Language!)}";

            var ordered = roots
                .OrderBy(set => String.Equals(set.Name, expectedName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(static set => set.MetadataName, StringComparer.Ordinal)
                .ToArray();

            foreach (var repeated in ordered.Skip(1))
            {
                // A second default-language set, which LOC200 reports already.
                if (findings.Any(finding => finding.Rule is LocalizationSetsRule.CannotExtend && finding.Location == repeated.Location))
                    continue;

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.RepeatedLanguage,
                    repeated.Location,
                    new EquatableArray<string>([repeated.Name, contract, repeated.Language!, ordered[0].Name])));
            }
        }
    }

    /// <summary>
    /// Whether a set rewords a set of another assembly in its own language, also through an abstract class of this
    /// assembly, as <c>AppEn : AppBase : Shop.ShopLocalizationEn</c> does. Two such rewordings are <c>LOC209</c>.
    /// </summary>
    private static bool RewordsSetOfOtherAssembly(DiscoveredLocalization set)
    {
        var rewords = set.Reworded is { Language: { } rewordedLanguage }
                      && String.Equals(rewordedLanguage, set.Language, StringComparison.OrdinalIgnoreCase);

        return rewords;
    }

    /// <summary>
    /// The full names of the contracts a set implements, generated ones included.
    /// </summary>
    private static IEnumerable<string> GetContracts(DiscoveredLocalization set, GeneratedContracts generatedContracts)
    {
        var generated = set.UnresolvedBaseTypes
            .Select(baseType => LocalizationDiscovery.FindGeneratedContract(baseType, generatedContracts))
            .OfType<string>();

        var contracts = set.ContractNames.Concat(set.OwnContractNames).Concat(generated).Distinct(StringComparer.Ordinal);
        return contracts;
    }
}
