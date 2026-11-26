using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Finds the localization sets of the current compilation: concrete, non-generic, top-level, and assembly-accessible
/// classes implementing <c>ILocalizationSet</c>, themselves or through a class they derive from. A class that implements
/// only a contract, such as a test double, is no set, so its name is never read as a language. A set marked
/// <c>[ExcludeFromLocalizationDiscovery]</c> is found too, but flagged (<see cref="DiscoveredLocalization.IsExcluded"/>):
/// it is never registered or checked, and counts only as a set of its language. A generic, nested, or file-local set can
/// still be registered by hand, and takes no part in the
/// language checks, with two exceptions that the same pass finds (<see cref="Read"/>): a nested or file-local set that
/// rewords another assembly's texts, which an app would otherwise lose without a word, and a class of such a shape, or
/// abstract, that lists <c>ILocalizationSet</c> next to a contract that binds to nothing yet, as a default-language set
/// does, and so gets no contract.
/// </summary>
/// <remarks>
/// A contract written by <see cref="LocalizationSetsPart"/> does not exist yet when the generator runs (it sees the
/// compilation before its own output is added), so a same-assembly set implementing it (<c>ShopLocalizationNl :
/// ILocalizationSet, IShopLocalization</c>) binds that base type to an error type. Such a set is kept with its unresolved
/// base types, and the generator ties it to a contract the same compilation generates when one of them names it
/// (<see cref="FindGeneratedContract"/>). Where two generated contracts share a name, the compiler binds it
/// (<see cref="BindContracts"/>).
/// </remarks>
internal static partial class LocalizationDiscovery
{
    /// <summary>
    /// The name of the pipeline step that holds what discovery found for the contracts (<see cref="Discovery"/>,
    /// <see cref="DiscoveredTexts.OfContract"/>), for a test to read it.
    /// </summary>
    internal const string ContractTrackingName = "Discovery.OfContract";

    /// <summary>
    /// The name of the pipeline step that holds what discovery found for the registrar (<see cref="Discovery"/>,
    /// <see cref="DiscoveredTexts.OfRegistrar"/>), for a test to read it.
    /// </summary>
    internal const string RegistrarTrackingName = "Discovery.OfRegistrar";

    /// <summary>
    /// The member of <c>ILocalizationSet</c> that gives a set its language, which is no text.
    /// </summary>
    public const string LanguageCodeMemberName = "LocalizationLanguageCode";

    /// <summary>
    /// Per file of a compilation, whether a base type written in a scope of that file leads to a set
    /// (<see cref="LeadsToSet"/>), by the scope and the words it is written in.
    /// </summary>
    // Discovery asks this of every class with a base list on every edit, and nearly all of them list what leads to no
    // set. The same words in the same scope name the same type, so each is bound once per file instead of once per
    // class, and a class that lists nothing of interest is passed by without its own base list being bound at all.
    // Held by the semantic model, which is made anew for every file of every compilation: nothing outlives the
    // compilation it was read from, and nothing is shared between two of them.
    private static readonly ConditionalWeakTable<SemanticModel, Dictionary<(SyntaxNode Scope, string Name), bool>> BaseTypesLeadingToSet = new();

    private static SymbolDisplayFormat FullyQualifiedFormat { get; } = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// A class as a cref names it inside its namespace, with its type parameters: <c>DutchBase&lt;T&gt;</c>.
    /// </summary>
    private static SymbolDisplayFormat CrefNameFormat { get; } = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// What one pass over the classes of the current compilation finds: the candidate sets, the nested or file-local
    /// sets that reword another assembly's texts, and the partial classes listing <c>ILocalizationSet</c> that no
    /// contract can come from because of their shape. Once as the part of the generator that writes the contracts reads
    /// it (<see cref="DiscoveredTexts.OfContract"/>), and once as the part that writes the registrar reads it
    /// (<see cref="DiscoveredTexts.OfRegistrar"/>).
    /// </summary>
    // One syntax provider for all of it: a provider re-runs its transform for every class with a base list on every
    // edit, and each pass costs its own walk over them. Nearly every such class is no set, so the transform first asks
    // what its base list names (MayBeSet), which costs one binding per name and file, and resolves the class only when
    // that leads on. A set is then read once with every text either part uses, and each part takes its own share of it,
    // class by class (ForPart). So an edit that changes only what one part uses leaves the other's pipeline as it was.
    // The registrar's share holds no wording and no location. A location holds the syntax tree of its file, which every
    // edit in that file renews, so a share with one location in it would change with every wording.
    public static (IncrementalValueProvider<Discovery> OfContract, IncrementalValueProvider<Discovery> OfRegistrar) Read(
        IncrementalGeneratorInitializationContext context)
    {
        var results = context.SyntaxProvider
            .CreateSyntaxProvider(predicate: IsCandidate, transform: ReadClass)
            .Where(static result => result is not null)
            .Select(static (result, _) => result!);

        var ofContract = Collect(results.Select(static (result, _) => ForPart(result, DiscoveredTexts.OfContract)), ContractTrackingName);
        var ofRegistrar = Collect(results.Select(static (result, _) => ForPart(result, DiscoveredTexts.OfRegistrar)), RegistrarTrackingName);

        return (ofContract, ofRegistrar);
    }

