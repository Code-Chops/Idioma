using System.Text.RegularExpressions;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Holds the sets of an assembly against its languages: what to generate, and what to report. Plain values in, plain
/// values out, so a pipeline step is reused while nothing relevant changes.
/// </summary>
internal sealed record LocalizationSetsAnalysis(
    EquatableArray<LocalizationContract> Contracts,
    EquatableArray<ProvidedLocalizationSet> ProvidedSets,
    EquatableArray<LocalizationSetsFinding> Findings)
{
    /// <summary>
    /// A name ending in what looks like a longer code (<see cref="GuessLongCode"/>): a capitalized language of two or
    /// three letters, then a region of two letters or a script of four, starting with a capital.
    /// </summary>
    private static readonly Regex LongCodeSuffix = new("(?<=.)([A-Z][a-z]{1,2})([A-Z][a-z]{3}|[A-Z][A-Za-z])$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Whether a text can't become a member of the generated texts class: it carries the class's own name, which C#
    /// allows no member (<c>LOC205</c>). The class declares no member of its own, so that no other name is taken.
    /// </summary>
    public static bool IsReservedTextName(string name, string textsName)
    {
        var isReserved = String.Equals(name, textsName, StringComparison.Ordinal);
        return isReserved;
    }

    public static LocalizationSetsAnalysis Analyze(
        EquatableArray<DiscoveredLocalization> discovered,
        EquatableArray<UnextendableSet> unextendable,
        DeclaredLanguages? languages,
        EquatableArray<ContractBinding> bindings)
    {
        var candidates = Merge(discovered);
        var generatedContracts = LocalizationDiscovery.GetGeneratedContracts(candidates, languages, bindings);
        var sets = candidates.Where(static candidate => !candidate.IsExcluded).ToImmutableArray();

        // A set marked [ExcludeFromLocalizationDiscovery] is registered by hand, so nothing is reported about it, but it
        // does translate its contract: it counts as a set of its language, here and for the assemblies referencing this
        // one. Otherwise the fix LOC400 names for a set needing constructor arguments would end in LOC201 or LOC600,
        // asking for the very set that exists.
        var excludedSets = candidates.Where(static candidate => candidate.IsExcluded).ToImmutableArray();
        var excluded = excludedSets.Where(static set => set.Language is not null).ToImmutableArray();

        // A set whose language the build can't read (its LanguageCode is no constant, or comes from another assembly)
        // may be the set of any language. So a contract it implements is not said to miss one, here or in an assembly
        // referencing this one: that would ask for a set that exists.
        var unreadable = sets.Concat(excludedSets).Where(static set => set.Language is null).ToImmutableArray();
        var findings = ImmutableArray.CreateBuilder<LocalizationSetsFinding>();
        var contracts = ImmutableArray.CreateBuilder<LocalizationContract>();
        var provided = ImmutableArray.CreateBuilder<ProvidedLocalizationSet>();

        foreach (var set in sets.Concat(excludedSets))
            foreach (var contract in set.ContractNames)
                provided.Add(new ProvidedLocalizationSet(contract, set.Language ?? ProvidedLocalizationSet.UnreadableLanguage));

        if (languages is not { } declared)
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
        else if (declared.DefaultLanguage is not { } defaultLanguage)
        {
            // The attribute's first language is one the build can't use, which LOC101 reports. Nothing is held to a
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
                    findings.Add(UnlistedLanguage(set, set.Language!, declared.Codes));

            // A default-language set whose name gives no language starts no contract, so every set that lists the
            // contract fails to compile (CS0246). The name that ends in the default language resolves it.
            foreach (var set in sets)
            {
                if (!LocalizationDiscovery.IsDefaultSetWithoutLanguage(set, generatedContracts))
                    continue;

                var suffix = SupportedLanguages.ToTypeSuffix(defaultLanguage);

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.CannotExtend,
                    set.Location,
                    new EquatableArray<string>([set.Name, $"its name doesn't end in {suffix}, the suffix of the default language, so the build can't read its language"]),
                    SuggestedName: SuggestNameEndingIn(set.Name, suffix)));
            }

            // A default-language set of a shape the generator can't extend would otherwise leave its contract and texts
            // ungenerated without a word, and every other language's set without the contract it implements. Not a
            // class that lists a contract another set starts, such as a test double of the default language: it's a
            // set of that contract, and starts none of its own.
            foreach (var set in unextendable)
            {
                if (String.Equals(set.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase)
                    && !LocalizationDiscovery.ListsOnlyOtherGeneratedContracts(set.UnresolvedBaseTypes, generatedContracts, ownContract: null))
                {
                    findings.Add(new LocalizationSetsFinding(LocalizationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, set.Problem])));
                }
            }
        }

        var analysis = new LocalizationSetsAnalysis(
            new EquatableArray<LocalizationContract>(contracts.ToImmutable()),
            new EquatableArray<ProvidedLocalizationSet>(provided.Distinct().OrderBy(static set => set.Contract, StringComparer.Ordinal).ThenBy(static set => set.Language, StringComparer.Ordinal).ToImmutableArray()),
            new EquatableArray<LocalizationSetsFinding>(findings.ToImmutable()));

        return analysis;
    }

    /// <summary>
    /// What a referenced library leaves untranslated: a contract no language set serves one of this assembly's
    /// languages for, which adding the set fixes, and inline texts lacking a language, which nothing here can fix.
    /// </summary>
    public static EquatableArray<LocalizationSetsFinding> FindLibraryGaps(
        DeclaredLanguages? languages,
        EquatableArray<ProvidedLocalizationSet> ownSets,
        EquatableArray<ReferencedLocalization> references)
    {
        if (languages is not { Codes.Length: > 0 } declared)
            return default;

        var findings = ImmutableArray.CreateBuilder<LocalizationSetsFinding>();
        var referencedSets = references.SelectMany(static reference => reference.Sets).ToArray();
        var contracts = referencedSets.Select(static set => set.Contract).Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal);

        foreach (var contract in contracts)
        {
            // A set whose language its build could not read may be the set of any of them.
            if (IsServed(referencedSets, contract, ProvidedLocalizationSet.UnreadableLanguage) || IsServed(ownSets, contract, ProvidedLocalizationSet.UnreadableLanguage))
                continue;

            foreach (var code in declared.Codes)
            {
                if (IsServed(referencedSets, contract, code) || IsServed(ownSets, contract, code))
                    continue;

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.LibraryMissingLanguage,
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

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.LibraryCannotBeTranslated,
                    declared.Location,
                    new EquatableArray<string>([reference.AssemblyName, code])));
            }
        }

        return new EquatableArray<LocalizationSetsFinding>(findings.ToImmutable());

        static bool IsServed(IEnumerable<ProvidedLocalizationSet> sets, string contract, string code)
        {
            return sets.Any(set => String.Equals(set.Contract, contract, StringComparison.Ordinal) && SupportedLanguages.Serves(set.Language, code));
        }
    }

    /// <summary>
    /// A partial set is discovered once per declaration that has a base list: one set, with the unresolved base types of
    /// all of them.
    /// </summary>
    private static ImmutableArray<DiscoveredLocalization> Merge(EquatableArray<DiscoveredLocalization> discovered)
    {
        var merged = discovered
            .GroupBy(static set => set.MetadataName, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => group.First() with
            {
                UnresolvedBaseTypes = new EquatableArray<UnresolvedBaseType>(group.SelectMany(static set => set.UnresolvedBaseTypes).Distinct().ToImmutableArray()),
                ListsLocalizationSet = group.Any(static set => set.ListsLocalizationSet),
            })
            .ToImmutableArray();

        return merged;
    }

    /// <summary>
    /// Builds the contract of every default-language set, and holds it to the languages: each must have a set of its
    /// own. Without <paramref name="findings"/> nothing is reported, and the contracts are written all the same.
    /// </summary>
    private static void AnalyzeContracts(
        ImmutableArray<DiscoveredLocalization> sets,
        ImmutableArray<DiscoveredLocalization> excluded,
        ImmutableArray<DiscoveredLocalization> unreadable,
        DeclaredLanguages declared,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationContract>.Builder contracts,
        ImmutableArray<ProvidedLocalizationSet>.Builder provided,
        ImmutableArray<LocalizationSetsFinding>.Builder? findings)
    {
        var defaultLanguage = declared.DefaultLanguage;

        // The contracts written so far, each with the set it comes from.
        var written = new Dictionary<string, string>(StringComparer.Ordinal);

        // A hint name tells the files of two contracts apart, and the compiler compares it without regard to case.
        var hintNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var set in sets)
        {
            if (!LocalizationDiscovery.IsDefaultSet(set, defaultLanguage, generatedContracts))
                continue;

            if (LocalizationDiscovery.DeriveBaseName(set.Name, set.Language!) is not { } baseName)
            {
                var suffix = SupportedLanguages.ToTypeSuffix(defaultLanguage ?? set.Language!);

                findings?.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.CannotExtend,
                    set.Location,
                    new EquatableArray<string>([set.Name, $"its name doesn't end in {suffix}, the suffix of the default language, so the generated class would carry the same name"]),
                    SuggestedName: SuggestNameEndingIn(set.Name, suffix)));

                continue;
            }

            var contractName = $"I{baseName}";
            var contractFullName = LocalizationDiscovery.GetContractFullName(set.Namespace, baseName);

            // A contract comes from one set, the first by name. Two sets start one when their names differ only in
            // the case of the language suffix, as ShopLocalizationEn and ShopLocalizationEN do. The second is
            // reported, and gets no contract and no texts of its own, which would carry the names of the first's.
            // Without a default language two sets can start one contract too, the Dutch set listing ILocalizationSet
            // as the English one does. Nothing is reported then, as nothing is held to a default language.
            if (written.TryGetValue(contractFullName, out var firstSet))
            {
                if (defaultLanguage is not null)
                {
                    findings?.Add(new LocalizationSetsFinding(
                        LocalizationSetsRule.CannotExtend,
                        set.Location,
                        new EquatableArray<string>([set.Name, $"'{firstSet}' is the default-language set of '{contractName}' already, and a contract has only one"])));
                }

                continue;
            }

            written.Add(contractFullName, set.Name);

            // The generated texts take their default wording from an instance the generated code creates, so a set it
            // can't create gets no texts. It still gets its contract, which it lists, as every other language does.
            // Reported here, and not as LOC400: that one advises to exclude the set and register it by hand, which a
            // default-language set must never be. A default-language set has its language, so only how it is created
            // can keep it from registering.
            var hasTexts = true;
            if (LocalizationDiscovery.GetCreationProblem(set) is { } problem)
            {
                findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, problem])));
                hasTexts = false;
            }

            // The default-language set lists its contract next to ILocalizationSet, as every set does: the generated
            // texts read its wording through the contract.
            if (!LocalizationDiscovery.ListsGeneratedContract(set, generatedContracts, contractFullName))
            {
                findings?.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.MissingContract,
                    set.Location,
                    new EquatableArray<string>([set.Name, set.Language!, contractName, set.Name]),
                    SuggestedName: contractName));
            }

            foreach (var member in set.Texts)
                if (IsReservedTextName(member.Name, baseName))
                    findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.ReservedTextName, set.Location, new EquatableArray<string>([set.Name, member.Name, baseName])));

            // At the method, which is what to change, and not at the set.
            foreach (var method in set.UnsupportedTexts)
                findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.UnsupportedText, method.Location, new EquatableArray<string>([set.Name, method.Name, method.Problem])));

            var implementers = sets.Where(other => other.Language is not null && ImplementsGeneratedContract(other, set, contractFullName, generatedContracts)).ToArray();
            var excludedImplementers = excluded.Where(other => ImplementsGeneratedContract(other, set, contractFullName, generatedContracts)).ToArray();
            var hasUnreadableImplementer = unreadable.Any(other => ImplementsGeneratedContract(other, set, contractFullName, generatedContracts));

            // A set reported for its base already says what to do about this language.
            var misdeclared = sets.Where(other => other.Language is not null
                && !String.Equals(other.MetadataName, set.MetadataName, StringComparison.Ordinal)
                && String.Equals(other.Namespace, set.Namespace, StringComparison.Ordinal)
                && (IsDerivedFromOtherLanguage(other, generatedContracts) || IsMissingContract(other, defaultLanguage))
                && String.Equals(LocalizationDiscovery.DeriveBaseName(other.Name, other.Language), baseName, StringComparison.Ordinal)).ToArray();

            // Not with a set of a language the build can't read among them: it may be the set of any language.
            if (findings is not null && !hasUnreadableImplementer)
            {
                foreach (var code in declared.Codes)
                {
                    // A set of the language itself, also for a region of a language that has a set (en-GB next to en):
                    // listing it says it has wording of its own, as every inline text takes one. At run time the set
                    // of its language would serve it, but so it would without listing it. An excluded set counts as
                    // well: it gets no LOC203 or LOC204 to say what is wrong with it, so only the language it plainly
                    // is of is not reported as missing. The default-language set is the set of its own language.
                    if (IsSetOf(set, code)
                        || implementers.Any(other => IsSetOf(other, code))
                        || excludedImplementers.Any(other => IsSetOf(other, code))
                        || misdeclared.Any(other => IsSetOf(other, code)))
                    {
                        continue;
                    }

                    var setName = $"{baseName}{SupportedLanguages.ToTypeSuffix(code)}";
                    var (suggestion, newSet) = SuggestSet(setName, contractName, code, set.Namespace, set.IsPublic, implementers.Prepend(set));

                    findings.Add(new LocalizationSetsFinding(
                        LocalizationSetsRule.MissingLanguage,
                        set.Location,
                        new EquatableArray<string>([baseName, code, suggestion]),
                        SuggestedName: setName,
                        NewSet: newSet));
                }
            }

            var otherSets = implementers
                .Concat(excludedImplementers)
                .OrderBy(static other => other.MetadataName, StringComparer.Ordinal)
                .ToArray();

            // Internal: an assembly referencing this one could not implement it anyway.
            if (set.IsPublic)
            {
                provided.Add(new ProvidedLocalizationSet(contractFullName, set.Language!));
                foreach (var otherSet in otherSets)
                    provided.Add(new ProvidedLocalizationSet(contractFullName, otherSet.Language!));

                if (hasUnreadableImplementer)
                    provided.Add(new ProvidedLocalizationSet(contractFullName, ProvidedLocalizationSet.UnreadableLanguage));
            }

            var links = otherSets
                .Prepend(set)
                .Select(static other => new SetLink(other.Language!, other.Namespace, other.Name, other.DeclaredTexts, other.InheritedTexts, other.Wordings, other.IsExcluded))
                .ToImmutableArray();

            contracts.Add(new LocalizationContract(
                HintName: GetHintName(set.Namespace, set.Name, hintNames),
                Namespace: set.Namespace,
                ClassName: set.Name,
                IsPublic: set.IsPublic,
                ContractName: contractName,
                TextsName: baseName,
                KeyPrefix: GetKeyPrefix(set.Namespace, baseName),
                Members: set.Texts,
                Sets: new EquatableArray<SetLink>(links),
                HasTexts: hasTexts,
                Usings: set.Usings));
        }
    }

    /// <summary>
    /// The name of the file a contract is written to, without its extension: the full name of the default-language set,
    /// as in <c>MyApp.Localization.ShopLocalizationEn</c>. A name that another contract's file has already, but for
    /// its letter case, gets a number: <c>shop.TextsEn_2</c> next to <c>Shop.TextsEn</c>.
    /// </summary>
    // Qualified by namespace: two sets sharing a simple name must not collide on the hint name. Without the @ of an
    // escaped keyword (namespace Shop.@event), which a hint name can't hold. The compiler compares hint names without
    // regard to case, and refuses the second of two that are equal to it, while C# tells Shop from shop. The contracts
    // come in the order of their sets' names, so the same file gets the number on every build.
    private static string GetHintName(string @namespace, string className, HashSet<string> taken)
    {
        var name = (@namespace.Length > 0 ? $"{@namespace}.{className}" : className).Replace("@", "");
        var hintName = name;

        for (var number = 2; !taken.Add(hintName); number++)
            hintName = $"{name}_{number}";

        return hintName;
    }

    /// <summary>
    /// The name to give a default-language set whose name does not end in the suffix of its language: the suffix in
    /// place of an ending that spells it without a capital (<c>ShopLocalizationen</c> becomes
    /// <c>ShopLocalizationEn</c>), and else the suffix added (<c>ShopTexts</c> becomes <c>ShopTextsEn</c>).
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
    // through a class it derives from, which names this contract (LocalizationDiscovery.FindGeneratedContract). A set
    // deriving from the default set implements it too.
    private static bool ImplementsGeneratedContract(
        DiscoveredLocalization other,
        DiscoveredLocalization defaultSet,
        string contractFullName,
        GeneratedContracts generatedContracts)
    {
        var implementsGeneratedContract = !String.Equals(other.MetadataName, defaultSet.MetadataName, StringComparison.Ordinal)
            && (other.AncestorNames.AsImmutableArray().Contains(defaultSet.MetadataName, StringComparer.Ordinal)
                || other.UnresolvedBaseTypes.Any(baseType => String.Equals(LocalizationDiscovery.FindGeneratedContract(baseType, generatedContracts), contractFullName, StringComparison.Ordinal)));

        return implementsGeneratedContract;
    }

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
                var (suggestion, newSet) = SuggestSet(setName, simpleName, code, contractNamespace, members.Any(static set => set.IsPublic), members);

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.MissingLanguage,
                    declared.Location,
                    new EquatableArray<string>([contract, code, suggestion]),
                    SuggestedName: setName,
                    NewSet: newSet));
            }
        }
    }

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
            else if (IsMissingContract(set, defaultLanguage))
            {
                var baseName = LocalizationDiscovery.DeriveBaseName(set.Name, language) ?? set.Name;
                var defaultSet = $"{baseName}{SupportedLanguages.ToTypeSuffix(defaultLanguage)}";

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.MissingContract,
                    set.Location,
                    new EquatableArray<string>([set.Name, language, $"I{baseName}", defaultSet]),
                    // Only a contract this build generates: a fix naming one that does not exist would not compile.
                    SuggestedName: generatedContracts.Contains(LocalizationDiscovery.GetContractFullName(set.Namespace, baseName)) ? $"I{baseName}" : null));
            }
        }
    }

    /// <summary>
    /// A set in another language than the set it derives from keeps that language for every text it does not override.
    /// A regional set on top of its language (<c>en-GB</c> on <c>en</c>) is fine. A base that implements
    /// <c>ILocalizationSet</c> only through a contract generated here is a set once that contract exists.
    /// </summary>
    private static bool IsDerivedFromOtherLanguage(DiscoveredLocalization set, GeneratedContracts generatedContracts)
    {
        var isDerived = set.BaseSetLanguage is { } baseLanguage
               && (set.BaseSetContracts.Length is 0 || set.BaseSetContracts.Any(baseType => LocalizationDiscovery.FindGeneratedContract(baseType, generatedContracts) is not null))
               && !SupportedLanguages.Serves(baseLanguage, set.Language!);

        return isDerived;
    }

    private static LocalizationSetsFinding DerivedFromOtherLanguage(DiscoveredLocalization set, string language)
    {
        var isDerived = new LocalizationSetsFinding(
            LocalizationSetsRule.DerivedFromOtherLanguage,
            set.Location,
            new EquatableArray<string>([set.Name, language, set.BaseSetName!, set.BaseSetLanguage!]));

        return isDerived;
    }

    private static LocalizationSetsFinding UnlistedLanguage(DiscoveredLocalization set, string language, EquatableArray<string> codes)
    {
        var advice = GuessLongCode(set.Name, codes) is { } meant
            ? $"If it is meant for '{meant}', list '{meant}' in the attribute, which its name then gives it, or implement 'string ILocalizationSet.LanguageCode => \"{meant}\";'. Otherwise add '{language}' to the attribute, or remove the set."
            : "Add the language to the attribute, or remove the set.";

        var unlistedLanguageFinding = new LocalizationSetsFinding(
            LocalizationSetsRule.UnlistedLanguage,
            set.Location,
            new EquatableArray<string>([set.Name, language, advice]));

        return unlistedLanguageFinding;
    }

    /// <summary>
    /// A set that lists <c>ILocalizationSet</c> in another language than the default, and implements no contract
    /// besides, so no text would ever read it. Without a default language to go by, no set is taken for one.
    /// </summary>
    private static bool IsMissingContract(DiscoveredLocalization set, string? defaultLanguage)
    {
        var isMissingContract = defaultLanguage is not null
               && set is { ListsLocalizationSet: true, BaseSetName: null, UnresolvedBaseTypes.Length: 0 }
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

    /// <summary>
    /// The set to add for a language a contract of this assembly misses: as the diagnostic names it, and as the code fix
    /// writes it (<see cref="NewLocalizationSet"/>). A region of a language that has a set derives from that set, to hold
    /// only the texts that differ: <c>'class ShopLocalizationEnGb : ShopLocalizationEn'</c>. Any other language
    /// implements the contract.
    /// </summary>
    /// <param name="setName">The name of the set to add.</param>
    /// <param name="contract">The contract as the namespace of the new set names it.</param>
    /// <param name="code">The language the set is for.</param>
    /// <param name="namespace">The namespace of the new set: that of the contract.</param>
    /// <param name="isPublic">Whether a set that implements the contract is public, as the contract's sets are.</param>
    /// <param name="sets">The sets of the contract, among which a set of a language above the region.</param>
    private static (string Suggestion, NewLocalizationSet NewSet) SuggestSet(string setName, string contract, string code, string @namespace, bool isPublic,
        IEnumerable<DiscoveredLocalization> sets)
    {
        // The nearest one: zh-Hans before zh for zh-Hans-CN. A sealed set has nothing deriving from it.
        var above = sets
            .Where(set => set is { Language: not null, IsSealed: false } && !IsSetOf(set, code) && SupportedLanguages.Serves(set.Language, code))
            .OrderByDescending(static set => set.Language!.Length)
            .ThenBy(static set => set.MetadataName, StringComparer.Ordinal)
            .FirstOrDefault();

        if (above.Language is null)
        {
            var declaration = $"{(isPublic ? "public" : "internal")} class {setName} : ILocalizationSet, {contract}";
            return (SupportedLanguages.SuggestSet(setName, contract), new NewLocalizationSet(@namespace, declaration));
        }

        // As accessible as the set it derives from, which a derived class can't exceed. Named by its full name where it
        // lies in another namespace than the new set.
        var keyword = above.IsRecord ? "record" : "class";
        var baseClass = String.Equals(above.Namespace, @namespace, StringComparison.Ordinal)
            ? LocalizationSetsGenerator.EscapeIdentifier(above.Name)
            : above.FullyQualifiedName;

        var suggestion = $"'{keyword} {setName} : {above.Name}', which keeps the wording of '{above.Language}' for every text it doesn't override";
        var derived = $"{(above.IsPublic ? "public" : "internal")} {keyword} {setName} : {baseClass}";

        return (suggestion, new NewLocalizationSet(@namespace, derived));
    }

    /// <summary>
    /// The set an app would add for a contract of a library: <c>GridLocalizationDe</c> for <c>Lib.IGridLocalization</c>.
    /// </summary>
    private static string SuggestSetName(string contract, string code)
    {
        var setName = $"{GetContractBaseName(contract)}{SupportedLanguages.ToTypeSuffix(code)}";
        return setName;
    }

    /// <summary>
    /// The name a contract's sets share before their language suffix: <c>GridLocalization</c> for
    /// <c>Lib.IGridLocalization</c>.
    /// </summary>
    private static string GetContractBaseName(string contract)
    {
        var name = contract.Substring(contract.LastIndexOf('.') + 1);

        var baseName = name.Length > 1 && name[0] == 'I' && Char.IsUpper(name[1]) ? name.Substring(1) : name;
        return baseName;
    }

    /// <summary>
    /// What the resource key of every text of a contract starts with: the full name of the generated class that holds
    /// the texts, and a dot. <c>MyApp.Localization.ShopLocalization.</c> for <c>ShopLocalizationEn</c> in the namespace
    /// <c>MyApp.Localization</c>, so that a key is the full name of its text
    /// (<c>MyApp.Localization.ShopLocalization.Checkout</c>).
    /// </summary>
    // The full name, and not the class alone or a part of it: two sets of one name in different namespaces, or in two
    // assemblies, then have keys of their own, as their generated classes have names of their own. Without the @ of an
    // escaped keyword (namespace Shop.@event): a key is a name, not code.
    private static string GetKeyPrefix(string @namespace, string baseName)
    {
        var prefix = @namespace.Length > 0 ? $"{@namespace.Replace("@", "")}.{baseName}." : $"{baseName}.";
        return prefix;
    }
}

