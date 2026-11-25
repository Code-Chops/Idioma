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
    /// The name of the method the generated class of a set of the texts of an enum gets, which gives the text of a
    /// value: <c>OrderStatusLocalization.GetTextByValue(order.Status)</c>.
    /// </summary>
    public const string LookupName = "GetTextByValue";

    /// <summary>
    /// The name of the extension method the generated class of a set of the texts of an enum gets, which gives a value
    /// its text: <c>order.Status.GetText()</c>.
    /// </summary>
    public const string ExtensionName = "GetText";

    /// <summary>
    /// The name of the method every generated class gets, which gives a text by its key:
    /// <c>ShopLocalization.GetTextByKey("Checkout")</c>.
    /// </summary>
    public const string GetName = "GetTextByKey";

    /// <summary>
    /// Whether a text can't become a member of the generated texts class (<c>LOC300</c>). It carries the class's own
    /// name, which C# allows no member, the name of the method that gives a text by its key
    /// (<see cref="GetName"/>), or the name of a method that gives the text of an enum value
    /// (<see cref="LookupName"/>, and <see cref="ExtensionName"/> on the value itself). The class declares no other
    /// member of its own, so that no other name is taken.
    /// </summary>
    /// <param name="name">The name of the text.</param>
    /// <param name="textsName">The name of the generated class.</param>
    /// <param name="hasLookup">Whether the class gets those two methods, as that of a set of the texts of an enum does.</param>
    public static bool IsReservedTextName(string name, string textsName, bool hasLookup)
    {
        var isReserved = String.Equals(name, textsName, StringComparison.Ordinal)
                         || String.Equals(name, GetName, StringComparison.Ordinal)
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

        // A set marked [ExcludeFromLocalizationDiscovery] is registered by hand, so nothing is reported about it, but it
        // does translate its contract: it counts as a set of its language, here and for the assemblies referencing this
        // one. Otherwise the fix LOC208 names for a set needing constructor arguments would end in LOC201 or LOC700,
        // asking for the very set that exists.
        var excludedSets = candidates.Where(static candidate => candidate.IsExcluded).ToImmutableArray();
        var excluded = excludedSets.Where(static set => set.Language is not null).ToImmutableArray();

        // A set whose language the build can't read (its LocalizationLanguageCode is no constant, or comes from another assembly)
        // may be the set of any language. So a contract it implements is not said to miss one, here or in an assembly
        // referencing this one: that would ask for a set that exists.
        // A set whose name gives no language, and that has no LocalizationLanguageCode, serves none. It gets LOC208, and
        // its contract is held to every language as if it weren't there: taken for a set of any language, a Dutch set
        // renamed ShopLocalizationDutch would hide every missing language of its contract behind that one warning.
        // Nor is a set whose code is no culture name (LOC207), as "en_GB": registering it throws, so it serves none.
        var unreadable = sets.Concat(excludedSets).Where(static set => IsUnreadable(set)).ToImmutableArray();
        var findings = ImmutableArray.CreateBuilder<LocalizationSetsFinding>();
        var contracts = ImmutableArray.CreateBuilder<LocalizationContract>();
        var provided = ImmutableArray.CreateBuilder<ProvidedLocalizationSet>();

        RecordSetsOfContracts(sets.Concat(excludedSets), provided);

        if (languages is not { } declared)
        {
            ReportWithoutLanguages(sets, generatedContracts, findings);
        }
        else if (declared.DefaultLanguage is not { } defaultLanguage)
        {
            // The attribute's first language is one the build can't use, which LOC101 reports. Nothing is held to a
            // default language then, and the next language does not take its place: that would report every set of the
            // language that was meant, and write no contract for it. The contracts are still written, each from the
            // set that starts it, so that the code using them keeps compiling while the attribute is put right.
            AnalyzeContracts(sets, excluded, unreadable, declared, generatedContracts, contracts, provided, findings: null, cancellationToken);

            // What needs no language list is still reported, as it is without the attribute.
            ReportDerivedFromOtherLanguage(sets, generatedContracts, findings);
        }
        else
        {
            AnalyzeContracts(sets, excluded, unreadable, declared, generatedContracts, contracts, provided, findings, cancellationToken);
            AnalyzeOwnContracts(sets, excluded, unreadable, declared, defaultLanguage, findings);
            AnalyzeSets(sets, declared.Codes, defaultLanguage, generatedContracts, findings);
            ReportExcludedSetsOfUnlistedLanguages(excluded, declared.Codes, findings);
            ReportDefaultSetsWithoutLanguage(sets, declared, defaultLanguage, generatedContracts, findings);
            ReportDefaultSetsOfAnotherLanguage(sets, defaultLanguage, generatedContracts, findings);
            ReportDefaultSetsOfAnotherContract(sets, defaultLanguage, generatedContracts, findings);
            ReportUnextendableDefaultSets(unextendable, defaultLanguage, generatedContracts, findings);
        }

        // Needs no language list: an app adding a language to a library's contract may declare none.
        ReportRepeatedLanguages(sets, generatedContracts, findings);

        var analysis = new LocalizationSetsAnalysis(
            new EquatableArray<LocalizationContract>(contracts.ToImmutable()),
            new EquatableArray<ProvidedLocalizationSet>(provided.Distinct().OrderBy(static set => set.Contract, StringComparer.Ordinal).ThenBy(static set => set.Language, StringComparer.Ordinal).ToImmutableArray()),
            new EquatableArray<LocalizationSetsFinding>(findings.ToImmutable()));

        return analysis;
    }

    /// <summary>
    /// Records every set for each public contract it implements, under its language. A set whose language the build
    /// can't read is recorded under <see cref="ProvidedLocalizationSet.UnreadableLanguage"/>, and a set whose name
    /// gives no language is left out.
    /// </summary>
    private static void RecordSetsOfContracts(IEnumerable<DiscoveredLocalization> sets, ImmutableArray<ProvidedLocalizationSet>.Builder provided)
    {
        foreach (var set in sets)
        {
            if (set.NameGivesNoLanguage || (set.Language is null && !IsUnreadable(set)))
                continue;

            foreach (var contract in set.ContractNames)
                provided.Add(new ProvidedLocalizationSet(contract, set.Language ?? ProvidedLocalizationSet.UnreadableLanguage));
        }
    }

    /// <summary>
    /// Whether a set has a language the build can't read, so that it may be the set of any language: its
    /// <c>LocalizationLanguageCode</c> isn't a constant, or it inherits it from a set of another assembly. Not a set
    /// whose name gives no language, and not one whose code is no culture name (<c>LOC207</c>), which serve none.
    /// </summary>
    private static bool IsUnreadable(DiscoveredLocalization set)
    {
        var isUnreadable = set is { Language: null, NameGivesNoLanguage: false, Locations.InvalidLanguageCode: null };
        return isUnreadable;
    }

    /// <summary>
    /// Reports what needs no language list, in an assembly that declares no languages: a set that would start a
    /// contract (<c>LOC100</c>), and a set that derives from a set of another language (<c>LOC203</c>).
    /// </summary>
    private static void ReportWithoutLanguages(
        ImmutableArray<DiscoveredLocalization> sets,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
                // Without a default language there is no telling which set the contract should come from.
                if (set.ListsLocalizationSet && !LocalizationDiscovery.ImplementsContract(set) && !LocalizationDiscovery.RewordsSet(set))
                    findings.Add(new LocalizationSetsFinding(LocalizationSetsRule.NoLanguagesDeclared, set.Location, new EquatableArray<string>([set.Name])));
                // No language list needed: an app rewording a library under a name without its language suffix would
                // serve another language without a word.
                else if (set.Language is { } language && IsDerivedFromOtherLanguage(set, generatedContracts))
                    findings.Add(DerivedFromOtherLanguage(set, language));
        }
    }

    /// <summary>
    /// Reports every set that derives from a set of another language (<c>LOC203</c>), which needs no language list.
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
