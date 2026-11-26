namespace CodeChops.Idioma.Generators;

internal sealed partial record LocalizationSetsAnalysis
{
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
        var pass = new ContractsPass(sets, excluded, unreadable, declared, generatedContracts, contracts, provided, findings);

        foreach (var set in sets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!LocalizationDiscovery.IsDefaultSet(set, pass.DefaultLanguage, generatedContracts))
                continue;

            AnalyzeContract(set, pass);
        }
    }

    /// <summary>
    /// What <see cref="AnalyzeContracts"/> holds while it goes through the default-language sets: what it was given,
    /// and what it keeps from one set to the next.
    /// </summary>
    private sealed class ContractsPass
    {
        public ContractsPass(
            ImmutableArray<DiscoveredLocalization> sets,
            ImmutableArray<DiscoveredLocalization> excluded,
            ImmutableArray<DiscoveredLocalization> unreadable,
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

            // Asked once for all contracts, and not of every set for each contract: that took seconds on each edit in a
            // project with hundreds of contracts.
            this.SetImplementers = new Implementers(sets, generatedContracts);
            this.ExcludedSetImplementers = new Implementers(excluded, generatedContracts);
            this.UnreadableSetImplementers = new Implementers(unreadable, generatedContracts);

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
        /// Where the findings go, or <see langword="null"/> when nothing is reported.
        /// </summary>
        public ImmutableArray<LocalizationSetsFinding>.Builder? Findings { get; }

        /// <summary>
        /// The contracts written so far, each with the set it comes from.
        /// </summary>
        public Dictionary<string, string> Written { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// The hint names taken so far. A hint name tells the files of two contracts apart, and the compiler compares
        /// it without regard to case.
        /// </summary>
        public HashSet<string> HintNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Implementers SetImplementers { get; }

        public Implementers ExcludedSetImplementers { get; }

        public Implementers UnreadableSetImplementers { get; }

        /// <summary>
        /// The sets reported for their base or for their missing contract, which says what to do about their language.
        /// </summary>
        public DiscoveredLocalization[] MisdeclaredSets { get; }
    }

    /// <summary>
    /// Builds the contract of one default-language set, and reports what is wrong with the set, its texts, and the
    /// languages of its contract.
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

        // The default-language set lists its contract next to ILocalizationSet, as every set does: the generated
        // texts read its wording through the contract.
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
        var hasUnreadableImplementer = pass.UnreadableSetImplementers.Of(set, contractFullName).Length > 0;

        // Not with a set of a language the build can't read among them: it may be the set of any language.
        if (pass.Findings is not null && !hasUnreadableImplementer)
            ReportMissingLanguages(set, baseName, contractName, implementers, excludedImplementers, pass, pass.Findings);

        var otherSets = implementers
            .Concat(excludedImplementers)
            .OrderBy(static other => other.MetadataName, StringComparer.Ordinal)
            .ToArray();

        // Internal: an assembly referencing this one could not implement it anyway.
        if (set.IsPublic)
        {
            pass.Provided.Add(new ProvidedLocalizationSet(contractFullName, set.Language!));
            foreach (var otherSet in otherSets)
                pass.Provided.Add(new ProvidedLocalizationSet(contractFullName, otherSet.Language!));

            if (hasUnreadableImplementer)
                pass.Provided.Add(new ProvidedLocalizationSet(contractFullName, ProvidedLocalizationSet.UnreadableLanguage));
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
            Enums: set.OfContract.Enums,
            SuppressedDiagnostics: GetContractSuppressedDiagnostics(set)));
    }

    /// <summary>
    /// Reports a default-language set whose name doesn't end in the suffix of its language (<c>LOC200</c>), so that
    /// its generated class would carry the set's own name.
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
    /// Whether a default-language set starts its contract, which it's then noted for. It starts none when another set
    /// has started that contract, or when a type carries the contract's name already. Both are reported
    /// (<c>LOC200</c>).
    /// </summary>
    private static bool StartsContract(DiscoveredLocalization set, string contractName, string contractFullName, ContractsPass pass)
    {
        var findings = pass.Findings;

        // A contract comes from one set, the first by name. Two sets start one when their names differ only in
        // the case of the language suffix, as ShopLocalizationEn and ShopLocalizationEN do. The second is
        // reported, and gets no contract and no texts of its own, which would carry the names of the first's.
        // Without a default language two sets can start one contract too, the Dutch set listing ILocalizationSet
        // as the English one does. Nothing is reported then, as nothing is held to a default language.
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

        // A type of the contract's name, such as an interface written by hand that doesn't derive from
        // ILocalizationSet, would meet the generated contract (CS0101). So neither the contract nor its texts are
        // written, and the sets that list that type aren't told to add it (LOC204).
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
    /// Whether the generated class of a default-language set holds its texts. It holds none when generated code can't
    /// create the set, or when a type or namespace carries the class's name already. Both are reported
    /// (<c>LOC200</c>), and the contract is written all the same.
    /// </summary>
    private static bool CanHoldTexts(DiscoveredLocalization set, string baseName, ImmutableArray<LocalizationSetsFinding>.Builder? findings)
    {
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

        return hasTexts;
    }

    /// <summary>
    /// Reports the texts of a default-language set that its contract or its generated class can't hold: a text with a
    /// reserved name (<c>LOC300</c>), a method that can't be a text (<c>LOC400</c>), and a text the set only inherits
    /// (<c>LOC303</c>).
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

        // At the text that isn't a member of the enum. The contract has it, so every other language that lacks it fails
        // to compile, and only this says where it comes from.
        foreach (var text in set.OfContract.TextsOutsideEnums)
            findings?.Add(new LocalizationSetsFinding(LocalizationSetsRule.TextOutsideEnum, text.Location, new EquatableArray<string>([set.Name, text.Name, text.Enums, text.Advice])));
    }

    /// <summary>
    /// Reports every language the attribute lists that a contract has no set of (<c>LOC201</c>), with the set to add.
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
    /// What the file of a contract turns off besides <c>CS0612</c> and <c>CS0618</c>: what naming the default-language
    /// set reports, then what naming its enums and their members reports, what writing the types of its texts'
    /// parameters reports, and what naming a text marked <c>[Obsolete]</c> or <c>[Experimental]</c> reports. Each id
    /// once, in that order.
    /// </summary>
    private static EquatableArray<string> GetContractSuppressedDiagnostics(DiscoveredLocalization set)
    {
        var ofEnums = set.OfContract.Enums.SelectMany(static @enum => @enum.SuppressedDiagnostics);
        var ofParameters = set.OfContract.Texts.SelectMany(static text => text.Parameters).SelectMany(static parameter => parameter.SuppressedDiagnostics);
        var ofTexts = set.OfContract.Texts.SelectMany(static text => text.SuppressedDiagnostics);

        var suppressed = set.SuppressedDiagnostics.Concat(ofEnums).Concat(ofParameters).Concat(ofTexts).Distinct(StringComparer.Ordinal).ToImmutableArray();
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
}
