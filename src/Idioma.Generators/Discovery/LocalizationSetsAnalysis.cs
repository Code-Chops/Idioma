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
        var unreadable = sets.Concat(excludedSets).Where(static set => set is { Language: null, NameGivesNoLanguage: false }).ToImmutableArray();
        var findings = ImmutableArray.CreateBuilder<LocalizationSetsFinding>();
        var contracts = ImmutableArray.CreateBuilder<LocalizationContract>();
        var provided = ImmutableArray.CreateBuilder<ProvidedLocalizationSet>();

        foreach (var set in sets.Concat(excludedSets))
        {
            if (set.NameGivesNoLanguage)
                continue;

            foreach (var contract in set.ContractNames)
                provided.Add(new ProvidedLocalizationSet(contract, set.Language ?? ProvidedLocalizationSet.UnreadableLanguage));
        }

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
            AnalyzeContracts(sets, excluded, unreadable, declared, generatedContracts, contracts, provided, findings: null, cancellationToken);

            // What needs no language list is still reported, as it is without the attribute.
            foreach (var set in sets)
                if (set.Language is { } language && IsDerivedFromOtherLanguage(set, generatedContracts))
                    findings.Add(DerivedFromOtherLanguage(set, language));
        }
        else
        {
            AnalyzeContracts(sets, excluded, unreadable, declared, generatedContracts, contracts, provided, findings, cancellationToken);
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

            // A default-language set that implements a contract already, such as a library's, translates or rewords it,
            // and starts none of its own. When it also lists the contract named after it, which binds to nothing, that
            // contract is the one it meant to start, and every set that lists it fails to compile (CS0246).
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

            // A default-language set of a shape the generator can't extend would otherwise leave its contract and texts
            // ungenerated without a word, and every other language's set without the contract it implements. Not a
            // class that lists a contract another set starts, such as a test double of the default language: it's a
            // set of that contract, and starts none of its own.
            foreach (var set in unextendable)
            {
                if (String.Equals(set.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase)
                    && !LocalizationDiscovery.ListsOnlyOtherGeneratedContracts(set.UnresolvedBaseTypes, generatedContracts, ownContract: null))
                {
                    findings.Add(new LocalizationSetsFinding(LocalizationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, set.Problem, "Make it a top-level class that isn't generic, abstract, or file-local."])));
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

            // ILocalizationSet<TEnum> for the contract of an enum, which every set of it lists (LOC205).
            var setInterface = references
                .SelectMany(static reference => reference.SetsOfEnums)
                .FirstOrDefault(setOfEnum => String.Equals(setOfEnum.Contract, contract, StringComparison.Ordinal))
                .SetInterface ?? SupportedLanguages.SetInterface;

            foreach (var code in declared.Codes)
            {
                if (IsServed(referencedSets, contract, code) || IsServed(ownSets, contract, code))
                    continue;

                // The contract by its full name, each part as code writes it. The set lies wherever the app keeps it, so
                // the fix chooses its namespace.
                var setName = SuggestSetName(contract, code);
                var writtenContract = String.Join(".", contract.Split('.').Select(LocalizationSetsPart.EscapeIdentifier));

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.LibraryMissingLanguage,
                    declared.Location,
                    new EquatableArray<string>([contract, code, SupportedLanguages.SuggestSet(setName, contract, setInterface)]),
                    SuggestedName: setName,
                    NewSet: new NewLocalizationSet(Namespace: null, $"public class {setName} : {setInterface}, {writtenContract}")));
            }
        }

        // Inline texts: a library's sets can be given a language from here, but no text it writes inline can, whether it
        // has sets or not.
        foreach (var reference in references)
        {
            if (!reference.HasInlineTexts || reference.Languages.Length is 0)
                continue;

            // Such a text falls back as any text does: to this assembly's default language where the library has it, and
            // else to the library's own default language.
            var appDefault = declared.Codes[0];
            var shown = reference.Languages.Any(language => SupportedLanguages.Serves(language, appDefault)) ? appDefault : reference.Languages[0];

            foreach (var code in declared.Codes)
            {
                if (reference.Languages.Any(language => SupportedLanguages.Serves(language, code)))
                    continue;

                findings.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.LibraryCannotBeTranslated,
                    declared.Location,
                    new EquatableArray<string>([reference.AssemblyName, code, shown])));
            }
        }

        return new EquatableArray<LocalizationSetsFinding>(findings.ToImmutable());

        static bool IsServed(IEnumerable<ProvidedLocalizationSet> sets, string contract, string code)
        {
            return sets.Any(set => String.Equals(set.Contract, contract, StringComparison.Ordinal) && SupportedLanguages.Serves(set.Language, code));
        }
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
        ImmutableArray<LocalizationSetsFinding>.Builder? findings,
        CancellationToken cancellationToken)
    {
        var defaultLanguage = declared.DefaultLanguage;

        // The contracts written so far, each with the set it comes from.
        var written = new Dictionary<string, string>(StringComparer.Ordinal);

        // A hint name tells the files of two contracts apart, and the compiler compares it without regard to case.
        var hintNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Asked once for all contracts, and not of every set for each contract: that took seconds on each edit in a
        // project with hundreds of contracts.
        var setImplementers = new Implementers(sets, generatedContracts);
        var excludedSetImplementers = new Implementers(excluded, generatedContracts);
        var unreadableSetImplementers = new Implementers(unreadable, generatedContracts);

        var misdeclaredSets = sets
            .Where(other => other.Language is not null && (IsDerivedFromOtherLanguage(other, generatedContracts) || IsMissingContract(other, defaultLanguage)))
            .ToArray();

        foreach (var set in sets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!LocalizationDiscovery.IsDefaultSet(set, defaultLanguage, generatedContracts))
                continue;

            if (LocalizationDiscovery.DeriveBaseName(set.Name, set.Language!) is not { } baseName)
            {
                var suffix = SupportedLanguages.ToTypeSuffix(defaultLanguage ?? set.Language!);
                var suggestedName = SuggestNameEndingIn(set.Name, suffix);

                findings?.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.CannotExtend,
                    set.Location,
                    new EquatableArray<string>([set.Name, $"its name doesn't end in {suffix}, the suffix of the default language, so the generated class would carry the same name", $"Rename it to {suggestedName}."]),
                    SuggestedName: suggestedName));

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
                        new EquatableArray<string>([set.Name, $"'{firstSet}' is the default-language set of '{contractName}' already, and a contract has only one", "Remove one of the two, or rename this one."])));
                }

                continue;
            }

            written.Add(contractFullName, set.Name);

            // A type of the contract's name, such as an interface written by hand that doesn't derive from
            // ILocalizationSet, would meet the generated contract (CS0101). So neither the contract nor its texts are
            // written, and the sets that list that type aren't told to add it (LOC204).
            if (set.OfContract.TypeNamedLikeContract is { } typeOfContractName)
            {
                findings?.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.CannotExtend,
                    set.Location,
                    new EquatableArray<string>([set.Name, $"its contract would be named '{contractName}', like {typeOfContractName}", "Rename the set or that type. To write the contract yourself, derive it from ILocalizationSet."])));

                continue;
            }

            // The generated texts take their default wording from an instance the generated code creates, so a set it
            // can't create gets no texts. It still gets its contract, which it lists, as every other language does.
            // Reported here, and not as LOC208: that one advises to exclude the set and register it by hand, which a
            // default-language set must never be. A default-language set has its language, so only how it is created
            // can keep it from registering.
            var hasTexts = true;
            if (LocalizationDiscovery.GetCreationProblem(set) is { } problem)
            {
                var advice = String.Equals(problem, LocalizationDiscovery.ObsoleteAsErrorProblem, StringComparison.Ordinal)
                    ? "Mark it [Obsolete] as a warning while code still uses it, or remove it."
                    : "Generated code creates it, so give it a public parameterless constructor and no required members.";

                findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, problem, advice])));
                hasTexts = false;
            }

            // The generated class would be a second type of its name (CS0101), so it isn't written. The contract is, as
            // for a set that can't be created: the sets list it.
            if (set.OfContract.TypeNamedLikeTexts is { } typeOfName)
            {
                var that = typeOfName.StartsWith("the namespace ", StringComparison.Ordinal) ? "that namespace" : "that type";

                findings?.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.CannotExtend,
                    set.Location,
                    new EquatableArray<string>([set.Name, $"its generated class would be named '{baseName}', like {typeOfName}", $"Rename the set or {that}."])));

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

            foreach (var member in set.OfContract.Texts)
            {
                if (!IsReservedTextName(member.Name, baseName, hasLookup: set.OfContract.Enums.Length > 0))
                    continue;

                var taken = member.Name switch
                {
                    _ when String.Equals(member.Name, baseName, StringComparison.Ordinal)
                        => $"the generated class '{baseName}', which can't hold a member of its own name. '{baseName}.{member.Name}' isn't generated",
                    GetName => $"the generated method '{baseName}.{GetName}', which gives a text by its key. The text '{baseName}.{member.Name}' isn't generated",
                    ExtensionName => $"the generated extension method '{ExtensionName}', which gives an enum value its text. The text '{baseName}.{member.Name}' isn't generated",
                    _ => $"the generated method '{baseName}.{LookupName}', which gives the text of an enum value. The text '{baseName}.{member.Name}' isn't generated",
                };

                // At the text, which is what to rename, and not at the set.
                var reserved = set.OfContract.ReservedTexts.FirstOrDefault(text => String.Equals(text.Name, member.Name, StringComparison.Ordinal));
                var location = reserved.Name is null ? set.Location : reserved.Location;

                findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.ReservedTextName, location, new EquatableArray<string>([set.Name, member.Name, taken])));
            }

            // At the method, which is what to change, and not at the set.
            foreach (var method in set.OfContract.UnsupportedTexts)
                findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.UnsupportedText, method.Location, new EquatableArray<string>([set.Name, method.Name, method.Problem])));

            // At the text that's left out, which says where it's written. Its contract would lack it, and so every other
            // language would too, without a word.
            foreach (var text in set.OfContract.UndeclaredTexts)
                findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.UndeclaredText, text.Location, new EquatableArray<string>([set.Name, text.Name, text.DeclaringType])));

            var implementers = setImplementers.Of(set, contractFullName).Where(static other => other.Language is not null).ToArray();
            var excludedImplementers = excludedSetImplementers.Of(set, contractFullName);
            var hasUnreadableImplementer = unreadableSetImplementers.Of(set, contractFullName).Length > 0;

            // A set reported for its base already says what to do about this language.
            var misdeclared = misdeclaredSets.Where(other => !String.Equals(other.MetadataName, set.MetadataName, StringComparison.Ordinal)
                && String.Equals(other.Namespace, set.Namespace, StringComparison.Ordinal)
                && String.Equals(LocalizationDiscovery.DeriveBaseName(other.Name, other.Language!), baseName, StringComparison.Ordinal)).ToArray();

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
                    var (suggestion, newSet) = SuggestSet(setName, GetSetInterface(set), contractName, code, set.Namespace, set.IsPublic, implementers.Prepend(set));

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
                .Select(static other => new SetLink(other.Language!, other.Namespace, other.Name, other.OfContract.DeclaredTexts, other.OfContract.InheritedTexts, other.OfContract.Wordings, other.IsExcluded))
                .ToImmutableArray();

            contracts.Add(new LocalizationContract(
                HintName: GetHintName(set.Namespace, set.Name, hintNames),
                Namespace: set.Namespace,
                ClassName: set.Name,
                IsPublic: set.IsPublic,
                ContractName: contractName,
                TextsName: baseName,
                Members: set.OfContract.Texts,
                Sets: new EquatableArray<SetLink>(links),
                HasTexts: hasTexts,
                Usings: set.OfContract.Usings,
                Enums: set.OfContract.Enums,
                SuppressedDiagnostics: GetContractSuppressedDiagnostics(set)));
        }
    }

    /// <summary>
    /// What the file of a contract turns off besides <c>CS0612</c> and <c>CS0618</c>: what naming the default-language
    /// set reports, then what naming its enums and their members reports, and what writing the types of its texts'
    /// parameters reports. Each id once, in that order.
    /// </summary>
    private static EquatableArray<string> GetContractSuppressedDiagnostics(DiscoveredLocalization set)
    {
        var ofEnums = set.OfContract.Enums.SelectMany(static @enum => @enum.SuppressedDiagnostics);
        var ofParameters = set.OfContract.Texts.SelectMany(static text => text.Parameters).SelectMany(static parameter => parameter.SuppressedDiagnostics);

        var suppressed = set.SuppressedDiagnostics.Concat(ofEnums).Concat(ofParameters).Distinct(StringComparer.Ordinal).ToImmutableArray();
        return new EquatableArray<string>(suppressed);
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
    /// The sets that implement the contract generated from a default-language set, whatever their language and other
    /// than that set itself.
    /// </summary>
    // The generator's own contract does not exist yet, so a set implementing it lists an unresolved name, itself or
    // through a class it derives from, which names this contract (LocalizationDiscovery.FindGeneratedContract). A set
    // deriving from the default set implements it too.
    private sealed class Implementers
    {
        private ImmutableArray<DiscoveredLocalization> Sets { get; }

        /// <summary>
        /// The indexes of the sets that list each contract, in the order of the sets.
        /// </summary>
        private Dictionary<string, List<int>> ByContract { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The indexes of the sets that derive from each class, in the order of the sets.
        /// </summary>
        private Dictionary<string, List<int>> ByAncestor { get; } = new(StringComparer.Ordinal);

        public Implementers(ImmutableArray<DiscoveredLocalization> sets, GeneratedContracts generatedContracts)
        {
            this.Sets = sets;

            for (var index = 0; index < sets.Length; index++)
            {
                foreach (var baseType in sets[index].UnresolvedBaseTypes)
                    if (LocalizationDiscovery.FindGeneratedContract(baseType, generatedContracts) is { } contract)
                        Add(this.ByContract, contract, index);

                foreach (var ancestor in sets[index].AncestorNames)
                    Add(this.ByAncestor, ancestor, index);
            }
        }

        public DiscoveredLocalization[] Of(DiscoveredLocalization defaultSet, string contractFullName)
        {
            this.ByContract.TryGetValue(contractFullName, out var listing);
            this.ByAncestor.TryGetValue(defaultSet.MetadataName, out var deriving);

            IEnumerable<int> indexes = (listing, deriving) switch
            {
                (null, null) => [],
                (not null, null) => listing,
                (null, not null) => deriving,
                _ => listing.Union(deriving).OrderBy(static index => index),
            };

            var implementers = indexes
                .Select(index => this.Sets[index])
                .Where(other => !String.Equals(other.MetadataName, defaultSet.MetadataName, StringComparison.Ordinal))
                .ToArray();

            return implementers;
        }

        private static void Add(Dictionary<string, List<int>> indexes, string key, int index)
        {
            if (!indexes.TryGetValue(key, out var list))
                indexes.Add(key, list = []);

            // A set that lists a contract twice, itself and through a class it derives from, counts once.
            if (list.Count is 0 || list[list.Count - 1] != index)
                list.Add(index);
        }
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
                var (suggestion, newSet) = SuggestSet(setName, SupportedLanguages.SetInterface, simpleName, code, contractNamespace, members.Any(static set => set.IsPublic), members);

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
            // A type of the contract's name, which the default-language set's LOC200 reports, is what it lists.
            else if (IsMissingContract(set, defaultLanguage) && set.OfContract.TypeNamedLikeContract is null)
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
    private static LocalizationSetsFinding UnlistedLanguage(DiscoveredLocalization set, string language, EquatableArray<string> codes)
    {
        var meant = GuessLongCode(set.Name, codes);

        var advice = meant is not null
            ? $"If it's meant for '{meant}', list '{meant}' in the attribute, which its name then gives it, or implement 'string ILocalizationSet.LocalizationLanguageCode => \"{meant}\";'. Otherwise add '{language}' to the attribute, or remove the set."
            : "Add the language to the attribute, or remove the set.";

        var unlistedLanguageFinding = new LocalizationSetsFinding(
            LocalizationSetsRule.UnlistedLanguage,
            set.Location,
            new EquatableArray<string>([set.Name, language, advice]),
            SuggestedName: meant ?? language);

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

    /// <summary>
    /// The set to add for a language a contract of this assembly misses: as the diagnostic names it, and as the code fix
    /// writes it (<see cref="NewLocalizationSet"/>). A region of a language that has a set derives from that set, to hold
    /// only the texts that differ: <c>'class ShopLocalizationEnGb : ShopLocalizationEn'</c>. Any other language
    /// implements the contract.
    /// </summary>
    /// <param name="setName">The name of the set to add.</param>
    /// <param name="setInterface">What the set lists before its contract (<see cref="GetSetInterface"/>).</param>
    /// <param name="contract">The contract as the namespace of the new set names it.</param>
    /// <param name="code">The language the set is for.</param>
    /// <param name="namespace">The namespace of the new set: that of the contract.</param>
    /// <param name="isPublic">Whether a set that implements the contract is public, as the contract's sets are.</param>
    /// <param name="sets">The sets of the contract, among which a set of a language above the region.</param>
    private static (string Suggestion, NewLocalizationSet NewSet) SuggestSet(string setName, string setInterface, string contract, string code, string @namespace, bool isPublic,
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
            var declaration = $"{(isPublic ? "public" : "internal")} class {setName} : {setInterface}, {contract}";
            return (SupportedLanguages.SuggestSet(setName, contract, setInterface), new NewLocalizationSet(@namespace, declaration));
        }

        // As accessible as the set it derives from, which a derived class can't exceed. Named by its full name where it
        // lies in another namespace than the new set.
        var keyword = above.IsRecord ? "record" : "class";
        var baseClass = String.Equals(above.Namespace, @namespace, StringComparison.Ordinal)
            ? LocalizationSetsPart.EscapeIdentifier(above.Name)
            : above.FullyQualifiedName;

        var suggestion = $"'{keyword} {setName} : {above.Name}', which keeps the wording of '{above.Language}' for every text it doesn't override";
        var derived = $"{(above.IsPublic ? "public" : "internal")} {keyword} {setName} : {baseClass}";

        return (suggestion, new NewLocalizationSet(@namespace, derived));
    }

    /// <summary>
    /// What a set of a contract lists before the contract, as its default-language set does: <c>ILocalizationSet</c>,
    /// or <c>ILocalizationSet&lt;OrderStatus&gt;</c> for a set of the texts of an enum. The enum stands by its name
    /// alone where it lies in the namespace of the set, and by its full name elsewhere.
    /// </summary>
    private static string GetSetInterface(DiscoveredLocalization defaultSet)
    {
        if (defaultSet.OfContract.Enums.Length is 0)
            return SupportedLanguages.SetInterface;

        var setInterface = String.Join(", ", defaultSet.OfContract.Enums.Select(@enum =>
        {
            var name = String.Equals(@enum.Namespace, defaultSet.Namespace, StringComparison.Ordinal)
                ? @enum.Name
                : @enum.FullyQualifiedName;

            return $"{SupportedLanguages.SetInterface}<{name}>";
        }));

        return setInterface;
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
    internal static string GetContractBaseName(string contract)
    {
        var name = contract.Substring(contract.LastIndexOf('.') + 1);

        var baseName = name.Length > 1 && name[0] == 'I' && Char.IsUpper(name[1]) ? name.Substring(1) : name;
        return baseName;
    }
}

/// <summary>
/// Everything a generated contract is built from: the name of its file, the default-language set, whether the generated
/// types are public like it or internal, the derived names, its text members in declaration order, the sets translating
/// them, whether the generated class holds the texts, the using directives its file repeats
/// (<see cref="RepeatedUsings"/>), and the enums whose members it has a text for, each of which the generated class
/// gets a method for that gives the text of a value. The class doesn't hold the texts when the generated code can't
/// create the set for their default wording (<c>LOC200</c>). The file turns off what naming the default-language set,
/// its enums and the types of its parameters reports (<see cref="LocalizationSetsAnalysis.GetContractSuppressedDiagnostics"/>).
/// </summary>
internal sealed record LocalizationContract(
    string HintName,
    string Namespace,
    string ClassName,
    bool IsPublic,
    string ContractName,
    string TextsName,
    EquatableArray<LocalizationMember> Members,
    EquatableArray<SetLink> Sets,
    bool HasTexts,
    RepeatedUsings Usings,
    EquatableArray<LocalizedEnum> Enums,
    EquatableArray<string> SuppressedDiagnostics);

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
    /// <c>ILocalizationSet.LocalizationLanguageCode</c> is no constant or comes from another assembly. It is no culture name, so it
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
    bool HasInlineTexts,
    EquatableArray<ReferencedSetOfEnum> SetsOfEnums);

/// <summary>
/// A public contract of a referenced assembly that holds the texts of an enum, with what each of its sets lists in place
/// of <c>ILocalizationSet</c>, such as <c>ILocalizationSet&lt;Lib.OrderStatus&gt;</c> (<c>LOC205</c>). The set an app
/// adds for it (<c>LOC700</c>) lists that too.
/// </summary>
internal readonly record struct ReferencedSetOfEnum(
    string Contract,
    string SetInterface);

/// <summary>
/// A diagnostic to report: which rule, where, and the arguments of its message. A finding a name resolves carries it,
/// for the code fix to apply: the name the class should carry (<c>LOC200</c>), the language to list
/// (<c>LOC202</c>), the contract it should implement (<c>LOC204</c>), or the set to add (<c>LOC201</c> and
/// <c>LOC700</c>), which also carries how to write that set.
/// </summary>
internal readonly record struct LocalizationSetsFinding(
    LocalizationSetsRule Rule,
    DiagnosticLocation Location,
    EquatableArray<string> Arguments,
    string? SuggestedName = null,
    NewLocalizationSet? NewSet = null);

/// <summary>
/// A set the code fix of <c>LOC201</c> or <c>LOC700</c> adds in a file of its own: its namespace, empty for the global
/// one, and its declaration without a body, such as
/// <c>public class ShopLocalizationDe : ILocalizationSet, IShopLocalization</c>. The namespace is
/// <see langword="null"/> for a set of a library's contract (<c>LOC700</c>), which the fix gives the namespace of the
/// app.
/// </summary>
internal readonly record struct NewLocalizationSet(
    string? Namespace,
    string Declaration);

/// <summary>
/// The rules <see cref="LocalizationSetsPart"/> reports.
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
    /// <c>LOC700</c>: a referenced contract has no set for a supported language.
    /// </summary>
    LibraryMissingLanguage,

    /// <summary>
    /// <c>LOC701</c>: a referenced library writes texts inline, and lacks a supported language.
    /// </summary>
    LibraryCannotBeTranslated,

    /// <summary>
    /// <c>LOC300</c>: a text of a default-language set is named like the generated texts class, or like a method of
    /// that class: the one that gives a text by its key, or one of the two that give the text of an enum
    /// value.
    /// </summary>
    ReservedTextName,

    /// <summary>
    /// <c>LOC400</c>: a method of a default-language set has the shape of a text that takes values, but can't be one.
    /// </summary>
    UnsupportedText,

    /// <summary>
    /// <c>LOC303</c>: a default-language set inherits a text from a class of its assembly without declaring it, so its
    /// contract leaves the text out.
    /// </summary>
    UndeclaredText,
}