/// <summary>
/// Everything a generated contract is built from: the name of its file, the default-language set, whether the generated
/// types are public like it or internal, the derived names, its text members in declaration order, the sets translating
/// them, whether the generated class holds the texts, and the using directives its file repeats
/// (<see cref="RepeatedUsings"/>). The class doesn't hold the texts when the generated code can't create the set for
/// their default wording (<c>LOC200</c>).
/// </summary>
internal sealed record LocalizationContract(
    string HintName,
    string Namespace,
    string ClassName,
    bool IsPublic,
    string ContractName,
    string TextsName,
    string KeyPrefix,
    EquatableArray<LocalizationMember> Members,
    EquatableArray<SetLink> Sets,
    bool HasTexts,
    RepeatedUsings Usings);

/// <summary>
/// A set translating a generated contract, for the crefs on its texts: its language, a culture name, its namespace and
/// name, by which the contract's file names it, the texts a cref can name on it, the texts it inherits, which
/// a cref names on the class declaring them, the wording it gives each text that returns a constant or takes values,
/// which the contract's file shows next to the link, and whether it is marked
/// <c>[ExcludeFromLocalizationDiscovery]</c>.
/// </summary>
internal readonly record struct SetLink(
    string Language,
    string Namespace,
    string Name,
    EquatableArray<string> DeclaredTexts,
    EquatableArray<InheritedText> InheritedTexts,
    EquatableArray<TextWording> Wordings,
    bool IsExcluded);