    /// <summary>
    /// What discovery found in every class, as one part of the generator reads it.
    /// </summary>
    private static IncrementalValueProvider<Discovery> Collect(IncrementalValuesProvider<DiscoveryResult> results, string trackingName)
    {
        var valueProvider = results
            .Collect()
            .Select(static (results, _) => new Discovery(
                new EquatableArray<DiscoveredLocalization>([.. results.Where(static result => result.Set is not null).Select(static result => result.Set!.Value)]),
                new EquatableArray<UnregistrableLocalization>([
                    .. results
                        .Where(static result => result.UnnameableOverride is not null)
                        .Select(static result => result.UnnameableOverride!.Value)
                        .Distinct()
                        .OrderBy(static set => set.MetadataName, StringComparer.Ordinal),
                ]),
                new EquatableArray<UnextendableSet>([
                    .. results
                        .Where(static result => result.UnextendableSet is not null)
                        .Select(static result => result.UnextendableSet!.Value)
                        .Distinct()
                        .OrderBy(static set => set.MetadataName, StringComparer.Ordinal),
                ])))
            .WithTrackingName(trackingName);

        return valueProvider;
    }

    /// <summary>
    /// The contract a default-language set gives rise to, or <see langword="null"/> when its name does not end in the
    /// language. The suffix is the language in PascalCase (<c>ShopLocalizationEn</c>, <c>ShopLocalizationZhHans</c>),
    /// and counts in any letter case that starts with a capital, as a set's name gives it its language
    /// (<see cref="SupportedLanguages.EndsInTypeSuffix"/>).
    /// </summary>
    public static string? DeriveBaseName(string className, string language)
    {
        var baseName = SupportedLanguages.EndsInTypeSuffix(className, language)
            ? className.Substring(startIndex: 0, className.Length - SupportedLanguages.ToTypeSuffix(language).Length)
            : null;

        return baseName;
    }

    /// <summary>
    /// A partial set is discovered once per declaration that has a base list: one set, with the unresolved base types of
    /// all of them.
    /// </summary>
    public static ImmutableArray<DiscoveredLocalization> Merge(EquatableArray<DiscoveredLocalization> discovered)
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
    /// The contracts the current compilation generates, worked out once for both parts of the generator, from what the
    /// registrar's part reads of the sets (<see cref="DiscoveredTexts.OfRegistrar"/>). That holds everything that decides
    /// where a contract comes from. It holds no wording and no location, so it stays as it was when a wording changes,
    /// and this step doesn't run again. A partial set counts with the base types of all its declarations
    /// (<see cref="Merge"/>).
    /// </summary>
    public static IncrementalValueProvider<GeneratedContracts> ReadGeneratedContracts(IncrementalValueProvider<Discovery> registrarDiscovery,
        IncrementalValueProvider<DeclaredLanguages?> languages, IncrementalValueProvider<EquatableArray<ContractBinding>> bindings)
    {
        var contracts = registrarDiscovery
            .Combine(languages)
            .Combine(bindings)
            .Select(static (data, _) => GetGeneratedContracts(Merge(data.Left.Left.Sets), data.Left.Right, data.Right))
            .WithTrackingName(GeneratedContractsTrackingName);

        return contracts;
    }

