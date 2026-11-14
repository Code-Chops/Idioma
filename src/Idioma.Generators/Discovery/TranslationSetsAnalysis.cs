using System.Text.RegularExpressions;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds the sets of an assembly against its languages: what to generate, and what to report. Plain values in, plain
/// values out, so a pipeline step is reused while nothing relevant changes.
/// </summary>
internal sealed record TranslationSetsAnalysis(
    EquatableArray<TranslationContract> Contracts,
    EquatableArray<ProvidedTranslationSet> ProvidedSets,
    EquatableArray<TranslationSetsFinding> Findings)
{
    /// <summary>
    /// A name ending in what looks like a longer code (<see cref="GuessLongCode"/>): a capitalized language of two or
    /// three letters, then an upper-case region or a capitalized script.
    /// </summary>
    private static readonly Regex LongCodeSuffix = new("(?<=.)([A-Z][a-z]{1,2})([A-Z]{2}|[A-Z][a-z]{3})$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Whether a text cannot become a member of the generated texts class: it carries the class's own name, which C#
    /// allows no member (<c>LOC014</c>). The class declares no member of its own, so that no other name is taken.
    /// </summary>
    public static bool IsReservedTextName(string name, string textsName)
    {
        var isReserved = String.Equals(name, textsName, StringComparison.Ordinal);
        return isReserved;
    }

    public static TranslationSetsAnalysis Analyze(
        EquatableArray<DiscoveredTranslations> discovered,
        EquatableArray<UnextendableSet> unextendable,
        DeclaredLanguages? languages)
    {
        var candidates = Merge(discovered);
        var generatedContracts = TranslationsDiscovery.GetGeneratedContractNames(candidates, languages);

        // A class kept only for a base type that binds to nothing is no set unless that type is a contract generated
        // here: it may derive from another generator's output (Formosa's generated configuration bases), which no
        // generator sees.
        var sets = candidates.Where(candidate => !candidate.IsExcluded && TranslationsDiscovery.IsSet(candidate, generatedContracts)).ToImmutableArray();

        // A set marked [ExcludeFromTranslationDiscovery] is registered by hand, so nothing is reported about it, but it
        // does translate its contract: it counts as a set of its language, here and for the assemblies referencing this
        // one. Otherwise the fix LOC003 names for a set needing constructor arguments would end in LOC004 or LOC008,
        // asking for the very set that exists.
        var excludedSets = candidates.Where(candidate => candidate.IsExcluded && TranslationsDiscovery.IsSet(candidate, generatedContracts)).ToImmutableArray();
        var excluded = excludedSets.Where(static set => set.Language is not null).ToImmutableArray();

        // A set whose language the build cannot read (its LanguageCode is no constant, or comes from another assembly)
        // may be the set of any language. So a contract it implements is not said to miss one, here or in an assembly
        // referencing this one: that would ask for a set that exists.
        var unreadable = sets.Concat(excludedSets).Where(static set => set.Language is null).ToImmutableArray();
        var findings = ImmutableArray.CreateBuilder<TranslationSetsFinding>();
        var contracts = ImmutableArray.CreateBuilder<TranslationContract>();
        var provided = ImmutableArray.CreateBuilder<ProvidedTranslationSet>();

        foreach (var set in sets.Concat(excludedSets))
            foreach (var contract in set.ContractNames)
                provided.Add(new ProvidedTranslationSet(contract, set.Language ?? ProvidedTranslationSet.UnreadableLanguage));

        if (languages is not { } declared)
        {
            foreach (var set in sets)
            {
                // Without a default language there is no telling which set the contract should come from.
                if (set.ListsTranslations && !TranslationsDiscovery.ImplementsContract(set) && !TranslationsDiscovery.RewordsSet(set))
                    findings.Add(new TranslationSetsFinding(TranslationSetsRule.NoLanguagesDeclared, set.Location, new EquatableArray<string>([set.Name])));
                // No language list needed: an app rewording a library under a name without its language suffix would
                // serve another language without a word.
                else if (set.Language is { } language && IsDerivedFromOtherLanguage(set, generatedContracts))
                    findings.Add(DerivedFromOtherLanguage(set, language));
            }
        }
        else if (declared.DefaultLanguage is not { } defaultLanguage)
        {
            // The attribute's first language is one the build cannot use, which LOC010 reports. Nothing is held to a
            // default language then, and the next language does not take its place: that would report every set of the
            // language that was meant, and write no contract for it. The contracts are still written, each from the
            // set that starts it, so that the code using them keeps compiling while the attribute is put right.
            AnalyzeContracts(sets, excluded, unreadable, declared, generatedContracts, contracts, provided, findings: null);

            // What needs no language list is still reported, as it is without the attribute.
            foreach (var set in sets)
                if (set.Language is { } language && IsDerivedFromOtherLanguage(set, generatedContracts))
                    findings.Add(DerivedFromOtherLanguage(set, language));
        }
        else
        {
            AnalyzeContracts(sets, excluded, unreadable, declared, generatedContracts, contracts, provided, findings);
            AnalyzeOwnContracts(sets, excluded, unreadable, declared, defaultLanguage, findings);
            AnalyzeSets(sets, declared.Codes, defaultLanguage, generatedContracts, findings);

            // Registered by hand, a set still serves only a listed language: an application shows a culture outside its
            // list in its default language, so a set for another language would never show, without a word.
            foreach (var set in excluded)
                if (!IsListed(set.Language!, declared.Codes))
                    findings.Add(UnlistedLanguage(set, set.Language!));

            // A default-language set of a shape the generator cannot extend would otherwise leave its contract and texts
            // ungenerated without a word, and every other language's set without the contract it implements.
            foreach (var set in unextendable)
                if (String.Equals(set.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase))
                    findings.Add(new TranslationSetsFinding(TranslationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, set.Problem])));
        }

        var analysis = new TranslationSetsAnalysis(
            new EquatableArray<TranslationContract>(contracts.ToImmutable()),
            new EquatableArray<ProvidedTranslationSet>(provided.Distinct().OrderBy(static set => set.Contract, StringComparer.Ordinal).ThenBy(static set => set.Language, StringComparer.Ordinal).ToImmutableArray()),
            new EquatableArray<TranslationSetsFinding>(findings.ToImmutable()));

        return analysis;
    }

    /// <summary>
    /// What a referenced library leaves untranslated: a contract no language set serves one of this assembly's
    /// languages for, which adding the set fixes, and inline texts lacking a language, which nothing here can fix.
    /// </summary>
    public static EquatableArray<TranslationSetsFinding> FindLibraryGaps(
        DeclaredLanguages? languages,
        EquatableArray<ProvidedTranslationSet> ownSets,
        EquatableArray<ReferencedTranslations> references)
    {
        if (languages is not { Codes.Length: > 0 } declared)
            return default;

        var findings = ImmutableArray.CreateBuilder<TranslationSetsFinding>();
        var referencedSets = references.SelectMany(static reference => reference.Sets).ToArray();
        var contracts = referencedSets.Select(static set => set.Contract).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal);

        foreach (var contract in contracts)
        {
            // A set whose language its build could not read may be the set of any of them.
            if (IsServed(referencedSets, contract, ProvidedTranslationSet.UnreadableLanguage) || IsServed(ownSets, contract, ProvidedTranslationSet.UnreadableLanguage))
                continue;

            foreach (var code in declared.Codes)
            {
                if (IsServed(referencedSets, contract, code) || IsServed(ownSets, contract, code))
                    continue;

                findings.Add(new TranslationSetsFinding(
                    TranslationSetsRule.LibraryMissingLanguage,
                    declared.Location,
                    new EquatableArray<string>([contract, code, SupportedLanguages.SuggestSet(SuggestSetName(contract, code), contract)])));
            }
        }

        // Inline texts: a library's sets can be given a language from here, but no text it writes inline can, whether it
        // has sets or not.
        foreach (var reference in references)
        {
            if (!reference.HasInlineTexts || reference.Languages.Length is 0)
                continue;

            foreach (var code in declared.Codes)
            {
                if (reference.Languages.Any(language => SupportedLanguages.Serves(language, code)))
                    continue;

                findings.Add(new TranslationSetsFinding(
                    TranslationSetsRule.LibraryCannotBeTranslated,
                    declared.Location,
                    new EquatableArray<string>([reference.AssemblyName, code])));
            }
        }

        return new EquatableArray<TranslationSetsFinding>(findings.ToImmutable());

        static bool IsServed(IEnumerable<ProvidedTranslationSet> sets, string contract, string code)
        {
            return sets.Any(set => String.Equals(set.Contract, contract, StringComparison.Ordinal) && SupportedLanguages.Serves(set.Language, code));
        }
    }

    /// <summary>
    /// A partial set is discovered once per declaration that has a base list: one set, with the unresolved base types of
    /// all of them.
    /// </summary>
    private static ImmutableArray<DiscoveredTranslations> Merge(EquatableArray<DiscoveredTranslations> discovered)
    {
        var merged = discovered
            .GroupBy(static set => set.MetadataName, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => group.First() with
            {
                UnresolvedBaseTypes = new EquatableArray<UnresolvedBaseType>(group.SelectMany(static set => set.UnresolvedBaseTypes).Distinct().ToImmutableArray()),
                ListsTranslations = group.Any(static set => set.ListsTranslations),
                IsPartial = group.Any(static set => set.IsPartial),
            })
            .ToImmutableArray();

        return merged;
    }

    /// <summary>
    /// Builds the contract of every default-language set, and holds it to the languages: each must have a set of its
    /// own. Without <paramref name="findings"/> nothing is reported, and the contracts are written all the same.
    /// </summary>
    private static void AnalyzeContracts(
        ImmutableArray<DiscoveredTranslations> sets,
        ImmutableArray<DiscoveredTranslations> excluded,
        ImmutableArray<DiscoveredTranslations> unreadable,
        DeclaredLanguages declared,
        ImmutableArray<string> generatedContracts,
        ImmutableArray<TranslationContract>.Builder contracts,
        ImmutableArray<ProvidedTranslationSet>.Builder provided,
        ImmutableArray<TranslationSetsFinding>.Builder? findings)
    {
        var defaultLanguage = declared.DefaultLanguage;
        var written = new HashSet<string>(StringComparer.Ordinal);

        foreach (var set in sets)
        {
            if (!TranslationsDiscovery.IsDefaultSet(set, defaultLanguage))
                continue;

            if (TranslationsDiscovery.DeriveBaseName(set.Name, set.Language!) is not { } baseName)
            {
                var suffix = SupportedLanguages.ToTypeSuffix(defaultLanguage ?? set.Language!);
                findings?.Add(new TranslationSetsFinding(
                    TranslationSetsRule.CannotExtend,
                    set.Location,
                    new EquatableArray<string>([set.Name, $"its name does not end in {suffix}, the suffix of the default language, so the generated class would carry the same name"]),
                    SuggestedName: SuggestNameEndingIn(set.Name, suffix)));
                continue;
            }

            if (!set.IsPartial)
            {
                findings?.Add(new TranslationSetsFinding(TranslationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, "it is not partial"])));
                continue;
            }

            // The generated texts take their default wording from an instance the generated code creates, so a set it
            // cannot create gets no texts either. Reported here, last, and not as LOC003: that one advises to exclude
            // the set and register it by hand, which a default-language set must never be. A default-language set has
            // its language, so only how it is created can keep it from registering.
            if (TranslationsDiscovery.GetCreationProblem(set) is { } problem)
            {
                findings?.Add(new TranslationSetsFinding(TranslationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, problem])));
                continue;
            }

            var contractName = $"I{baseName}";
            var contractFullName = TranslationsDiscovery.GetContractFullName(set.Namespace, baseName);

            // Without a default language two sets can start one contract, the Dutch set listing ITranslationSet as the
            // English one does. It is written once, from the first of them by name.
            if (!written.Add(contractFullName) && defaultLanguage is null)
                continue;

            foreach (var member in set.Texts)
                if (IsReservedTextName(member.Name, baseName))
                    findings?.Add(new TranslationSetsFinding(TranslationSetsRule.ReservedTextName, set.Location, new EquatableArray<string>([set.Name, member.Name, baseName])));

            var implementers = sets.Where(other => other.Language is not null && ImplementsGeneratedContract(other, set, contractFullName, generatedContracts)).ToArray();
            var excludedImplementers = excluded.Where(other => ImplementsGeneratedContract(other, set, contractFullName, generatedContracts)).ToArray();
            var hasUnreadableImplementer = unreadable.Any(other => ImplementsGeneratedContract(other, set, contractFullName, generatedContracts));

            // A set reported for its base already says what to do about this language.
            var misdeclared = sets.Where(other => other.Language is not null
                && !String.Equals(other.MetadataName, set.MetadataName, StringComparison.Ordinal)
                && String.Equals(other.Namespace, set.Namespace, StringComparison.Ordinal)
                && (IsDerivedFromOtherLanguage(other, generatedContracts) || IsMissingContract(other, defaultLanguage))
                && String.Equals(TranslationsDiscovery.DeriveBaseName(other.Name, other.Language), baseName, StringComparison.Ordinal)).ToArray();

            // Not with a set of a language the build cannot read among them: it may be the set of any language.
            if (findings is not null && !hasUnreadableImplementer)
            {
                foreach (var code in declared.Codes)
                {
                    // A set of the language itself, also for a region of a language that has a set (en-GB next to en):
                    // listing it says it has wording of its own, as every inline text takes one. At run time the set
                    // of its language would serve it, but so it would without listing it. An excluded set counts as
                    // well: it gets no LOC006 or LOC007 to say what is wrong with it, so only the language it plainly
                    // is of is not reported as missing. The default-language set is the set of its own language.
                    if (IsSetOf(set, code)
                        || implementers.Any(other => IsSetOf(other, code))
                        || excludedImplementers.Any(other => IsSetOf(other, code))
                        || misdeclared.Any(other => IsSetOf(other, code)))
                    {
                        continue;
                    }

                    findings.Add(new TranslationSetsFinding(
                        TranslationSetsRule.MissingLanguage,
                        set.Location,
                        new EquatableArray<string>([baseName, code, SuggestSet($"{baseName}{SupportedLanguages.ToTypeSuffix(code)}", contractName, code, implementers.Prepend(set))])));
                }
            }

            var translators = implementers
                .Concat(excludedImplementers)
                .OrderBy(static other => other.MetadataName, StringComparer.Ordinal)
                .ToArray();

            // Internal: an assembly referencing this one could not implement it anyway.
            if (set.IsPublic)
            {
                provided.Add(new ProvidedTranslationSet(contractFullName, set.Language!));
                foreach (var translator in translators)
                    provided.Add(new ProvidedTranslationSet(contractFullName, translator.Language!));

                if (hasUnreadableImplementer)
                    provided.Add(new ProvidedTranslationSet(contractFullName, ProvidedTranslationSet.UnreadableLanguage));
            }

            var translations = translators
                .Prepend(set)
                .Select(static other => new TranslationLink(other.Language!, other.Namespace, other.Name, other.StringProperties, other.InheritedTexts, other.Wordings, other.IsExcluded))
                .ToImmutableArray();

            contracts.Add(new TranslationContract(
                Namespace: set.Namespace,
                ClassName: set.Name,
                IsRecord: set.IsRecord,
                IsPublic: set.IsPublic,
                IsObsolete: set.IsObsolete,
                ContractName: contractName,
                TextsName: baseName,
                KeyPrefix: $"{DeriveStem(baseName)}.",
                Members: set.Texts,
                Translations: new EquatableArray<TranslationLink>(translations)));
        }
    }

    /// <summary>
    /// The name to give a default-language set whose name does not end in the suffix of its language: the suffix in
    /// place of an ending that spells it in another letter case (<c>ShopTranslationsEn</c> becomes
    /// <c>ShopTranslationsEN</c>), and else the suffix added (<c>ShopTexts</c> becomes <c>ShopTextsEN</c>).
    /// </summary>
    private static string SuggestNameEndingIn(string name, string suffix)
    {
        var suggestedName = name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? name.Substring(startIndex: 0, name.Length - suffix.Length) + suffix
            : name + suffix;

        return suggestedName;
    }

    /// <summary>
    /// Whether a set, other than the default-language set itself, implements the contract generated from it, whatever
    /// its language.
    /// </summary>
    // The generator's own contract does not exist yet, so a set implementing it lists an unresolved name, itself or
    // through a class it derives from, which binds to this contract only if the compiler's lookup reaches this
    // namespace before any other contract of that name. A set deriving from the default set implements it too.
    private static bool ImplementsGeneratedContract(
        DiscoveredTranslations other,
        DiscoveredTranslations defaultSet,
        string contractFullName,
        ImmutableArray<string> generatedContracts)
    {
        var implementsGeneratedContract = !String.Equals(other.MetadataName, defaultSet.MetadataName, StringComparison.Ordinal)
            && (other.AncestorNames.AsImmutableArray().Contains(defaultSet.MetadataName, StringComparer.Ordinal)
                || other.UnresolvedBaseTypes.Any(baseType => String.Equals(TranslationsDiscovery.FindGeneratedContract(baseType, generatedContracts), contractFullName, StringComparison.Ordinal)));

        return implementsGeneratedContract;
    }

    /// <summary>
    /// Holds the contracts written by hand in this assembly to its languages, as a generated contract is: each must have
    /// a set for every language. Reported at the attribute, like a referenced contract, since the gap is a language the
    /// attribute lists. Only contracts this assembly has a set of are seen; a contract with a set whose language the
    /// build cannot read is skipped. An excluded set counts for the language it serves, but no contract is seen through
    /// it alone.
    /// </summary>
    private static void AnalyzeOwnContracts(
        ImmutableArray<DiscoveredTranslations> sets,
        ImmutableArray<DiscoveredTranslations> excluded,
        ImmutableArray<DiscoveredTranslations> unreadable,
        DeclaredLanguages declared,
        string defaultLanguage,
        ImmutableArray<TranslationSetsFinding>.Builder findings)
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

            // A set reported for listing ITranslationSet instead of this contract already says what to do about its
            // language.
            var misdeclared = sets.Where(other => other.Language is not null
                && String.Equals(other.Namespace, contractNamespace, StringComparison.Ordinal)
                && IsMissingContract(other, defaultLanguage)
                && String.Equals(TranslationsDiscovery.DeriveBaseName(other.Name, other.Language), baseName, StringComparison.Ordinal)).ToArray();

            foreach (var code in declared.Codes)
            {
                // A set of the language itself, as for a generated contract.
                if (members.Any(set => IsSetOf(set, code))
                    || excludedMembers.Any(set => IsSetOf(set, code))
                    || misdeclared.Any(set => IsSetOf(set, code)))
                {
                    continue;
                }

                findings.Add(new TranslationSetsFinding(
                    TranslationSetsRule.MissingLanguage,
                    declared.Location,
                    new EquatableArray<string>([contract, code, SuggestSet(SuggestSetName(contract, code), simpleName, code, members)])));
            }
        }
    }

    private static void AnalyzeSets(
        ImmutableArray<DiscoveredTranslations> sets,
        EquatableArray<string> codes,
        string defaultLanguage,
        ImmutableArray<string> generatedContracts,
        ImmutableArray<TranslationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
            if (set.Language is not { } language)
                continue;

            if (!IsListed(language, codes))
            {
                findings.Add(UnlistedLanguage(set, language));
            }
            else if (IsDerivedFromOtherLanguage(set, generatedContracts))
            {
                findings.Add(DerivedFromOtherLanguage(set, language));
            }
            else if (IsMissingContract(set, defaultLanguage))
            {
                var baseName = TranslationsDiscovery.DeriveBaseName(set.Name, language) ?? set.Name;
                var defaultSet = $"{baseName}{SupportedLanguages.ToTypeSuffix(defaultLanguage)}";

                findings.Add(new TranslationSetsFinding(
                    TranslationSetsRule.MissingContract,
                    set.Location,
                    new EquatableArray<string>([set.Name, language, $"I{baseName}", defaultSet]),
                    // Only a contract this build generates: a fix naming one that does not exist would not compile.
                    SuggestedName: generatedContracts.Contains(TranslationsDiscovery.GetContractFullName(set.Namespace, baseName), StringComparer.Ordinal) ? $"I{baseName}" : null));
            }
        }
    }

    /// <summary>
    /// A set in another language than the set it derives from keeps that language for every text it does not override.
    /// A regional set on top of its language (<c>en-GB</c> on <c>en</c>) is fine. A base that implements
    /// <c>ITranslationSet</c> only through a contract generated here is a set once that contract exists.
    /// </summary>
    private static bool IsDerivedFromOtherLanguage(DiscoveredTranslations set, ImmutableArray<string> generatedContracts)
    {
        var isDerived = set.BaseSetLanguage is { } baseLanguage
               && (set.BaseSetContracts.Length is 0 || set.BaseSetContracts.Any(baseType => TranslationsDiscovery.FindGeneratedContract(baseType, generatedContracts) is not null))
               && !SupportedLanguages.Serves(baseLanguage, set.Language!);

        return isDerived;
    }

    private static TranslationSetsFinding DerivedFromOtherLanguage(DiscoveredTranslations set, string language)
    {
        var isDerived = new TranslationSetsFinding(
            TranslationSetsRule.DerivedFromOtherLanguage,
            set.Location,
            new EquatableArray<string>([set.Name, language, set.BaseSetName!, set.BaseSetLanguage!]));

        return isDerived;
    }

    private static TranslationSetsFinding UnlistedLanguage(DiscoveredTranslations set, string language)
    {
        var advice = GuessLongCode(set.Name) is { } meant
            ? $"If it is meant for '{meant}', list '{meant}' in the attribute, which its name then gives it, or implement 'string ITranslationSet.LanguageCode => \"{meant}\";'. Otherwise add '{language}' to the attribute, or remove the set."
            : "Add the language to the attribute, or remove the set.";

        var unlistedLanguageFinding = new TranslationSetsFinding(
            TranslationSetsRule.UnlistedLanguage,
            set.Location,
            new EquatableArray<string>([set.Name, language, advice]));

        return unlistedLanguageFinding;
    }

    /// <summary>
    /// A set that lists <c>ITranslationSet</c> in another language than the default, and implements no contract
    /// besides, so no text would ever read it. Without a default language to go by, no set is taken for one.
    /// </summary>
    private static bool IsMissingContract(DiscoveredTranslations set, string? defaultLanguage)
    {
        var isMissingContract = defaultLanguage is not null
               && set is { ListsTranslations: true, BaseSetName: null, UnresolvedBaseTypes.Length: 0 }
               && !TranslationsDiscovery.ImplementsContract(set)
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
    /// A longer code a set's name looks like it ends in, for the advice of <c>LOC005</c> only: a language of two or three
    /// letters followed by a region (<c>ShopTranslationsEnGB</c>: <c>en-GB</c>) or a script (<c>ShopTranslationsZhHans</c>:
    /// <c>zh-Hans</c>). A name gives such a code only when the attribute lists it, as a name alone cannot tell it from
    /// <c>AdminUiNL</c>.
    /// </summary>
    private static string? GuessLongCode(string setName)
    {
        var match = LongCodeSuffix.Match(setName);

        var longCode = match.Success ? $"{match.Groups[1].Value.ToLowerInvariant()}-{match.Groups[2].Value}" : null;
        return longCode;
    }

    /// <summary>
    /// Whether a set is of a language itself, and not of one that only serves it: a set of <c>en</c> is no set of
    /// <c>en-GB</c>.
    /// </summary>
    private static bool IsSetOf(DiscoveredTranslations set, string code)
    {
        var isSetOf = String.Equals(set.Language, code, StringComparison.OrdinalIgnoreCase);
        return isSetOf;
    }

    /// <summary>
    /// The set to add for a language a contract of this assembly misses, as the diagnostic names it. A region of a
    /// language that has a set derives from that set, to hold only the texts that differ:
    /// <c>'class ShopTranslationsEnGB : ShopTranslationsEN'</c>. Any other language implements the contract.
    /// </summary>
    private static string SuggestSet(string setName, string contract, string code, IEnumerable<DiscoveredTranslations> sets)
    {
        // The nearest one: zh-Hans before zh for zh-Hans-CN. A sealed set has nothing deriving from it.
        var above = sets
            .Where(set => set is { Language: not null, IsSealed: false } && !IsSetOf(set, code) && SupportedLanguages.Serves(set.Language, code))
            .OrderByDescending(static set => set.Language!.Length)
            .ThenBy(static set => set.MetadataName, StringComparer.Ordinal)
            .FirstOrDefault();

        if (above.Language is null)
            return SupportedLanguages.SuggestSet(setName, contract);

        var suggestion = $"'{(above.IsRecord ? "record" : "class")} {setName} : {above.Name}', which keeps the wording of '{above.Language}' for every text it does not override";
        return suggestion;
    }

    /// <summary>
    /// The set an app would add for a contract of a library: <c>GridTranslationsDE</c> for <c>Lib.IGridTranslations</c>.
    /// </summary>
    private static string SuggestSetName(string contract, string code)
    {
        var setName = $"{GetContractBaseName(contract)}{SupportedLanguages.ToTypeSuffix(code)}";
        return setName;
    }

    /// <summary>
    /// The name a contract's sets share before their language suffix: <c>GridTranslations</c> for
    /// <c>Lib.IGridTranslations</c>.
    /// </summary>
    private static string GetContractBaseName(string contract)
    {
        var name = contract.Substring(contract.LastIndexOf('.') + 1);

        var baseName = name.Length > 1 && name[0] == 'I' && Char.IsUpper(name[1]) ? name.Substring(1) : name;
        return baseName;
    }

    /// <summary>
    /// The base name without a trailing <c>Translations</c>: <c>ShopTranslations</c> gives <c>Shop</c>, the stem of the
    /// resource keys (<c>Shop.Checkout</c>). A base name that is nothing but <c>Translations</c> keeps it, so the key
    /// prefix is never empty.
    /// </summary>
    private static string DeriveStem(string baseName)
    {
        const string suffix = "Translations";

        var stem = baseName.Length > suffix.Length && baseName.EndsWith(suffix, StringComparison.Ordinal)
            ? baseName.Substring(startIndex: 0, baseName.Length - suffix.Length)
            : baseName;

        return stem;
    }
}

