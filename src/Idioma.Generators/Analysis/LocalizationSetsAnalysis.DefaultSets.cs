namespace CodeChops.Idioma.Generators;

internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// Reports a default-language set whose name gives no language (<c>LOC200</c>), with the name that ends in the
    /// default language.
    /// </summary>
    // Such a set starts no contract, so every set that lists the contract fails to compile (CS0246). The name that
    // ends in the default language resolves it.
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
            var suggestedName = SuggestNameEndingIn(set.Name, suffix);

            // Its name gives no language, so it may be meant for another one, as ShopLocalizationDutch is. The
            // contract it lists, IShopLocalization, gives the name that set would have.
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
    /// Reports a set named after the default language whose <c>LocalizationLanguageCode</c> gives it another language, or
    /// one the build can't read (<c>LOC200</c>). It lists the contract named after it, which no set then starts.
    /// </summary>
    // Without this, every set that lists the contract fails to compile (CS0246), and nothing says why. A code that isn't
    // a culture name is LOC207 already.
    private static void ReportDefaultSetsOfAnotherLanguage(
        ImmutableArray<DiscoveredLocalization> sets,
        string defaultLanguage,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
            if (set is not { ListsLocalizationSet: true, NameGivesNoLanguage: false, Locations.InvalidLanguageCode: null }
                || String.Equals(set.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase)
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

            var problem = set.Language is { } language
                ? $"its LocalizationLanguageCode gives '{language}', where its name gives '{defaultLanguage}'"
                : "its LocalizationLanguageCode isn't a constant, so the build can't read its language";

            findings.Add(new LocalizationSetsFinding(
                LocalizationSetsRule.CannotExtend,
                set.Location,
                new EquatableArray<string>([set.Name, problem, $"Return the constant '{defaultLanguage}' from LocalizationLanguageCode, or remove LocalizationLanguageCode so that the name gives the language."])));
        }
    }

    /// <summary>
    /// Reports a default-language set that implements another contract and also lists the contract named after it,
    /// which it can't start (<c>LOC200</c>).
    /// </summary>
    // A default-language set that implements a contract already, such as a library's, translates or rewords it,
    // and starts none of its own. When it also lists the contract named after it, which binds to nothing, that
    // contract is the one it meant to start, and every set that lists it fails to compile (CS0246).
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
    /// Reports a default-language set of a shape the generator can't extend (<c>LOC200</c>).
    /// </summary>
    // Such a set would otherwise leave its contract and texts ungenerated without a word, and every other language's
    // set without the contract it implements. Not a class that lists a contract another set starts, such as a test
    // double of the default language: it's a set of that contract, and starts none of its own.
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