    /// <summary>
    /// The name of the pipeline step that holds the contracts the compilation generates
    /// (<see cref="ReadGeneratedContracts"/>), for a test to read it.
    /// </summary>
    internal const string GeneratedContractsTrackingName = "GeneratedContracts";

    /// <summary>
    /// The contracts the current compilation generates, from its sets and languages. A same-assembly set can implement
    /// such a contract while it doesn't exist yet. The bindings say which of two contracts of one name a base type
    /// names (<see cref="BindContracts"/>).
    /// </summary>
    public static GeneratedContracts GetGeneratedContracts(IEnumerable<DiscoveredLocalization> sets, DeclaredLanguages? languages,
        EquatableArray<ContractBinding> bindings)
    {
        if (languages is not { } declared)
            return new GeneratedContracts(ImmutableArray<string>.Empty, bindings);

        var starts = GetContractStarts(sets, declared);

        // Every set lists ILocalizationSet next to its contract, so a second set of the default language has the shape
        // of a default set too. The contract it lists tells the two apart. So a second pass leaves out each set that
        // lists only contracts the first pass found for other sets.
        var candidates = new GeneratedContracts(GetContractNames(starts), bindings);
        var contractNames = GetContractNames(starts.Where(set => IsDefaultSet(set, declared.DefaultLanguage, candidates)));

        return new GeneratedContracts(contractNames, bindings);
    }

    /// <summary>
    /// The sets that have the shape of a default-language set, from which the contracts come.
    /// </summary>
    private static DiscoveredLocalization[] GetContractStarts(IEnumerable<DiscoveredLocalization> sets, DeclaredLanguages declared)
    {
        // An excluded set is registered by hand, if at all, so no contract comes from it. A set the generated code
        // can't create (LOC200) still gives its contract, which it lists, and gets no texts.
        var starts = sets.Where(set => !set.IsExcluded && ReadsAsDefaultSet(set, declared.DefaultLanguage)).ToArray();
        return starts;
    }

    /// <summary>
    /// The full names of the contracts that default-language sets give rise to, in order.
    /// </summary>
    private static ImmutableArray<string> GetContractNames(IEnumerable<DiscoveredLocalization> defaultSets)
    {
        var names = defaultSets
            .Select(static set => GetOwnGeneratedContract(set))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray();

        return names;
    }

    /// <summary>
    /// The full name of the contract a default-language set gives rise to, or <see langword="null"/> when its name does
    /// not end in its language (<see cref="DeriveBaseName"/>).
    /// </summary>
    private static string? GetOwnGeneratedContract(DiscoveredLocalization set)
    {
        var contract = set.Language is { } language && DeriveBaseName(set.Name, language) is { } baseName
            ? GetContractFullName(set.Namespace, baseName)
            : null;

        return contract;
    }

    /// <summary>
    /// Whether a class lists contracts generated here, and none of them is <paramref name="ownContract"/>: it's a set of
    /// those contracts, and not the start of a contract of its own. A class that lists no generated contract, or its own
    /// among others, can still start one.
    /// </summary>
    public static bool ListsOnlyOtherGeneratedContracts(EquatableArray<UnresolvedBaseType> baseTypes,
        GeneratedContracts generatedContracts, string? ownContract)
    {
        var listsAny = false;

        foreach (var baseType in baseTypes)
        {
            if (FindGeneratedContract(baseType, generatedContracts) is not { } found)
                continue;

            if (String.Equals(found, ownContract, StringComparison.Ordinal))
                return false;

            listsAny = true;
        }

        return listsAny;
    }

    /// <summary>
    /// The full name of the contract generated from a default-language set: <c>Shop.IShopLocalization</c> for
    /// <c>Shop.ShopLocalizationEn</c>.
    /// </summary>
    public static string GetContractFullName(string @namespace, string baseName)
    {
        var contractName = @namespace.Length > 0 ? $"{@namespace}.I{baseName}" : $"I{baseName}";
        return contractName;
    }

