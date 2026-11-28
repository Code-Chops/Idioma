namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationSetsAnalysis"/>
internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// Reports a default-language set whose name gives no language (<c>LOC200</c>), with a name that ends in the
    /// default language. Such a set starts no contract, so every set that lists it fails to compile (CS0246).
    /// </summary>
    private static void ReportDefaultSetsWithoutLanguage(
        ImmutableArray<DiscoveredLocalization> sets,
        DeclaredLanguages declared,
        string defaultLanguage,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
            if (!LocalizationDiscovery.IsDefaultSetWithoutLanguage(set, generatedContracts))
                continue;

            var suffix = SupportedLanguages.ToTypeSuffix(defaultLanguage);

            if (IsSuffixAlone(set.Name, suffix))
            {
                findings.Add(SuffixAlone(set, suffix));
                continue;
            }

            var suggestedName = SuggestNameEndingIn(set.Name, suffix);

            // It may be meant for another language, as ShopLocalizationDutch is. The contract it lists gives that name.
            var otherLanguage = declared.Codes.FirstOrDefault(code => !String.Equals(code, defaultLanguage, StringComparison.OrdinalIgnoreCase));
            var contractStem = set.UnresolvedBaseTypes.Select(static baseType => baseType.Name)
                .FirstOrDefault(static name => name.Length > 1 && name[0] == 'I' && Char.IsUpper(name[1]))?.Substring(1);
            var advice = otherLanguage is null || contractStem is null
                ? $"Rename it to {suggestedName}."
                : $"Rename it to {suggestedName}. If it's the set of another language, end its name in that language instead, as in {contractStem}{SupportedLanguages.ToTypeSuffix(otherLanguage)}.";

            findings.Add(new LocalizationSetsFinding(
                LocalizationSetsRule.CannotExtend,
                set.Location,
                new EquatableArray<string>([set.Name, $"its name doesn't end in {suffix}, the suffix of the default language, so the build can't read its language", advice]),
                SuggestedName: suggestedName));
        }
    }

    /// <summary>
    /// Reports a set whose name ends in the default language but serves another language, such as
    /// <c>ShopLocalizationEnNl</c> for a declared <c>en-NL</c> where the default language is <c>nl</c> (<c>LOC200</c>). No
    /// set then starts its contract, so every set that lists it fails to compile (CS0246).
    /// </summary>
    private static void ReportDefaultSetsOfAnotherLanguage(
        ImmutableArray<DiscoveredLocalization> sets,
        string defaultLanguage,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
            if (set is not { ListsLocalizationSet: true, Language: { } language }
                || String.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase)
                || LocalizationDiscovery.ImplementsContract(set)
                || LocalizationDiscovery.RewordsSet(set)
                || LocalizationDiscovery.DeriveBaseName(set.Name, defaultLanguage) is not { } baseName)
            {
                continue;
            }

            var ownContract = $"I{baseName}";

            if (!set.UnresolvedBaseTypes.Any(baseType => String.Equals(baseType.Name, ownContract, StringComparison.Ordinal)
                                                         && LocalizationDiscovery.FindGeneratedContract(baseType, generatedContracts) is null))
            {
                continue;
            }

            var problem = $"it serves '{language}', where its name ends in the default language '{defaultLanguage}'";

            findings.Add(new LocalizationSetsFinding(
                LocalizationSetsRule.CannotExtend,
                set.Location,
                new EquatableArray<string>([set.Name, problem, $"Rename it so that its name gives '{defaultLanguage}'."])));
        }
    }

    /// <summary>
    /// Reports a set named after the language of a regional default language, such as <c>ShopLocalizationEn</c> with
    /// <c>("en-US", "nl")</c> (<c>LOC200</c>). No set then starts its contract, so every set that lists it fails to
    /// compile (CS0246).
    /// </summary>
    private static void ReportDefaultSetsOfParentLanguage(
        ImmutableArray<DiscoveredLocalization> sets,
        string defaultLanguage,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
            if (set is not { ListsLocalizationSet: true, Language: { } language }
                || String.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase)
                || !SupportedLanguages.Serves(language, defaultLanguage)
                || LocalizationDiscovery.ImplementsContract(set)
                || LocalizationDiscovery.RewordsSet(set)
                || LocalizationDiscovery.DeriveBaseName(set.Name, language) is not { } baseName)
            {
                continue;
            }

            var ownContract = $"I{baseName}";

            if (!set.UnresolvedBaseTypes.Any(baseType => String.Equals(baseType.Name, ownContract, StringComparison.Ordinal)
                                                         && LocalizationDiscovery.FindGeneratedContract(baseType, generatedContracts) is null))
            {
                continue;
            }

            var suggestedName = baseName + SupportedLanguages.ToTypeSuffix(defaultLanguage);

            findings.Add(new LocalizationSetsFinding(
                LocalizationSetsRule.CannotExtend,
                set.Location,
                new EquatableArray<string>([set.Name, $"its name gives '{language}', and the default language is '{defaultLanguage}'", $"Rename it to {suggestedName}, or make '{language}' the default language in [assembly: SupportedLanguages]."]),
                SuggestedName: suggestedName));
        }
    }

    /// <summary>
    /// Reports a default-language set that implements another contract and also lists the contract named after it,
    /// which it can't start (<c>LOC200</c>). A set that implements a contract, such as a library's, starts none.
    /// </summary>
    private static void ReportDefaultSetsOfAnotherContract(
        ImmutableArray<DiscoveredLocalization> sets,
        string defaultLanguage,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
            if (set is not { ListsLocalizationSet: true, Language: { } language }
                || !String.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase)
                || !LocalizationDiscovery.ImplementsContract(set)
                || LocalizationDiscovery.RewordsSet(set)
                || LocalizationDiscovery.DeriveBaseName(set.Name, language) is not { } baseName)
            {
                continue;
            }

            var ownContract = $"I{baseName}";

            if (!set.UnresolvedBaseTypes.Any(baseType => String.Equals(baseType.Name, ownContract, StringComparison.Ordinal)
                                                         && LocalizationDiscovery.FindGeneratedContract(baseType, generatedContracts) is null))
            {
                continue;
            }

            var other = set.ContractNames.Concat(set.OwnContractNames).FirstOrDefault() is { } name ? $"'{name}'" : "an internal contract of another assembly";

            findings.Add(new LocalizationSetsFinding(
                LocalizationSetsRule.CannotExtend,
                set.Location,
                new EquatableArray<string>([set.Name, $"it implements {other} too, and a set starts a contract only when it implements no other", $"Write the texts of '{ownContract}' in a set of their own, and implement {other} in another class."])));
        }
    }

    /// <summary>
    /// Reports a set of another language that lists the contract named after it while no default-language set gives
    /// that contract (<c>LOC205</c>), such as <c>ShopLocalizationNl : ILocalizationSet, IShopLocalization</c> without a
    /// <c>ShopLocalizationEn</c>. Without this, the build shows only CS0246 for the contract. A set reported for
    /// anything else already, such as LOC202 or a name of the default language, isn't reported again.
    /// </summary>
    private static void ReportSetsOfContractWithoutDefaultSet(
        ImmutableArray<DiscoveredLocalization> sets,
        string defaultLanguage,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
            if (set is not { ListsLocalizationSet: true, Language: { } language }
                || String.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase)
                || LocalizationDiscovery.ImplementsContract(set)
                || LocalizationDiscovery.RewordsSet(set)
                || LocalizationDiscovery.DeriveBaseName(set.Name, language) is not { } baseName)
            {
                continue;
            }

            var ownContract = $"I{baseName}";

            if (!ListsMissingContract(set, ownContract, generatedContracts)
                || findings.Any(finding => finding.Location == set.Location))
            {
                continue;
            }

            // A set meant to give the contract is reported already, such as ShopLocalizationEn where the default
            // language is en-US (LOC200), and fixing it gives this set its contract.
            if (sets.Any(other => !String.Equals(other.MetadataName, set.MetadataName, StringComparison.Ordinal)
                                  && String.Equals(other.Namespace, set.Namespace, StringComparison.Ordinal)
                                  && ListsMissingContract(other, ownContract, generatedContracts)
                                  && findings.Any(finding => finding.Location == other.Location)))
            {
                continue;
            }

            var defaultSet = $"{baseName}{SupportedLanguages.ToTypeSuffix(defaultLanguage)}";

            findings.Add(new LocalizationSetsFinding(
                LocalizationSetsRule.MissingContract,
                set.Location,
                new EquatableArray<string>([
                    set.Name,
                    language,
                    $"its contract '{ownContract}' doesn't exist",
                    $"The generator writes '{ownContract}' from a default-language set named '{defaultSet}' that it can use. Write that set, or check the name of the one you have.",
                ])));
        }
    }

    /// <summary>
    /// Whether a set lists a contract of that name that binds to nothing and that no set generates.
    /// </summary>
    private static bool ListsMissingContract(DiscoveredLocalization set, string contract, GeneratedContracts generatedContracts)
    {
        var listsMissingContract = set.UnresolvedBaseTypes.Any(baseType => String.Equals(baseType.Name, contract, StringComparison.Ordinal)
                                                                           && LocalizationDiscovery.FindGeneratedContract(baseType, generatedContracts) is null);

        return listsMissingContract;
    }

    /// <summary>
    /// Reports a default-language set of a shape the generator can't extend (<c>LOC200</c>). A class that lists a
    /// contract another set starts, such as a test double, is left alone, because it starts none.
    /// </summary>
    private static void ReportUnextendableDefaultSets(
        EquatableArray<UnextendableSet> unextendable,
        string defaultLanguage,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in unextendable)
        {
            if (String.Equals(set.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase)
                && !LocalizationDiscovery.ListsOnlyOtherGeneratedContracts(set.UnresolvedBaseTypes, generatedContracts, ownContract: null))
            {
                findings.Add(new LocalizationSetsFinding(LocalizationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, set.Problem, "Make it a top-level class that isn't generic, abstract, or file-local."])));
            }
        }
    }
}