/// <summary>
/// Everything a generated contract is built from: the default-language set, whether the generated types are public like
/// it or internal, whether naming it is warned about as obsolete, the derived names, its text members in declaration
/// order, and the sets translating them.
/// </summary>
internal sealed record TranslationContract(
    string Namespace,
    string ClassName,
    bool IsRecord,
    bool IsPublic,
    bool IsObsolete,
    string ContractName,
    string TextsName,
    string KeyPrefix,
    EquatableArray<TranslationsMember> Members,
    EquatableArray<TranslationLink> Translations);

/// <summary>
/// A set translating a generated contract, for the crefs on its texts: its language, a culture name, its namespace and
/// name, by which the contract's file names it, the properties a cref can name on it, the texts it inherits, which
/// a cref names on the class declaring them, the wording it gives each text that returns a constant, which the
/// contract's file shows next to the link, and whether it is marked <c>[ExcludeFromTranslationDiscovery]</c>.
/// </summary>
internal readonly record struct TranslationLink(
    string Language,
    string Namespace,
    string Name,
    EquatableArray<string> StringProperties,
    EquatableArray<InheritedText> InheritedTexts,
    EquatableArray<TextWording> Wordings,
    bool IsExcluded);

/// <summary>
/// That an assembly holds a set of a public contract for a language, or for <see cref="UnreadableLanguage"/>.
/// </summary>
internal readonly record struct ProvidedTranslationSet(
    string Contract,
    string Language)
{
    /// <summary>
    /// What stands for the language of a set whose language its build could not read, as its
    /// <c>ITranslationSet.LanguageCode</c> is no constant or comes from another assembly. It is no culture name, so it
    /// serves no language, and a build that knows it takes the contract to miss none.
    /// </summary>
    public const string UnreadableLanguage = "*";
}