    /// <summary>
    /// The generated contract an unresolved base type will bind to once it exists, or <see langword="null"/> when it
    /// names none. A qualified name names the contract in its qualifier's namespace. A simple name names the one
    /// generated contract of that name. Where two share it, the compiler said which (<see cref="BindContracts"/>).
    /// </summary>
    // Not by the name alone where two share it: two default sets of one name in different namespaces generate two
    // contracts of one name, and a set implementing one must not count as a language of the other. A simple name that
    // one contract has counts for it, also where the set can't reach it, because the compiler then reports that.
    public static string? FindGeneratedContract(UnresolvedBaseType baseType, GeneratedContracts generatedContracts)
    {
        if (baseType.IsQualified)
        {
            if (baseType.Namespace is not { } @namespace)
                return null;

            var fullName = @namespace.Length > 0 ? $"{@namespace}.{baseType.Name}" : baseType.Name;

            return generatedContracts.Contains(fullName) ? fullName : null;
        }

        if (!generatedContracts.TryFindBySimpleName(baseType.Name, out var found))
            return null;

        // Two share the name: the contract the compiler bound it to, as long as the compilation still generates it.
        found ??= generatedContracts.FindBinding(baseType.Site) is { } bound && generatedContracts.Contains(bound) ? bound : null;

        return found;
    }

    /// <summary>
    /// Whether one of a candidate's base types, or those of a class it derives from, will bind to a contract the same
    /// compilation generates. Pass <paramref name="contract"/> to ask for that contract alone.
    /// </summary>
    public static bool ListsGeneratedContract(DiscoveredLocalization candidate, GeneratedContracts generatedContracts, string? contract = null)
    {
        var listsGeneratedContract = candidate.UnresolvedBaseTypes.Any(baseType => FindGeneratedContract(baseType, generatedContracts) is { } found
                                                                                   && (contract is null || String.Equals(found, contract, StringComparison.Ordinal)));
        return listsGeneratedContract;
    }

    /// <summary>
    /// Whether a set is where a contract comes from: it has the shape of a default-language set
    /// (<see cref="ReadsAsDefaultSet"/>), and doesn't list only contracts generated from other sets
    /// (<see cref="ListsOnlyOtherGeneratedContracts"/>), as a second set of the default language does.
    /// </summary>
    public static bool IsDefaultSet(DiscoveredLocalization set, string? defaultLanguage, GeneratedContracts generatedContracts)
    {
        var isSet = ReadsAsDefaultSet(set, defaultLanguage)
                    && !ListsOnlyOtherGeneratedContracts(set.UnresolvedBaseTypes, generatedContracts, GetOwnGeneratedContract(set));

        return isSet;
    }

    /// <summary>
    /// Whether a set has the shape of the set a contract comes from: it lists <c>ILocalizationSet</c>, itself or
    /// through an abstract class it derives from (<see cref="ListsLocalizationSet"/>), in the default language,
    /// implements no contract already (<see cref="ImplementsContract"/>), and rewords no set
    /// (<see cref="RewordsSet"/>). Without a default language to go by (<see cref="DeclaredLanguages.DefaultLanguage"/>),
    /// a set of any language the build can read is taken for it, so that its contract is still written while the
    /// attribute is wrong. Every language's set of one contract then gives the same contract, which is written once.
    /// </summary>
    private static bool ReadsAsDefaultSet(DiscoveredLocalization set, string? defaultLanguage)
    {
        var readsAsDefaultSet = set.ListsLocalizationSet
               && !ImplementsContract(set)
               && !RewordsSet(set)
               && set.Language is not null
               && (defaultLanguage is null || String.Equals(set.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase));

        return readsAsDefaultSet;
    }

    /// <summary>
    /// Whether a set reads as a default-language set whose name gives it no language, such as <c>ShopLocalization</c>
    /// or <c>ShopTexts</c>. It lists <c>ILocalizationSet</c> next to a contract that binds to nothing and that no
    /// other set starts, as the default-language set lists the contract generated from it. Such a set gets no
    /// contract, which <c>LOC200</c> reports with the name to give it.
    /// </summary>
    // Only with such a contract in its base list. A set that lists a contract another set starts is a set of that
    // contract in a language the build can't read from its name, which LOC208 reports.
    public static bool IsDefaultSetWithoutLanguage(DiscoveredLocalization set, GeneratedContracts generatedContracts)
    {
        var isDefaultSetWithoutLanguage = set is { NameGivesNoLanguage: true, ListsLocalizationSet: true, IsExcluded: false, UnresolvedBaseTypes.Length: > 0 }
            && !ImplementsContract(set)
            && !RewordsSet(set)
            && !set.UnresolvedBaseTypes.Any(baseType => FindGeneratedContract(baseType, generatedContracts) is not null);

        return isDefaultSetWithoutLanguage;
    }