/// <summary>
/// That an assembly holds a set of a public contract for a language, or for <see cref="UnreadableLanguage"/>.
/// </summary>
internal readonly record struct ProvidedLocalizationSet(
    string Contract,
    string Language)
{
    /// <summary>
    /// What stands for the language of a set whose language its build could not read, as its
    /// <c>ILocalizationSet.LanguageCode</c> is no constant or comes from another assembly. It is no culture name, so it
    /// serves no language, and a build that knows it takes the contract to miss none.
    /// </summary>
    public const string UnreadableLanguage = "*";
}

/// <summary>
/// What a referenced assembly says about its languages: those it declares, its sets of public contracts, and whether it
/// writes texts inline.
/// </summary>
internal readonly record struct ReferencedLocalization(
    string AssemblyName,
    EquatableArray<string> Languages,
    EquatableArray<ProvidedLocalizationSet> Sets,
    bool HasInlineTexts);

/// <summary>
/// A diagnostic to report: which rule, where, and the arguments of its message. A finding a name resolves carries it,
/// for the code fix to apply: the name the class should carry (<c>LOC200</c>), the contract it should implement
/// (<c>LOC204</c>), or the set to add (<c>LOC201</c>), which also carries how to write that set.
/// </summary>
internal readonly record struct LocalizationSetsFinding(
    LocalizationSetsRule Rule,
    DiagnosticLocation Location,
    EquatableArray<string> Arguments,
    string? SuggestedName = null,
    NewLocalizationSet? NewSet = null);