/// <summary>
/// What a referenced assembly says about its languages: those it declares, its sets of public contracts, and whether it
/// writes texts inline.
/// </summary>
internal readonly record struct ReferencedTranslations(
    string AssemblyName,
    EquatableArray<string> Languages,
    EquatableArray<ProvidedTranslationSet> Sets,
    bool HasInlineTexts);

/// <summary>
/// A diagnostic to report: which rule, where, and the arguments of its message. A finding a name resolves carries it,
/// for the code fix to apply: the name the class should carry (<c>LOC002</c>), or the contract it should implement
/// (<c>LOC007</c>).
/// </summary>
internal readonly record struct TranslationSetsFinding(
    TranslationSetsRule Rule,
    DiagnosticLocation Location,
    EquatableArray<string> Arguments,
    string? SuggestedName = null);

/// <summary>
/// The rules <see cref="TranslationSetsGenerator"/> reports.
/// </summary>
internal enum TranslationSetsRule
{
    /// <summary>
    /// <c>LOC001</c>: a set lists <c>ITranslationSet</c> in an assembly that declares no languages.
    /// </summary>
    NoLanguagesDeclared,

    /// <summary>
    /// <c>LOC002</c>: a default-language set the generator cannot extend.
    /// </summary>
    CannotExtend,

    /// <summary>
    /// <c>LOC004</c>: a contract of this assembly, generated or written by hand, has no set for a supported language.
    /// </summary>
    MissingLanguage,

    /// <summary>
    /// <c>LOC005</c>: a set serves a language the assembly does not list, a set marked
    /// <c>[ExcludeFromTranslationDiscovery]</c> included.
    /// </summary>
    UnlistedLanguage,

    /// <summary>
    /// <c>LOC006</c>: a set derives from a set of another language, in any assembly.
    /// </summary>
    DerivedFromOtherLanguage,

    /// <summary>
    /// <c>LOC007</c>: a set lists <c>ITranslationSet</c> instead of implementing its contract.
    /// </summary>
    MissingContract,

    /// <summary>
    /// <c>LOC008</c>: a referenced contract has no set for a supported language.
    /// </summary>
    LibraryMissingLanguage,

    /// <summary>
    /// <c>LOC009</c>: a referenced library writes texts inline, and lacks a supported language.
    /// </summary>
    LibraryCannotBeTranslated,

    /// <summary>
    /// <c>LOC014</c>: a text of a default-language set is named like a member of the generated texts class.
    /// </summary>
    ReservedTextName,
}