    /// <summary>
    /// What keeps generated code from creating a default-language set, as the clause of the <c>LOC200</c> message, or
    /// <see langword="null"/> when it can: what keeps any set from registering (<c>new T()</c>), and besides that a
    /// parameterless constructor that is not public, which <c>GeneratedCode.GetDefaultSet</c> asks for
    /// (<c>where TSet : new()</c>).
    /// </summary>
    // Not a language code the build can't read, which keeps the set from registering, but not from being created. A
    // default-language set with such a code gets LOC200 for its language instead (ReportDefaultSetsOfAnotherLanguage).
    public static string? GetCreationProblem(DiscoveredLocalization set)
    {
        var registrationProblem = String.Equals(set.RegistrationProblem, UnreadableLanguageCodeProblem, StringComparison.Ordinal)
            ? null
            : set.RegistrationProblem;

        var problem = registrationProblem
               ?? (set.HasPublicParameterlessConstructor ? null : "it has no public parameterless constructor");

        return problem;
    }

    /// <summary>
    /// Whether a set implements a contract the input compilation knows: one written by hand in its own assembly,
    /// whatever its accessibility, a public one of any assembly, or an internal one of another assembly that it sees
    /// through <c>InternalsVisibleTo</c>. Such a set rewords or translates that contract, and starts none of its own.
    /// </summary>
    public static bool ImplementsContract(DiscoveredLocalization set)
    {
        var implementsContract = set.ContractNames.Length > 0 || set.OwnContractNames.Length > 0 || set.ImplementsInternalContract;
        return implementsContract;
    }

    /// <summary>
    /// Whether a set derives from a concrete set that implements <c>ILocalizationSet</c> in the input compilation.
    /// Listing <c>ILocalizationSet</c> again is then how it implements <c>LocalizationLanguageCode</c> (<c>en-GB</c> on <c>en</c>),
    /// not how it starts a contract.
    /// </summary>
    public static bool RewordsSet(DiscoveredLocalization set)
    {
        var rewordsSet = set.BaseSet is { Contracts.Length: 0 };
        return rewordsSet;
    }

    private static string GetFullyQualifiedName(INamedTypeSymbol type)
    {
        return type.ToDisplayString(FullyQualifiedFormat);
    }

    /// <summary>
    /// Whether an assembly references this package. Only such an assembly can hold its attributes or a generated
    /// registrar, which keeps the framework's assemblies out of every scan of the references.
    /// </summary>
    public static bool ReferencesPackage(IAssemblySymbol assembly)
    {
        return assembly.Modules.First().ReferencedAssemblies.Any(
            static identity => String.Equals(identity.Name, PackageNames.Assembly, StringComparison.Ordinal));
    }

    /// <summary>
    /// Cheap syntactic pre-filter: a class or record class with a base list could implement the interface. A record
    /// struct is no set, and is left out before anything is bound.
    /// </summary>
    private static bool IsCandidate(SyntaxNode node, CancellationToken _)
    {
        var isCandidate = node is ClassDeclarationSyntax { BaseList: not null }
            or RecordDeclarationSyntax { BaseList: not null, ClassOrStructKeyword.RawKind: not (int)SyntaxKind.StructKeyword };

        return isCandidate;
    }

    /// <summary>
    /// What one part of the generator reads of what discovery found in a class
    /// (<see cref="ForPart(DiscoveredLocalization, DiscoveredTexts)"/>). The registrar's share holds no location, so
    /// it leaves out a set that is only reported (<see cref="DiscoveryResult.UnnameableOverride"/>), and where a class
    /// the generator can't extend is written.
    /// </summary>
    internal static DiscoveryResult ForPart(DiscoveryResult result, DiscoveredTexts texts)
    {
        var forPart = result with { Set = result.Set is { } set ? ForPart(set, texts) : null };

        if (texts is DiscoveredTexts.OfRegistrar)
        {
            forPart = forPart with
            {
                UnnameableOverride = null,
                UnextendableSet = result.UnextendableSet is { } unextendable
                    ? unextendable with { Location = default, BaseTypeLocations = default }
                    : null,
            };
        }

        return forPart;
    }

