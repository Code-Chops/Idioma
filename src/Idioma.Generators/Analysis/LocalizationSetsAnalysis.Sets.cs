using System.Text.RegularExpressions;

namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationSetsAnalysis"/>
internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// A name ending in a language and a region or script, such as <c>EnGb</c> or <c>ZhHans</c>
    /// (<see cref="GuessLongCode"/>).
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
            // A type of the contract's name is what it lists, and LOC200 reports that already.
            else if (IsMissingContract(set, defaultLanguage) && set.OfContract.TypeNamedLikeContract is null)
            {
                var baseName = LocalizationDiscovery.DeriveBaseName(set.Name, language) ?? set.Name;
                var defaultSet = $"{baseName}{SupportedLanguages.ToTypeSuffix(defaultLanguage)}";
                var contract = $"I{baseName}";
                var isGenerated = generatedContracts.Contains(LocalizationDiscovery.GetContractFullName(set.Namespace, baseName));

                // Without a usable default-language set, there's no contract to add, so the advice names none.
                var (problem, advice) = isGenerated
                    ? (MissesContract(contract), AddContract(contract, defaultSet))
                    : ("implements no contract", $"Its contract would be '{contract}', which the generator writes from a default-language set named '{defaultSet}' that it can use. Write that set, or remove ILocalizationSet if '{set.Name}' isn't a set.");

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.MissingContract,
                    set.Location,
                    new EquatableArray<string>([set.Name, language, problem, advice]),
                    // A fix naming a contract that doesn't exist wouldn't compile.
                    SuggestedName: isGenerated ? contract : null));
            }
        }
    }

    /// <summary>
    /// The problem in the <c>LOC205</c> message for a generated contract.
    /// </summary>
    private static string MissesContract(string contract)
    {
        return $"doesn't implement '{contract}'";
    }

    /// <summary>
    /// The advice in the <c>LOC205</c> message for a generated contract.
    /// </summary>
    private static string AddContract(string contract, string defaultSet)
    {
        return $"Add '{contract}', the contract generated from '{defaultSet}', next to ILocalizationSet.";
    }

    /// <summary>
    /// Reports an excluded set that serves an unlisted language (<c>LOC202</c>). An app shows an unlisted culture in
    /// its default language, so the set would never show.
    /// </summary>
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
    /// Whether a set derives from a set of another language, which it would show for every text it doesn't override.
    /// A region on top of its language (<c>en-GB</c> on <c>en</c>) is fine.
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
    /// A set of an unlisted language (<c>LOC202</c>). The fix lists the longer code the name likely meant, such as
    /// <c>fr-BE</c> for <c>ShopLocalizationFrBe</c>, and else its own language. A code that's no language, such as
    /// <c>gb</c>, isn't listed, because that would only give <c>LOC103</c>. A near miss, such as <c>en-UK</c> for
    /// <c>ShopLocalizationEnUk</c>, gets the code it likely meant (<c>en-GB</c>) and the name that gives it.
    /// </summary>
    /// <remarks>
    /// A region after a language that isn't listed, as in <c>ShopLocalizationFrBe</c> without <c>fr</c>, may be either:
    /// <c>fr-BE</c>, or <c>be</c> (Belarusian) after a word. The message names both, and the fix offers both, the long
    /// code first.
    /// </remarks>
    private static LocalizationSetsFinding UnlistedLanguage(DiscoveredLocalization set, string language, EquatableArray<string> codes)
    {
        var meant = GuessLongCode(set.Name, codes);
        var isGuessed = false;

        if (meant is null && GuessLongCode(set.Name, codes, afterAnyLanguage: true) is { } guessed)
        {
            meant = guessed;
            isGuessed = true;
        }

        var isLanguage = SupportedLanguages.FindUnknownPart(language) is null;
        string? nearMiss = null;

        if (meant is not null && SupportedLanguages.FindUnknownPart(meant) is { } unknown)
        {
            if (unknown.Corrected is { } corrected)
            {
                var stem = set.Name.Substring(0, set.Name.Length - SupportedLanguages.ToTypeSuffix(meant).Length);
                nearMiss = $"Did you mean '{corrected}'? Then name it {stem}{SupportedLanguages.ToTypeSuffix(corrected)}.";
            }

            meant = null;
        }

        var meantAdvice = nearMiss ?? $"If it's meant for '{meant}', list '{meant}' in the attribute, which its name then gives it.";

        var advice = (meant ?? nearMiss, isLanguage) switch
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
            SuggestedName: meant ?? (isLanguage ? language : null),
            AlternativeName: meant is not null && isGuessed && isLanguage ? language : null);

        return unlistedLanguageFinding;
    }

    /// <summary>
    /// A set of a non-default language that lists <c>ILocalizationSet</c> but no contract, so no text reads it.
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
    /// The longer code a set's name seems to end in, for the advice of <c>LOC202</c>: <c>en-GB</c> for
    /// <c>ShopLocalizationEnGb</c>. A region in PascalCase is guessed only after a listed language, so <c>App</c> in
    /// <c>MyAppNl</c> isn't taken for one.
    /// </summary>
    /// <param name="afterAnyLanguage">
    /// Also after a two-letter language that isn't listed, where both it and the region exist, as <c>fr-BE</c> for
    /// <c>ShopLocalizationFrBe</c>. The name may then mean the region as a language after a word, so it's a guess.
    /// </param>
    private static string? GuessLongCode(string setName, EquatableArray<string> codes, bool afterAnyLanguage = false)
    {
        var match = LongCodeSuffix.Match(setName);

        if (!match.Success)
            return null;

        var language = match.Groups[1].Value.ToLowerInvariant();
        var part = match.Groups[2].Value;

        // A region in capitals (NlBE) or a script (ZhHans) reads as a code after any word.
        if (part.Length is 2 && Char.IsLower(part[1]) && !IsListed(language, codes)
            && !(afterAnyLanguage && language.Length is 2 && SupportedLanguages.FindUnknownPart($"{language}-{part.ToUpperInvariant()}") is null))
        {
            return null;
        }

        var longCode = $"{language}-{(part.Length is 2 ? part.ToUpperInvariant() : part)}";
        return longCode;
    }

    /// <summary>
    /// Whether a set is of exactly that language: a set of <c>en</c> is no set of <c>en-GB</c>.
    /// </summary>
    private static bool IsSetOf(DiscoveredLocalization set, string code)
    {
        var isSetOf = String.Equals(set.Language, code, StringComparison.OrdinalIgnoreCase);
        return isSetOf;
    }
}
