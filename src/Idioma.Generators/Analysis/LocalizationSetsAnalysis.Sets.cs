using System.Text.RegularExpressions;

namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationSetsAnalysis"/>
internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// A name ending in what looks like a longer code (<see cref="GuessLongCode"/>): a capitalized language of two or
    /// three letters, then a region of two letters or a script of four, starting with a capital.
    /// </summary>
    private static readonly Regex LongCodeSuffix = new("(?<=.)([A-Z][a-z]{1,2})([A-Z][a-z]{3}|[A-Z][A-Za-z])$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static void AnalyzeSets(
        ImmutableArray<DiscoveredLocalization> sets,
        EquatableArray<string> codes,
        string defaultLanguage,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
            if (set.Language is not { } language)
                continue;

            if (!IsListed(language, codes))
            {
                findings.Add(UnlistedLanguage(set, language, codes));
            }
            else if (IsDerivedFromOtherLanguage(set, generatedContracts))
            {
                findings.Add(DerivedFromOtherLanguage(set, language));
            }
            // A type of the contract's name, which the default-language set's LOC200 reports, is what it lists.
            else if (IsMissingContract(set, defaultLanguage) && set.OfContract.TypeNamedLikeContract is null)
            {
                var baseName = LocalizationDiscovery.DeriveBaseName(set.Name, language) ?? set.Name;
                var defaultSet = $"{baseName}{SupportedLanguages.ToTypeSuffix(defaultLanguage)}";
                var contract = $"I{baseName}";
                var isGenerated = generatedContracts.Contains(LocalizationDiscovery.GetContractFullName(set.Namespace, baseName));

                // Without a default-language set the generator can use, there's no contract to add. Naming one, or the
                // set it would come from, would send the reader after types that don't exist.
                var (problem, advice) = isGenerated
                    ? (MissesContract(contract), AddContract(contract, defaultSet))
                    : ("implements no contract", $"Its contract would be '{contract}', which the generator writes from a default-language set named '{defaultSet}' that it can use. Write that set, or remove ILocalizationSet if '{set.Name}' isn't a set.");

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.MissingContract,
                    set.Location,
                    new EquatableArray<string>([set.Name, language, problem, advice]),
                    // Only a contract this build generates: a fix naming one that does not exist would not compile.
                    SuggestedName: isGenerated ? contract : null));
            }
        }
    }

    /// <summary>
    /// What a set lacks, as the clause of the <c>LOC204</c> message, for a contract this build generates.
    /// </summary>
    private static string MissesContract(string contract)
    {
        return $"doesn't implement '{contract}'";
    }

    /// <summary>
    /// What to add, as the advice of the <c>LOC204</c> message, for a contract this build generates from a set.
    /// </summary>
    private static string AddContract(string contract, string defaultSet)
    {
        return $"Add '{contract}', the contract generated from '{defaultSet}', next to ILocalizationSet.";
    }

    /// <summary>
    /// Reports a set marked <c>[ExcludeFromLocalizationDiscovery]</c> that serves a language the attribute doesn't
    /// list (<c>LOC202</c>).
    /// </summary>
    // Registered by hand, a set still serves only a listed language: an application shows a culture outside its
    // list in its default language, so a set for another language would never show, without a word.
    private static void ReportExcludedSetsOfUnlistedLanguages(
        ImmutableArray<DiscoveredLocalization> excluded,
        EquatableArray<string> codes,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        foreach (var set in excluded)
            if (!IsListed(set.Language!, codes))
                findings.Add(UnlistedLanguage(set, set.Language!, codes));
    }

    /// <summary>
    /// A set in another language than the set it derives from keeps that language for every text it does not override.
    /// A regional set on top of its language (<c>en-GB</c> on <c>en</c>) is fine. A base that implements
    /// <c>ILocalizationSet</c> only through a contract generated here is a set once that contract exists.
    /// </summary>
    private static bool IsDerivedFromOtherLanguage(DiscoveredLocalization set, GeneratedContracts generatedContracts)
    {
        var isDerived = set.BaseSet is { Language: { } baseLanguage } baseSet
               && (baseSet.Contracts.Length is 0 || baseSet.Contracts.Any(baseType => LocalizationDiscovery.FindGeneratedContract(baseType, generatedContracts) is not null))
               && !SupportedLanguages.Serves(baseLanguage, set.Language!);

        return isDerived;
    }

    private static LocalizationSetsFinding DerivedFromOtherLanguage(DiscoveredLocalization set, string language)
    {
        var isDerived = new LocalizationSetsFinding(
            LocalizationSetsRule.DerivedFromOtherLanguage,
            set.Location,
            new EquatableArray<string>([set.Name, language, set.BaseSet!.Value.Name, set.BaseSet.Value.Language!]));

        return isDerived;
    }

    /// <summary>
    /// A set of a language the attribute doesn't list. It carries the language its code fix lists: the longer code its
    /// name was likely meant for, such as <c>en-GB</c> for <c>ShopLocalizationEnGb</c>, and else its own.
    /// </summary>
    /// <remarks>
    /// The language its name gives may be no language at all, such as <c>gb</c>, the last two letters of
    /// <c>ShopLocalizationEnGb</c>. Listing that would only give <c>LOC104</c>, so it's neither advised nor listed by the
    /// fix.
    /// </remarks>
    private static LocalizationSetsFinding UnlistedLanguage(DiscoveredLocalization set, string language, EquatableArray<string> codes)
    {
        var meant = GuessLongCode(set.Name, codes);
        var isLanguage = SupportedLanguages.FindUnknownPart(language) is null;
        var meantAdvice = $"If it's meant for '{meant}', list '{meant}' in the attribute, which its name then gives it, or implement 'string ILocalizationSet.LocalizationLanguageCode => \"{meant}\";'.";

        var advice = (meant, isLanguage) switch
        {
            (not null, true) => $"{meantAdvice} Otherwise add '{language}' to the attribute, or remove the set.",
            (not null, false) => $"{meantAdvice} Otherwise remove the set, because '{language}' is no language.",
            (null, true) => "Add the language to the attribute, or remove the set.",
            (null, false) => $"'{language}' is no language, so name the set after one that the attribute lists, or remove the set.",
        };

        var unlistedLanguageFinding = new LocalizationSetsFinding(
            LocalizationSetsRule.UnlistedLanguage,
            set.Location,
            new EquatableArray<string>([set.Name, language, advice]),
            SuggestedName: meant ?? (isLanguage ? language : null));

        return unlistedLanguageFinding;
    }

    /// <summary>
    /// A set that lists <c>ILocalizationSet</c> in another language than the default, and implements no contract
    /// besides, so no text would ever read it. Without a default language to go by, no set is taken for one.
    /// </summary>
    private static bool IsMissingContract(DiscoveredLocalization set, string? defaultLanguage)
    {
        var isMissingContract = defaultLanguage is not null
               && set is { ListsLocalizationSet: true, BaseSet: null, UnresolvedBaseTypes.Length: 0 }
               && !LocalizationDiscovery.ImplementsContract(set)
               && !String.Equals(set.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase);

        return isMissingContract;
    }

    /// <summary>
    /// Listed, a region of something listed, or the language of a listed region.
    /// </summary>
    private static bool IsListed(string language, EquatableArray<string> codes)
    {
        var isListed = codes.Any(code => SupportedLanguages.Serves(code, language) || SupportedLanguages.Serves(language, code));
        return isListed;
    }

    /// <summary>
    /// A longer code a set's name looks like it ends in, for the advice of <c>LOC202</c> only: a language of two or three
    /// letters followed by a region (<c>ShopLocalizationEnGb</c>: <c>en-GB</c>) or a script (<c>ShopLocalizationZhHans</c>:
    /// <c>zh-Hans</c>). A name gives such a code only when the attribute lists it, as a name alone can't tell it from
    /// <c>AdminUiNl</c>. So a region in PascalCase is guessed only after a language the attribute lists (<c>Nl</c> in
    /// <c>ShopLocalizationNlBe</c>), and not after a word such as <c>App</c> in <c>MyAppNl</c>.
    /// </summary>
    private static string? GuessLongCode(string setName, EquatableArray<string> codes)
    {
        var match = LongCodeSuffix.Match(setName);

        if (!match.Success)
            return null;

        var language = match.Groups[1].Value.ToLowerInvariant();
        var part = match.Groups[2].Value;

        // A region in capitals (NlBE) or a script (ZhHans) reads as a code after any word.
        if (part.Length is 2 && Char.IsLower(part[1]) && !IsListed(language, codes))
            return null;

        // A region is in upper case in a culture name, and a script as the name writes it.
        var longCode = $"{language}-{(part.Length is 2 ? part.ToUpperInvariant() : part)}";
        return longCode;
    }

    /// <summary>
    /// Whether a set is of a language itself, and not of one that only serves it: a set of <c>en</c> is no set of
    /// <c>en-GB</c>.
    /// </summary>
    private static bool IsSetOf(DiscoveredLocalization set, string code)
    {
        var isSetOf = String.Equals(set.Language, code, StringComparison.OrdinalIgnoreCase);
        return isSetOf;
    }
}