    /// <summary>
    /// The share of a set that one part of the generator reads, of what <see cref="DiscoverSet"/> reads for both: the
    /// texts a contract is written from, with every location, or the names the registrar passes on, without one. So an
    /// edit that changes only the other share leaves that part's pipeline as it was.
    /// </summary>
    internal static DiscoveredLocalization ForPart(DiscoveredLocalization set, DiscoveredTexts texts)
    {
        var forPart = texts switch
        {
            DiscoveredTexts.OfContract => set with { OwnTexts = default },
            // The registrar's part asks which enums a set holds the texts of (EnumNames), so that an enum doesn't get a
            // second GetText from its [Localized] members, and reads nothing else of its texts. It holds no location
            // either. Every other field of a set is a name or a number, which an edit of a wording leaves as it was.
            DiscoveredTexts.OfRegistrar => set with { OfContract = default, Locations = default },
            _ => set,
        };

        return forPart;
    }

    /// <summary>
    /// Resolves a candidate to what discovery finds in it for both parts of the generator, or <see langword="null"/>
    /// when it holds nothing of interest.
    /// </summary>
    private static DiscoveryResult? ReadClass(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;

        if (!MayBeSet(declaration, context.SemanticModel, cancellationToken)
            || context.SemanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol { TypeKind: TypeKind.Class } type)
        {
            return null;
        }

        // Generated code writes new T(), so the type must be concrete and closed, and nameable from another file: a
        // nested set could be private, and a file-local type reports internal while no other file can name it.
        if (type.IsAbstract || type.IsGenericType || type.ContainingType is not null || type.IsFileLocal)
            return DiscoverUnnameable(type, declaration, context.SemanticModel, cancellationToken);

        // Only a class that implements ILocalizationSet is a set: itself, through a class it derives from, or through an
        // interface. A class that implements only a contract, such as a test double, is none, so its name is never read
        // as a language. Ruled out before any declaration of an ancestor gets a semantic model of its own.
        if (!ImplementsLocalizationSet(type))
            return null;

        var result = new DiscoveryResult(
            DiscoverSet(type, declaration, context.SemanticModel, cancellationToken),
            UnnameableOverride: null,
            UnextendableSet: null);

        return result;
    }

    /// <summary>
    /// Whether a declaration may be that of a set: one of the base types it lists leads to a set
    /// (<see cref="LeadsToSet"/>). A class is a set, or a candidate for one, only through what its base lists name, so a
    /// declaration that lists nothing of the kind is none, and a partial class is found through the part that does.
    /// </summary>
    private static bool MayBeSet(TypeDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        // The type parameters of a class are in reach of its own base list, where the same words may name another type
        // than they do for the class next to it. Such a class is asked by itself.
        var known = declaration is { TypeParameterList: null, Parent: not null }
            ? BaseTypesLeadingToSet.GetOrCreateValue(semanticModel)
            : null;

        foreach (var baseType in declaration.BaseList!.Types)
        {
            if (known is null)
            {
                if (LeadsToSet(baseType.Type, semanticModel, cancellationToken))
                    return true;

                continue;
            }

            var key = (declaration.Parent!, baseType.Type.ToString());
            bool leadsToSet;

            // The files of a compilation may be read at the same time, each with a model of its own; the lock is for a
            // host that would read one file from two threads.
            lock (known)
            {
                if (!known.TryGetValue(key, out leadsToSet))
                    known[key] = leadsToSet = LeadsToSet(baseType.Type, semanticModel, cancellationToken);
            }

            if (leadsToSet)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a base type, as a base list names it, leads to a set: it is <c>ILocalizationSet</c> or implements it, it
    /// is a class that lists, itself or through a class it derives from, a type that binds to nothing, as a contract
    /// generated here does, or it binds to nothing itself.
    /// </summary>
    // Whatever does not bind to a type counts, so that nothing is passed by for a reason this does not know.
    private static bool LeadsToSet(TypeSyntax baseType, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (semanticModel.GetSymbolInfo(baseType, cancellationToken).Symbol is not INamedTypeSymbol { TypeKind: not TypeKind.Error } type)
            return true;

        var leadsToSet = IsLocalizationSetInterface(type)
               || ImplementsLocalizationSet(type)
               || PackageNames.IsContract(type)
               || ImplementsAnyContract(type)
               || (type.TypeKind is TypeKind.Class && HasUnboundBaseType(type));

        return leadsToSet;
    }
}
