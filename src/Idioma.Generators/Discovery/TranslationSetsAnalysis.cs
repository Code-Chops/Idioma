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
    /// The name of the constant the generated texts class declares next to the texts.
    /// </summary>
    public const string ResourceKeyPrefixName = "ResourceKeyPrefix";

    /// <summary>
    /// A name ending in what looks like a longer code (<see cref="GuessLongCode"/>): a capitalized language of two or
    /// three letters, then an upper-case region or a capitalized script.
    /// </summary>
    private static readonly Regex LongCodeSuffix = new("(?<=.)([A-Z][a-z]{1,2})([A-Z]{2}|[A-Z][a-z]{3})$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// Whether a text cannot become a member of the generated texts class: the class declares a member of that name
    /// itself, or the text carries the class's own name (<c>LOC014</c>).
    /// </summary>
    public static bool IsReservedTextName(string name, string textsName)
    {
        return String.Equals(name, ResourceKeyPrefixName, StringComparison.Ordinal) || String.Equals(name, textsName, StringComparison.Ordinal);
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
        var excluded = candidates.Where(candidate => candidate is { IsExcluded: true, Language: not null } && TranslationsDiscovery.IsSet(candidate, generatedContracts)).ToImmutableArray();
        var findings = ImmutableArray.CreateBuilder<TranslationSetsFinding>();
        var contracts = ImmutableArray.CreateBuilder<TranslationContract>();
        var provided = ImmutableArray.CreateBuilder<ProvidedTranslationSet>();

        foreach (var set in sets.Concat(excluded))
            if (set.Language is { } language)
                foreach (var contract in set.ContractNames)
                    provided.Add(new ProvidedTranslationSet(contract, language));

        if (languages is { Codes.Length: > 0 } declared)
        {
            AnalyzeContracts(sets, excluded, declared.Codes, generatedContracts, contracts, provided, findings);
            AnalyzeOwnContracts(sets, excluded, declared, findings);
            AnalyzeSets(sets, declared.Codes, generatedContracts, findings);

            // Registered by hand, a set still serves only a listed language: an application shows a culture outside its
            // list in its default language, so a set for another language would never show, without a word.
            foreach (var set in excluded)
                if (!IsListed(set.Language!, declared.Codes))
                    findings.Add(UnlistedLanguage(set, set.Language!));

            // A default-language set of a shape the generator cannot extend would otherwise leave its contract and texts
            // ungenerated without a word, and every other language's set without the contract it implements.
            foreach (var set in unextendable)
                if (String.Equals(set.Language, declared.Codes[0], StringComparison.OrdinalIgnoreCase))
                    findings.Add(new TranslationSetsFinding(TranslationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, set.Problem])));
        }
        else
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

        return new TranslationSetsAnalysis(
            new EquatableArray<TranslationContract>(contracts.ToImmutable()),
            new EquatableArray<ProvidedTranslationSet>(provided.Distinct().OrderBy(static set => set.Contract, StringComparer.Ordinal).ThenBy(static set => set.Language, StringComparer.Ordinal).ToImmutableArray()),
            new EquatableArray<TranslationSetsFinding>(findings.ToImmutable()));
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
        return discovered
            .GroupBy(static set => set.MetadataName, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => group.First() with
            {
                UnresolvedBaseTypes = new EquatableArray<UnresolvedBaseType>(group.SelectMany(static set => set.UnresolvedBaseTypes).Distinct().ToImmutableArray()),
                ListsTranslations = group.Any(static set => set.ListsTranslations),
                IsPartial = group.Any(static set => set.IsPartial),
            })
            .ToImmutableArray();
    }

    private static void AnalyzeContracts(
        ImmutableArray<DiscoveredTranslations> sets,
        ImmutableArray<DiscoveredTranslations> excluded,
        EquatableArray<string> codes,
        ImmutableArray<string> generatedContracts,
        ImmutableArray<TranslationContract>.Builder contracts,
        ImmutableArray<ProvidedTranslationSet>.Builder provided,
        ImmutableArray<TranslationSetsFinding>.Builder findings)
    {
        foreach (var set in sets)
        {
            if (!TranslationsDiscovery.IsDefaultSet(set, codes[0]))
                continue;

            if (TranslationsDiscovery.DeriveBaseName(set.Name, set.Language!) is not { } baseName)
            {
                var suffix = SupportedLanguages.ToTypeSuffix(codes[0]);
                findings.Add(new TranslationSetsFinding(
                    TranslationSetsRule.CannotExtend,
                    set.Location,
                    new EquatableArray<string>([set.Name, $"its name does not end in {suffix}, the suffix of the default language, so the generated class would carry the same name"]),
                    SuggestedName: set.Name + suffix));
                continue;
            }

            if (!set.IsPartial)
            {
                findings.Add(new TranslationSetsFinding(TranslationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, "it is not partial"])));
                continue;
            }

            // The generated texts take their default wording from an instance the generated code creates, so a set it
            // cannot create gets no texts either. Reported here, last, and not as LOC003: that one advises to exclude
            // the set and register it by hand, which a default-language set must never be. A default-language set has
            // its language, so the constructor is the only thing that can keep it from registering.
            if (set.RegistrationProblem is { } problem)
            {
                findings.Add(new TranslationSetsFinding(TranslationSetsRule.CannotExtend, set.Location, new EquatableArray<string>([set.Name, problem])));
                continue;
            }

            var contractName = $"I{baseName}";
            var contractFullName = TranslationsDiscovery.GetContractFullName(set.Namespace, baseName);

            foreach (var member in set.Texts)
                if (IsReservedTextName(member.Name, baseName))
                    findings.Add(new TranslationSetsFinding(TranslationSetsRule.ReservedTextName, set.Location, new EquatableArray<string>([set.Name, member.Name, baseName])));

            var implementers = sets.Where(other => ImplementsGeneratedContract(other, set, contractFullName, generatedContracts)).ToArray();
            var excludedImplementers = excluded.Where(other => ImplementsGeneratedContract(other, set, contractFullName, generatedContracts)).ToArray();

            // A set reported for its base already says what to do about this language.
            var misdeclared = sets.Where(other => other.Language is not null
                && !String.Equals(other.MetadataName, set.MetadataName, StringComparison.Ordinal)
                && String.Equals(other.Namespace, set.Namespace, StringComparison.Ordinal)
                && (IsDerivedFromOtherLanguage(other, generatedContracts) || IsMissingContract(other, codes[0]))
                && String.Equals(TranslationsDiscovery.DeriveBaseName(other.Name, other.Language), baseName, StringComparison.Ordinal)).ToArray();

            foreach (var code in codes.Skip(1))
            {
                // The default set serves the regions of its own language (en-GB for en), as any set serves its
                // regions, at run time and in the checks of a hand-written or referenced contract. An excluded set
                // counts only for a language it plainly serves: it gets no LOC006 or LOC007 to say what is wrong with
                // it, so the language it was perhaps meant for is still reported as missing.
                if (SupportedLanguages.Serves(set.Language!, code)
                    || implementers.Any(other => SupportedLanguages.Serves(other.Language!, code))
                    || excludedImplementers.Any(other => SupportedLanguages.Serves(other.Language!, code))
                    || misdeclared.Any(other => SupportedLanguages.Serves(other.Language!, code)))
                {
                    continue;
                }

                findings.Add(new TranslationSetsFinding(
                    TranslationSetsRule.MissingLanguage,
                    set.Location,
                    new EquatableArray<string>([baseName, code, SupportedLanguages.SuggestSet($"{baseName}{SupportedLanguages.ToTypeSuffix(code)}", contractName)])));
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
            }

            var translations = translators
                .Prepend(set)
                .Select(static other => new TranslationLink(other.Language!, other.Namespace, other.Name, other.FullyQualifiedName, other.StringProperties))
                .ToImmutableArray();

            contracts.Add(new TranslationContract(
                Namespace: set.Namespace,
                ClassName: set.Name,
                IsRecord: set.IsRecord,
                IsPublic: set.IsPublic,
                ContractName: contractName,
                TextsName: baseName,
                KeyPrefix: $"{DeriveStem(baseName)}.",
                Members: set.Texts,
                Translations: new EquatableArray<TranslationLink>(translations)));
        }
    }

    /// <summary>
    /// Whether a set, other than the default-language set itself, implements the contract generated from it.
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
        return other.Language is not null
               && !String.Equals(other.MetadataName, defaultSet.MetadataName, StringComparison.Ordinal)
               && (other.AncestorNames.AsImmutableArray().Contains(defaultSet.MetadataName, StringComparer.Ordinal)
                   || other.UnresolvedBaseTypes.Any(baseType => String.Equals(TranslationsDiscovery.FindGeneratedContract(baseType, generatedContracts), contractFullName, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Holds the contracts written by hand in this assembly to its languages, as a generated contract is: each must have
    /// a set for every language. Reported at the attribute, like a referenced contract, since the gap is a language the
    /// attribute lists. Only contracts this assembly has a set of are seen; a contract whose sets give no language the
    /// build can read is skipped. An excluded set counts for the language it serves, but no contract is seen through it
    /// alone.
    /// </summary>
    private static void AnalyzeOwnContracts(
        ImmutableArray<DiscoveredTranslations> sets,
        ImmutableArray<DiscoveredTranslations> excluded,
        DeclaredLanguages declared,
        ImmutableArray<TranslationSetsFinding>.Builder findings)
    {
        var contracts = sets
            .SelectMany(static set => set.OwnContractNames)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal);

        foreach (var contract in contracts)
        {
            var members = sets.Where(set => set.OwnContractNames.AsImmutableArray().Contains(contract, StringComparer.Ordinal)).ToArray();

            if (members.Any(static set => set.Language is null))
                continue;

            var excludedMembers = excluded.Where(set => set.OwnContractNames.AsImmutableArray().Contains(contract, StringComparer.Ordinal)).ToArray();

            var separator = contract.LastIndexOf('.');
            var contractNamespace = separator < 0 ? "" : contract.Substring(startIndex: 0, separator);
            var simpleName = contract.Substring(separator + 1);
            var baseName = GetContractBaseName(contract);

            // A set reported for listing ITranslations instead of this contract already says what to do about its
            // language.
            var misdeclared = sets.Where(other => other.Language is not null
                && String.Equals(other.Namespace, contractNamespace, StringComparison.Ordinal)
                && IsMissingContract(other, declared.Codes[0])
                && String.Equals(TranslationsDiscovery.DeriveBaseName(other.Name, other.Language), baseName, StringComparison.Ordinal)).ToArray();

            foreach (var code in declared.Codes)
            {
                if (members.Any(set => SupportedLanguages.Serves(set.Language!, code))
                    || excludedMembers.Any(set => SupportedLanguages.Serves(set.Language!, code))
                    || misdeclared.Any(set => SupportedLanguages.Serves(set.Language!, code)))
                {
                    continue;
                }

                findings.Add(new TranslationSetsFinding(
                    TranslationSetsRule.MissingLanguage,
                    declared.Location,
                    new EquatableArray<string>([contract, code, SupportedLanguages.SuggestSet(SuggestSetName(contract, code), simpleName)])));
            }
        }
    }

    private static void AnalyzeSets(
        ImmutableArray<DiscoveredTranslations> sets,
        EquatableArray<string> codes,
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
            else if (IsMissingContract(set, codes[0]))
            {
                var baseName = TranslationsDiscovery.DeriveBaseName(set.Name, language) ?? set.Name;
                var defaultSet = $"{baseName}{SupportedLanguages.ToTypeSuffix(codes[0])}";

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
    /// A regional set on top of its language (<c>nl-BE</c> on <c>nl</c>) is fine. A base that implements
    /// <c>ITranslations</c> only through a contract generated here is a set once that contract exists.
    /// </summary>
    private static bool IsDerivedFromOtherLanguage(DiscoveredTranslations set, ImmutableArray<string> generatedContracts)
    {
        return set.BaseSetLanguage is { } baseLanguage
               && (set.BaseSetContracts.Length is 0 || set.BaseSetContracts.Any(baseType => TranslationsDiscovery.FindGeneratedContract(baseType, generatedContracts) is not null))
               && !SupportedLanguages.Serves(baseLanguage, set.Language!);
    }

    private static TranslationSetsFinding DerivedFromOtherLanguage(DiscoveredTranslations set, string language)
    {
        return new TranslationSetsFinding(
            TranslationSetsRule.DerivedFromOtherLanguage,
            set.Location,
            new EquatableArray<string>([set.Name, language, set.BaseSetName!, set.BaseSetLanguage!]));
    }

    private static TranslationSetsFinding UnlistedLanguage(DiscoveredTranslations set, string language)
    {
        var advice = GuessLongCode(set.Name) is { } meant
            ? $"If it is meant for '{meant}', list '{meant}' in the attribute, which its name then gives it, or implement 'string ITranslations.LanguageCode => \"{meant}\";'. Otherwise add '{language}' to the attribute, or remove the set."
            : "Add the language to the attribute, or remove the set.";

        return new TranslationSetsFinding(TranslationSetsRule.UnlistedLanguage, set.Location, new EquatableArray<string>([set.Name, language, advice]));
    }

    /// <summary>
    /// A set that lists <c>ITranslations</c> in another language than the default, and implements no contract besides,
    /// so no text would ever read it.
    /// </summary>
    private static bool IsMissingContract(DiscoveredTranslations set, string defaultLanguage)
    {
        return set is { ListsTranslations: true, BaseSetName: null, UnresolvedBaseTypes.Length: 0 }
               && !TranslationsDiscovery.ImplementsContract(set)
               && !String.Equals(set.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Listed, a region of something listed, or the language of a listed region.
    /// </summary>
    private static bool IsListed(string language, EquatableArray<string> codes)
    {
        return codes.Any(code => SupportedLanguages.Serves(code, language) || SupportedLanguages.Serves(language, code));
    }

    /// <summary>
    /// A longer code a set's name looks like it ends in, for the advice of <c>LOC005</c> only: a language of two or three
    /// letters followed by a region (<c>ShopTranslationsNlBE</c>: <c>nl-BE</c>) or a script (<c>ShopTranslationsZhHans</c>:
    /// <c>zh-Hans</c>). A name gives such a code only when the attribute lists it, as a name alone cannot tell it from
    /// <c>AdminUiNL</c>.
    /// </summary>
    private static string? GuessLongCode(string setName)
    {
        var match = LongCodeSuffix.Match(setName);

        return match.Success ? $"{match.Groups[1].Value.ToLowerInvariant()}-{match.Groups[2].Value}" : null;
    }

    /// <summary>
    /// The set an app would add for a contract of a library: <c>GridTranslationsDE</c> for <c>Lib.IGridTranslations</c>.
    /// </summary>
    private static string SuggestSetName(string contract, string code)
    {
        return $"{GetContractBaseName(contract)}{SupportedLanguages.ToTypeSuffix(code)}";
    }

    /// <summary>
    /// The name a contract's sets share before their language suffix: <c>GridTranslations</c> for
    /// <c>Lib.IGridTranslations</c>.
    /// </summary>
    private static string GetContractBaseName(string contract)
    {
        var name = contract.Substring(contract.LastIndexOf('.') + 1);

        return name.Length > 1 && name[0] == 'I' && Char.IsUpper(name[1]) ? name.Substring(1) : name;
    }

    /// <summary>
    /// The base name without a trailing <c>Translations</c>: <c>ShopTranslations</c> gives <c>Shop</c>, the stem of the
    /// resource keys (<c>Shop.Checkout</c>). A base name that is nothing but <c>Translations</c> keeps it, so the key
    /// prefix is never empty.
    /// </summary>
    private static string DeriveStem(string baseName)
    {
        const string suffix = "Translations";

        return baseName.Length > suffix.Length && baseName.EndsWith(suffix, StringComparison.Ordinal)
            ? baseName.Substring(startIndex: 0, baseName.Length - suffix.Length)
            : baseName;
    }
}

/// <summary>
/// Everything a generated contract is built from: the default-language set, whether the generated types are public like
/// it or internal, the derived names, its text members in declaration order, and the sets translating them.
/// </summary>
internal sealed record TranslationContract(
    string Namespace,
    string ClassName,
    bool IsRecord,
    bool IsPublic,
    string ContractName,
    string TextsName,
    string KeyPrefix,
    EquatableArray<TranslationsMember> Members,
    EquatableArray<TranslationLink> Translations);

/// <summary>
/// A set translating a generated contract, for the crefs on its texts: its language, its namespace and name, by which
/// the contract's file names a set of its own namespace, its full name for a set of another, and the properties a cref
/// can name on it.
/// </summary>
internal readonly record struct TranslationLink(
    string Language,
    string Namespace,
    string Name,
    string FullyQualifiedName,
    EquatableArray<string> StringProperties);

/// <summary>
/// That an assembly holds a set of a public contract for a language.
/// </summary>
internal readonly record struct ProvidedTranslationSet(
    string Contract,
    string Language);

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
    /// <c>LOC001</c>: a set lists <c>ITranslations</c> in an assembly that declares no languages.
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
    /// <c>LOC007</c>: a set lists <c>ITranslations</c> instead of implementing its contract.
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
