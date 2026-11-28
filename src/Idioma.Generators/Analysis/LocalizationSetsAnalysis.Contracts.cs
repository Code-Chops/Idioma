namespace CodeChops.Idioma.Generators;

/// <inheritdoc cref="LocalizationSetsAnalysis"/>
internal sealed partial record LocalizationSetsAnalysis
{
    /// <summary>
    /// Builds the contract of every default-language set, and checks that each language has a set of it. Without
    /// <paramref name="findings"/>, nothing is reported, but the contracts are still written.
    /// </summary>
    private static void AnalyzeContracts(
        ImmutableArray<DiscoveredLocalization> sets,
        ImmutableArray<DiscoveredLocalization> excluded,
        DeclaredLanguages declared,
        GeneratedContracts generatedContracts,
        ImmutableArray<LocalizationContract>.Builder contracts,
        ImmutableArray<ProvidedLocalizationSet>.Builder provided,
        ImmutableArray<LocalizationSetsFinding>.Builder? findings,
        CancellationToken cancellationToken)
    {
        var pass = new ContractsPass(sets, excluded, declared, generatedContracts, contracts, provided, findings);

        foreach (var set in sets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!LocalizationDiscovery.IsDefaultSet(set, pass.DefaultLanguage, generatedContracts))
                continue;

            AnalyzeContract(set, pass);
        }
    }

    /// <summary>
    /// The state <see cref="AnalyzeContracts"/> keeps across the default-language sets.
    /// </summary>
    private sealed class ContractsPass
    {
        public ContractsPass(
            ImmutableArray<DiscoveredLocalization> sets,
            ImmutableArray<DiscoveredLocalization> excluded,
            DeclaredLanguages declared,
            GeneratedContracts generatedContracts,
            ImmutableArray<LocalizationContract>.Builder contracts,
            ImmutableArray<ProvidedLocalizationSet>.Builder provided,
            ImmutableArray<LocalizationSetsFinding>.Builder? findings)
        {
            this.Declared = declared;
            this.DefaultLanguage = declared.DefaultLanguage;
            this.GeneratedContracts = generatedContracts;
            this.Contracts = contracts;
            this.Provided = provided;
            this.Findings = findings;

            // Indexed once for all contracts, so a project with hundreds of contracts doesn't scan every set per contract.
            this.SetImplementers = new Implementers(sets, generatedContracts);
            this.ExcludedSetImplementers = new Implementers(excluded, generatedContracts);

            var defaultLanguage = this.DefaultLanguage;
            this.MisdeclaredSets = sets
                .Where(other => other.Language is not null && (IsDerivedFromOtherLanguage(other, generatedContracts) || IsMissingContract(other, defaultLanguage)))
                .ToArray();
        }

        public DeclaredLanguages Declared { get; }

        public string? DefaultLanguage { get; }

        public GeneratedContracts GeneratedContracts { get; }

        public ImmutableArray<LocalizationContract>.Builder Contracts { get; }

        public ImmutableArray<ProvidedLocalizationSet>.Builder Provided { get; }

        /// <summary>
        /// <see langword="null"/> when nothing is reported.
        /// </summary>
        public ImmutableArray<LocalizationSetsFinding>.Builder? Findings { get; }

        /// <summary>
        /// The contracts written so far, each with the set it comes from.
        /// </summary>
        public Dictionary<string, string> Written { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The hint names taken so far, ignoring case as the compiler does.
        /// </summary>
        public HashSet<string> HintNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Implementers SetImplementers { get; }

        public Implementers ExcludedSetImplementers { get; }

        /// <summary>
        /// The sets reported for their base or their missing contract, which already says what to do about their language.
        /// </summary>
        public DiscoveredLocalization[] MisdeclaredSets { get; }
    }

    /// <summary>
    /// Builds the contract of one default-language set, and reports problems with the set, its texts and its languages.
    /// </summary>
    private static void AnalyzeContract(DiscoveredLocalization set, ContractsPass pass)
    {
        if (LocalizationDiscovery.DeriveBaseName(set.Name, set.Language!) is not { } baseName)
        {
            ReportNameWithoutLanguageSuffix(set, pass);
            return;
        }

        var contractName = $"I{baseName}";
        var contractFullName = LocalizationDiscovery.GetContractFullName(set.Namespace, baseName);

        if (!StartsContract(set, contractName, contractFullName, pass))
            return;

        var hasTexts = CanHoldTexts(set, baseName, pass.Findings);

        // The generated texts read the default wording through the contract, so the set must list it.
        var listsContract = LocalizationDiscovery.ListsGeneratedContract(set, pass.GeneratedContracts, contractFullName);

        if (!listsContract)
        {
            pass.Findings?.Add(new LocalizationSetsFinding(
                LocalizationSetsRule.MissingContract,
                set.Location,
                new EquatableArray<string>([set.Name, set.Language!, MissesContract(contractName), AddContract(contractName, set.Name)]),
                SuggestedName: contractName));
        }

        ReportTexts(set, baseName, pass.Findings);

        var implementers = pass.SetImplementers.Of(set, contractFullName).Where(static other => other.Language is not null).ToArray();
        var excludedImplementers = pass.ExcludedSetImplementers.Of(set, contractFullName);

        if (pass.Findings is not null)
            ReportMissingLanguages(set, baseName, contractName, implementers, excludedImplementers, pass, pass.Findings);

        var otherSets = implementers
            .Concat(excludedImplementers)
            .OrderBy(static other => other.MetadataName, StringComparer.Ordinal)
            .ToArray();

        // A referencing assembly can't implement an internal contract.
        if (set.IsPublic)
        {
            pass.Provided.Add(ProvidedLocalizationSet.Of(contractFullName, set));
            foreach (var otherSet in otherSets)
                pass.Provided.Add(ProvidedLocalizationSet.Of(contractFullName, otherSet));
        }

        var links = otherSets
            .Prepend(set)
            .Select(static other => new SetLink(other.Language!, other.Namespace, other.Name, other.OfContract.DeclaredTexts, other.OfContract.InheritedTexts, other.OfContract.Wordings, other.IsExcluded))
            .ToImmutableArray();

        pass.Contracts.Add(new LocalizationContract(
            HintName: GetHintName(set.Namespace, set.Name, pass.HintNames),
            Namespace: set.Namespace,
            ClassName: set.Name,
            IsPublic: set.IsPublic,
            ContractName: contractName,
            TextsName: baseName,
            Members: set.OfContract.Texts,
            Sets: new EquatableArray<SetLink>(links),
            HasTexts: hasTexts,
            DefaultSetListsContract: listsContract,
            Usings: set.OfContract.Usings,
            Enums: set.OfContract.Enums));
    }

    /// <summary>
    /// Reports a default-language set whose name doesn't end in its language suffix (<c>LOC200</c>).
    /// </summary>
    private static void ReportNameWithoutLanguageSuffix(DiscoveredLocalization set, ContractsPass pass)
    {
        var suffix = SupportedLanguages.ToTypeSuffix(pass.DefaultLanguage ?? set.Language!);

        if (IsSuffixAlone(set.Name, suffix))
        {
            pass.Findings?.Add(SuffixAlone(set, suffix));
            return;
        }

        var suggestedName = SuggestNameEndingIn(set.Name, suffix);

        pass.Findings?.Add(new LocalizationSetsFinding(
            LocalizationSetsRule.CannotExtend,
            set.Location,
            new EquatableArray<string>([set.Name, $"its name doesn't end in {suffix}, the suffix of the default language, so the generated class would carry the same name", $"Rename it to {suggestedName}."]),
            SuggestedName: suggestedName));
    }

    /// <summary>
    /// Whether a default-language set starts its contract. It doesn't when another set started it or a type has its
    /// name (<c>LOC200</c>).
    /// </summary>
    private static bool StartsContract(DiscoveredLocalization set, string contractName, string contractFullName, ContractsPass pass)
    {
        var findings = pass.Findings;

        // A contract comes from the first set by name, such as ShopLocalizationEn next to ShopLocalizationEN. The
        // second is reported only when there's a default language to hold it to.
        if (pass.Written.TryGetValue(contractFullName, out var firstSet))
        {
            if (pass.DefaultLanguage is not null)
            {
                findings?.Add(new LocalizationSetsFinding(
                    LocalizationSetsRule.CannotExtend,
                    set.Location,
                    new EquatableArray<string>([set.Name, $"'{firstSet}' is the default-language set of '{contractName}' already, and a contract has only one", "Remove one of the two, or rename this one."])));
            }

            return false;
        }

        pass.Written.Add(contractFullName, set.Name);

        // A type of the contract's name would clash with it (CS0101), so neither the contract nor its texts are written.
        if (set.OfContract.TypeNamedLikeContract is { } typeOfContractName)
        {
            findings?.Add(new LocalizationSetsFinding(
                LocalizationSetsRule.CannotExtend,
                set.Location,
                new EquatableArray<string>([set.Name, $"its contract would be named '{contractName}', like {typeOfContractName}", "Rename the set or that type. To write the contract yourself, derive it from ILocalizationSet."])));

            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the generated class holds the texts. It doesn't when generated code can't create the set or a type or
    /// namespace has its name (<c>LOC200</c>). The contract is still written.
    /// </summary>
    private static bool CanHoldTexts(DiscoveredLocalization set, string baseName, ImmutableArray<LocalizationSetsFinding>.Builder? findings)
    {
        // The texts take their default wording from an instance the generated code creates. This isn't LOC207,
        // because that advises excluding the set, which a default-language set must never be.
        var hasTexts = true;
        if (LocalizationDiscovery.GetCreationProblem(set) is { } problem)
        {
            const string advice = "Generated code creates it, so give it a public parameterless constructor and no required members.";

            findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, problem, advice])));
            hasTexts = false;
        }

        // The generated class would clash with that type (CS0101).
        if (set.OfContract.TypeNamedLikeTexts is { } typeOfName)
        {
            var that = typeOfName.StartsWith("the namespace ", StringComparison.Ordinal) ? "that namespace" : "that type";

            findings?.Add(new LocalizationSetsFinding(
                LocalizationSetsRule.CannotExtend,
                set.Location,
                new EquatableArray<string>([set.Name, $"its generated class would be named '{baseName}', like {typeOfName}", $"Rename the set or {that}."])));

            hasTexts = false;
        }

        return hasTexts;
    }

    /// <summary>
    /// Reports the texts the contract or generated class can't hold (<c>LOC300</c>, <c>LOC303</c>, <c>LOC305</c>,
    /// <c>LOC400</c>), each at the text to change.
    /// </summary>
    private static void ReportTexts(DiscoveredLocalization set, string baseName, ImmutableArray<LocalizationSetsFinding>.Builder? findings)
    {
        foreach (var member in set.OfContract.Texts)
        {
            if (!IsReservedTextName(member.Name, baseName, hasLookup: set.OfContract.Enums.Length > 0))
                continue;

            var taken = member.Name switch
            {
                _ when String.Equals(member.Name, baseName, StringComparison.Ordinal)
                    => $"the generated class '{baseName}', which can't hold a member of its own name. '{baseName}.{member.Name}' isn't generated",
                GetName =>$"the generated method '{baseName}.{member.Name}', which gives a text by its key. The text '{baseName}.{member.Name}' isn't generated",
                KeysName => $"the generated property '{baseName}.{KeysName}', which lists the key of every text. The text '{baseName}.{member.Name}' isn't generated",
                ExtensionName => $"the generated extension method '{ExtensionName}', which gives an enum value its text. The text '{baseName}.{member.Name}' isn't generated",
                _ => $"the generated method '{baseName}.{LookupName}', which gives the text of an enum value. The text '{baseName}.{member.Name}' isn't generated",
            };

            var reserved = set.OfContract.ReservedTexts.FirstOrDefault(text => String.Equals(text.Name, member.Name, StringComparison.Ordinal));
            var location = reserved.Name is null ? set.Location : reserved.Location;

            findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.ReservedTextName, location, new EquatableArray<string>([set.Name, member.Name, taken])));
        }

        foreach (var method in set.OfContract.UnsupportedTexts)
            findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.UnsupportedText, method.Location, new EquatableArray<string>([set.Name, method.Name, method.Problem])));

        // Otherwise the contract, and so every other language, would lack the text silently.
        foreach (var text in set.OfContract.UndeclaredTexts)
            findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.UndeclaredText, text.Location, new EquatableArray<string>([set.Name, text.Name, text.DeclaringType])));

        // Every other language that lacks the text fails to compile, and only this says where it comes from.
        foreach (var text in set.OfContract.TextsOutsideEnums)
            findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.TextOutsideEnum, text.Location, new EquatableArray<string>([set.Name, text.Name, text.Enums, text.Advice])));
    }

    /// <summary>
    /// Reports every listed language a contract has no set of (<c>LOC201</c>), with the set to add.
    /// </summary>
    private static void ReportMissingLanguages(
        DiscoveredLocalization set,
        string baseName,
        string contractName,
        DiscoveredLocalization[] implementers,
        DiscoveredLocalization[] excludedImplementers,
        ContractsPass pass,
        ImmutableArray<LocalizationSetsFinding>.Builder findings)
    {
        // A set reported for its base already says what to do about this language.
        var misdeclared = pass.MisdeclaredSets.Where(other => !String.Equals(other.MetadataName, set.MetadataName, StringComparison.Ordinal)
            && String.Equals(other.Namespace, set.Namespace, StringComparison.Ordinal)
            && String.Equals(LocalizationDiscovery.DeriveBaseName(other.Name, other.Language!), baseName, StringComparison.Ordinal)).ToArray();

        foreach (var code in pass.Declared.Codes)
        {
            // A listed region (en-GB next to en) needs a set of its own, because listing it says it has its own
            // wording. An excluded set counts only for the language it plainly serves, because it gets no LOC204 or
            // LOC205.
            if (IsSetOf(set, code)
                || implementers.Any(other => IsSetOf(other, code))
                || excludedImplementers.Any(other => IsSetOf(other, code))
                || misdeclared.Any(other => IsSetOf(other, code)))
            {
                continue;
            }

            var setName = $"{baseName}{SupportedLanguages.ToTypeSuffix(code)}";
            var (suggestion, newSet) = SuggestSet(setName, GetLocalizationSetInterface(set), contractName, code, set.Namespace, set.IsPublic, implementers.Prepend(set));

            findings.Add(new LocalizationSetsFinding(
                LocalizationSetsRule.MissingLanguage,
                set.Location,
                new EquatableArray<string>([baseName, code, suggestion]),
                SuggestedName: setName,
                NewSet: newSet));
        }
    }

    /// <summary>
    /// The hint name of a contract's file: the set's full name without <c>@</c>, such as
    /// <c>MyApp.Localization.ShopLocalizationEn</c>. The compiler ignores case in hint names, so a name taken but for
    /// case gets a number, such as <c>shop.TextsEn_2</c>.
    /// </summary>
    // The contracts come in the order of their sets' names, so the same file gets the number on every build.
    private static string GetHintName(string @namespace, string className, HashSet<string> taken)
    {
        var name = (@namespace.Length > 0 ? $"{@namespace}.{className}" : className).Replace("@", "");
        var hintName = name;

        for (var number = 2; !taken.Add(hintName); number++)
            hintName = $"{name}_{number}";

        return hintName;
    }
}
