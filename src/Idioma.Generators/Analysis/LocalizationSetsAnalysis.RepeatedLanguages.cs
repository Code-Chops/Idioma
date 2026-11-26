namespace CodeChops.Idioma.Generators;

internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// Reports a set of a contract for a language that another set of that contract in this assembly serves already
    /// (<c>LOC209</c>), where neither derives from the other. Only one of them shows, and which one depends on the order
    /// of their names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every contract counts: one this build generates, one written by hand, and a library's. A set that derives from
    /// another of the same language rewords it, and wins over it whatever the order, so it's no second set. Nor is a set
    /// registered by hand, or one that can't be registered automatically, because neither competes at startup.
    /// </para>
    /// <para>
    /// A set that derives from a set of its language in another assembly, such as a library's, rewords that set too. The
    /// registry shows each of several such rewordings for the texts it words itself. So two of them are reported only
    /// where both word one text, of which users see only one wording.
    /// </para>
    /// </remarks>
    private static void ReportRepeatedLanguages(
        ImmutableArray<DiscoveredLocalization> sets,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        var byContractAndLanguage = new Dictionary<(string Contract, string Language), List<DiscoveredLocalization>>();

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

            // A set deriving from another of the group rewords that set, and so does one deriving from a set of its
            // language in another assembly.
            var names = new HashSet<string>(ofLanguage.Select(static set => set.MetadataName), StringComparer.Ordinal);
            var rewordings = ofLanguage.Where(set => !set.AncestorNames.Any(names.Contains) && RewordsSetOfOtherAssembly(set)).ToList();
            var roots = ofLanguage.Where(set => !set.AncestorNames.Any(names.Contains) && !RewordsSetOfOtherAssembly(set)).ToList();

            ReportRepeatedRewordings(contract, rewordings, findings);

            if (roots.Count < 2)
                continue;

            // The set named after the contract and its language is the one that stays. The others are reported.
            var contractName = contract.Substring(contract.LastIndexOf('.') + 1);
            var expectedName = contractName.Length > 1 ? $"{contractName.Substring(1)}{SupportedLanguages.ToTypeSuffix(roots[0].Language!)}" : null;

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
    /// Whether a set derives directly from a set of its own language in another assembly, which it rewords. Only a class
    /// of another assembly leaves the classes of this one a set derives from (<see cref="DiscoveredLocalization.AncestorNames"/>)
    /// empty, where the set it derives from (<see cref="DiscoveredLocalization.BaseSet"/>) is known.
    /// </summary>
    private static bool RewordsSetOfOtherAssembly(DiscoveredLocalization set)
    {
        var rewords = set.AncestorNames.Length is 0
                      && set.BaseSet is { Language: { } baseLanguage }
                      && String.Equals(baseLanguage, set.Language, StringComparison.OrdinalIgnoreCase);

        return rewords;
    }

    /// <summary>
    /// Reports each rewording of a set of another assembly that words a text an earlier rewording of the group words
    /// too (<c>LOC209</c>), in the order of their names. Users see the wording of only one of them for that text.
    /// </summary>
    private static void ReportRepeatedRewordings(string contract, List<DiscoveredLocalization> rewordings,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        var ordered = rewordings.OrderBy(static set => set.MetadataName, StringComparer.Ordinal).ToArray();

        for (var i = 1; i < ordered.Length; i++)
        {
            var repeated = ordered[i];
            var texts = new HashSet<string>(repeated.OfContract.DeclaredTexts, StringComparer.Ordinal);

            for (var j = 0; j < i; j++)
            {
                if (!ordered[j].OfContract.DeclaredTexts.Any(texts.Contains))
                    continue;

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.RepeatedLanguage,
                    repeated.Location,
                    new EquatableArray<string>([repeated.Name, contract, repeated.Language!, ordered[j].Name])));

                break;
            }
        }
    }

    /// <summary>
    /// The full names of the contracts a set implements, itself or through a class it derives from: those that exist
    /// in the compilation, and those this build generates.
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