/// <summary>
/// A set the code fix of <c>LOC201</c> adds in a file of its own: its namespace, empty for the global one, and its
/// declaration without a body, such as <c>public class ShopLocalizationDe : ILocalizationSet, IShopLocalization</c>.
/// </summary>
internal readonly record struct NewLocalizationSet(
    string Namespace,
    string Declaration);

/// <summary>
/// The rules <see cref="LocalizationSetsGenerator"/> reports.
/// </summary>
internal enum LocalizationSetsRule
{
    /// <summary>
    /// <c>LOC100</c>: a set lists <c>ILocalizationSet</c> in an assembly that declares no languages.
    /// </summary>
    NoLanguagesDeclared,

    /// <summary>
    /// <c>LOC200</c>: a default-language set the generator can't use.
    /// </summary>
    CannotExtend,

    /// <summary>
    /// <c>LOC201</c>: a contract of this assembly, generated or written by hand, has no set for a supported language.
    /// </summary>
    MissingLanguage,

    /// <summary>
    /// <c>LOC202</c>: a set serves a language the assembly does not list, a set marked
    /// <c>[ExcludeFromLocalizationDiscovery]</c> included.
    /// </summary>
    UnlistedLanguage,

    /// <summary>
    /// <c>LOC203</c>: a set derives from a set of another language, in any assembly.
    /// </summary>
    DerivedFromOtherLanguage,

    /// <summary>
    /// <c>LOC204</c>: a set lists <c>ILocalizationSet</c> without implementing its contract.
    /// </summary>
    MissingContract,

    /// <summary>
    /// <c>LOC600</c>: a referenced contract has no set for a supported language.
    /// </summary>
    LibraryMissingLanguage,

    /// <summary>
    /// <c>LOC601</c>: a referenced library writes texts inline, and lacks a supported language.
    /// </summary>
    LibraryCannotBeTranslated,

    /// <summary>
    /// <c>LOC205</c>: a text of a default-language set is named like a member of the generated texts class.
    /// </summary>
    ReservedTextName,

    /// <summary>
    /// <c>LOC300</c>: a method of a default-language set has the shape of a text that takes values, but can't be one.
    /// </summary>
    UnsupportedText,
}
