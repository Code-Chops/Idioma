namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds the sets of an assembly against its languages: what to generate, and what to report. Plain values in, plain
/// values out, so a pipeline step is reused while nothing relevant changes.
/// </summary>
internal sealed partial record LocalizationSetsAnalysis(
    EquatableArray<LocalizationContract> Contracts,
    EquatableArray<ProvidedLocalizationSet> ProvidedSets,
    EquatableArray<LocalizationSetsFinding> Findings)
{
    /// <summary>
    /// The method of an enum's generated class that gives the text of a value:
    /// <c>OrderStatusLocalization.GetTextByValue(order.Status)</c>.
    /// </summary>
    public const string LookupName = "GetTextByValue";

    /// <summary>
    /// The extension method of an enum's generated class: <c>order.Status.GetText()</c>.
    /// </summary>
    public const string ExtensionName = "GetText";

    /// <summary>
    /// The method of every generated class that gives a text by its key, or <see langword="null"/> for an unknown key.
    /// </summary>
    public const string GetName = "GetTextByKey";

    /// <summary>
    /// The property of every generated class that lists the key of every text.
    /// </summary>
    public const string KeysName = "TextKeys";

    /// <summary>
    /// Whether a text's name is taken in the generated class (<c>LOC300</c>): the class's own name, or the name of one
    /// of its members. The class declares no other members, so no other name is taken.
    /// </summary>
    /// <param name="textsName">The name of the generated class.</param>
    /// <param name="hasLookup">Whether the class gets the two methods of an enum's texts.</param>
    public static bool IsReservedTextName(string name, string textsName, bool hasLookup)
    {
        var isReserved = String.Equals(name, textsName, StringComparison.Ordinal)
                         || String.Equals(name, GetName, StringComparison.Ordinal)
                         || String.Equals(name, KeysName, StringComparison.Ordinal)
                         || (hasLookup && String.Equals(name, LookupName, StringComparison.Ordinal))
                         || (hasLookup && String.Equals(name, ExtensionName, StringComparison.Ordinal));

        return isReserved;
    }

    public static LocalizationSetsAnalysis Analyze(
        EquatableArray<DiscoveredLocalization> discovered,
        EquatableArray<UnextendableSet> unextendable,
        DeclaredLanguages? languages,
        GeneratedContracts generatedContracts,
        CancellationToken cancellationToken = default)
    {
        var candidates = LocalizationDiscovery.Merge(discovered);
        var sets = candidates.Where(static candidate => !candidate.IsExcluded).ToImmutableArray();

        // An excluded set is registered by hand, so nothing is reported about it. It still counts as a set of its
        // language, here and for referencing assemblies, so LOC201 and LOC700 don't ask for a set that exists.
        var excludedSets = candidates.Where(static candidate => candidate.IsExcluded).ToImmutableArray();
        var excluded = excludedSets.Where(static set => set.Language is not null).ToImmutableArray();

        // A set without a language (LOC207) serves none, so its contract is held to every language without it.
        var findings = ImmutableArray.CreateBuilder<LocalizationSetsFinding>();
        var contracts = ImmutableArray.CreateBuilder<LocalizationContract>();
        var provided = ImmutableArray.CreateBuilder<ProvidedLocalizationSet>();

        AddProvidedSets(sets.Concat(excludedSets), provided);

        if (languages is not { } declared)
        {
            ReportWithoutLanguages(sets, generatedContracts, findings);
        }
        else if (declared.DefaultLanguage is not { } defaultLanguage)
        {
            // The first language is unusable (LOC102). The next one doesn't take its place, because that would report
            // every set of the intended language. The contracts are still written, so code using them keeps compiling.
            AnalyzeContracts(sets, excluded, declared, generatedContracts, contracts, provided, findings: null, cancellationToken);

            ReportDerivedFromOtherLanguage(sets, generatedContracts, findings);
        }
        else
        {
            AnalyzeContracts(sets, excluded, declared, generatedContracts, contracts, provided, findings, cancellationToken);
            AnalyzeOwnContracts(sets, excluded, declared, defaultLanguage, findings);
            AnalyzeSets(sets, declared.Codes, defaultLanguage, generatedContracts, findings);
            ReportExcludedSetsOfUnlistedLanguages(excluded, declared.Codes, findings);
            ReportDefaultSetsWithoutLanguage(sets, declared, defaultLanguage, generatedContracts, findings);
            ReportDefaultSetsOfAnotherLanguage(sets, defaultLanguage, generatedContracts, findings);
            ReportDefaultSetsOfParentLanguage(sets, defaultLanguage, generatedContracts, findings);
            ReportDefaultSetsOfAnotherContract(sets, defaultLanguage, generatedContracts, findings);
            ReportUnextendableDefaultSets(unextendable, defaultLanguage, generatedContracts, findings);
            ReportSetsOfContractWithoutDefaultSet(sets, defaultLanguage, generatedContracts, findings);
        }

        // Needs no language list: an app adding a language to a library's contract may declare none.
        ReportRepeatedLanguages(sets, generatedContracts, findings);

        var analysis = new LocalizationSetsAnalysis(
            new EquatableArray<LocalizationContract>(contracts.ToImmutable()),
            new EquatableArray<ProvidedLocalizationSet>(provided.Distinct().OrderBy(static set => set.Contract, StringComparer.Ordinal).ThenBy(static set => set.Language, StringComparer.Ordinal).ThenBy(static set => set.Set, StringComparer.Ordinal).ToImmutableArray()),
            new EquatableArray<LocalizationSetsFinding>(findings.ToImmutable()));

        return analysis;
    }

    /// <summary>
    /// Lists every set with a language for each public contract it implements.
    /// </summary>
    private static void AddProvidedSets(IEnumerable<DiscoveredLocalization> sets, ImmutableArray<ProvidedLocalizationSet>.Builder provided)
    {
        foreach (var set in sets)
        {
            if (set.Language is null)
                continue;

            foreach (var contract in set.ContractNames)
                provided.Add(ProvidedLocalizationSet.Of(contract, set));
        }
    }

    /// <summary>
    /// Reports, in an assembly that declares no languages, a set that would start a contract (<c>LOC100</c>) and a set
    /// derived from another language (<c>LOC204</c>).
    /// </summary>
    private static void ReportWithoutLanguages(
        ImmutableArray<DiscoveredLocalization> sets,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
                // Without a default language, there's no telling which set the contract comes from.
                if (set.ListsLocalizationSet && !LocalizationDiscovery.ImplementsContract(set) && !LocalizationDiscovery.RewordsSet(set))
                    findings.Add(new LocalizationSetsFinding(LocalizationSetsRule.NoLanguagesDeclared, set.Location, new EquatableArray<string>([$"'{set.Name}' is a localization set"])));
                // Needs no language list, because an app rewording a library's set could otherwise serve another
                // language silently.
                else if (set.Language is { } language && IsDerivedFromOtherLanguage(set, generatedContracts))
                    findings.Add(DerivedFromOtherLanguage(set, language));
        }
    }

    /// <summary>
    /// Reports every set derived from a set of another language (<c>LOC204</c>).
    /// </summary>
    private static void ReportDerivedFromOtherLanguage(
        ImmutableArray<DiscoveredLocalization> sets,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
            if (set.Language is { } language && IsDerivedFromOtherLanguage(set, generatedContracts))
                findings.Add(DerivedFromOtherLanguage(set, language));
    }
}
