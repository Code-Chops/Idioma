using System.Collections.Concurrent;
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
/// A contract written by <see cref="LocalizationSetsGenerator"/> does not exist yet when the generators run (they all see
/// the same input compilation), so a same-assembly set implementing it (<c>ShopLocalizationNl : ILocalizationSet,
/// IShopLocalization</c>) binds that base type to an error type. Such a set is kept with its unresolved base types, and
/// the generators tie it to a contract the same compilation generates when one of them names it
/// (<see cref="FindGeneratedContract"/>). Where two generated contracts share a name, the compiler binds it
/// (<see cref="BindContracts"/>).
/// </remarks>
internal static partial class LocalizationDiscovery
{
    /// <summary>
    /// The name of the pipeline step that holds what discovery found (<see cref="Discovery"/>), for a test to read it.
    /// </summary>
    internal const string TrackingName = "Discovery";

    private const string LanguageCodeMemberName = "LocalizationLanguageCode";

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

    /// <summary>
    /// Per file of a compilation, what discovery found in each class, for both generators.
    /// </summary>
    // Both generators read every class with a base list on every edit, and the driver gives them the same semantic model
    // of each file. So the first to come to a class reads what both use, and the second finds it here. Without this,
    // every set was read twice on every edit, which cost a project with hundreds of sets more than all else discovery
    // does. Held by the semantic model, as the base types above are: nothing outlives the compilation it was read from.
    private static readonly ConditionalWeakTable<SemanticModel, ConcurrentDictionary<SyntaxNode, DiscoveryResult?>> ReadOnce = new();

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
    /// contract can come from because of their shape.
    /// </summary>
    // One syntax provider for all three: a provider re-runs its transform for every class with a base list on every
    // edit, and each pass costs its own walk over them. The two generators calling this each build one; the binding the
    // first does is reused by the second. Nearly every such class is no set, so the transform first asks what its base
    // list names (MayBeSet), which costs one binding per name and file, and resolves the class only when that leads on.
    // A set is then read with every text it has, on every edit, so each generator reads only the texts it uses
    // (DiscoveredTexts).
    public static IncrementalValueProvider<Discovery> Read(IncrementalGeneratorInitializationContext context, DiscoveredTexts texts)
    {
        var valueProvider = context.SyntaxProvider
            .CreateSyntaxProvider<DiscoveryResult?>(
                predicate: IsCandidate,
                transform: texts is DiscoveredTexts.OfContract
                    ? static (context, cancellationToken) => Discover(context, DiscoveredTexts.OfContract, cancellationToken)
                    : static (context, cancellationToken) => Discover(context, DiscoveredTexts.OfRecord, cancellationToken))
            .Where(static result => result is not null)
            .Select(static (result, _) => result!)
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
            .WithTrackingName(TrackingName);

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
        found ??= generatedContracts.FindBinding(baseType.Location) is { } bound && generatedContracts.Contains(bound) ? bound : null;

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
    /// Whether a set has the shape of the set a contract comes from: it lists <c>ILocalizationSet</c> itself, in the
    /// default language, implements no contract already (<see cref="ImplementsContract"/>), and rewords no set
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
    // contract in a language the build can't read from its name, which LOC400 reports.
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
    /// parameterless constructor that is not public, which the texts' <c>DefaultSet&lt;TSet&gt;</c> asks for
    /// (<c>where TSet : new()</c>).
    /// </summary>
    public static string? GetCreationProblem(DiscoveredLocalization set)
    {
        var problem = set.RegistrationProblem
               ?? (set.HasPublicParameterlessConstructor ? null : "it has no public parameterless constructor");

        return problem;
    }

    /// <summary>
    /// Whether a set implements a contract the input compilation knows: one written by hand in its own assembly,
    /// whatever its accessibility, or a public one of any assembly. Such a set rewords or translates that contract, and
    /// starts none of its own.
    /// </summary>
    public static bool ImplementsContract(DiscoveredLocalization set)
    {
        var implementsContract = set.ContractNames.Length > 0 || set.OwnContractNames.Length > 0;
        return implementsContract;
    }

    /// <summary>
    /// Whether a set derives from a concrete set that implements <c>ILocalizationSet</c> in the input compilation.
    /// Listing <c>ILocalizationSet</c> again is then how it implements <c>LocalizationLanguageCode</c> (<c>en-GB</c> on <c>en</c>),
    /// not how it starts a contract.
    /// </summary>
    public static bool RewordsSet(DiscoveredLocalization set)
    {
        var rewordsSet = set is { BaseSetName: not null, BaseSetContracts.Length: 0 };
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
    /// Resolves a candidate to what discovery finds in it for one generator, or <see langword="null"/> when it holds
    /// nothing of interest. The class is read once for both generators (<see cref="ReadClass"/>).
    /// </summary>
    private static DiscoveryResult? Discover(GeneratorSyntaxContext context, DiscoveredTexts texts, CancellationToken cancellationToken)
    {
        var read = ReadOnce.GetOrCreateValue(context.SemanticModel);

        if (!read.TryGetValue(context.Node, out var result))
        {
            result = ReadClass(context, cancellationToken);
            read.TryAdd(context.Node, result);
        }

        var forGenerator = result?.Set is { } set ? result with { Set = ForGenerator(set, texts) } : result;
        return forGenerator;
    }

    /// <summary>
    /// The part of a set that one generator reads, as <see cref="DiscoverSet"/> gives it for <paramref name="texts"/>
    /// alone: the texts a contract is written from, or the names the registrar records. So an edit that changes only
    /// the other part leaves the generator's pipeline as it was.
    /// </summary>
    internal static DiscoveredLocalization ForGenerator(DiscoveredLocalization set, DiscoveredTexts texts)
    {
        var forGenerator = texts switch
        {
            DiscoveredTexts.OfContract => set with { OwnTexts = new EquatableArray<string>(ImmutableArray<string>.Empty) },
            DiscoveredTexts.OfRecord => set with
            {
                DeclaredTexts = new EquatableArray<string>(ImmutableArray<string>.Empty),
                InheritedTexts = new EquatableArray<InheritedText>(ImmutableArray<InheritedText>.Empty),
                Wordings = new EquatableArray<TextWording>(ImmutableArray<TextWording>.Empty),
                Texts = new EquatableArray<LocalizationMember>(ImmutableArray<LocalizationMember>.Empty),
                UnsupportedTexts = new EquatableArray<UnsupportedText>(ImmutableArray<UnsupportedText>.Empty),
                Usings = default,
                Enums = new EquatableArray<LocalizedEnum>(ImmutableArray<LocalizedEnum>.Empty),
                TypeNamedLikeTexts = null,
                ReservedTexts = new EquatableArray<ReservedText>(ImmutableArray<ReservedText>.Empty),
                UndeclaredTexts = new EquatableArray<UndeclaredText>(ImmutableArray<UndeclaredText>.Empty),
            },
            _ => set,
        };

        return forGenerator;
    }

    /// <summary>
    /// Resolves a candidate to what discovery finds in it for both generators, or <see langword="null"/> when it holds
    /// nothing of interest.
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
            DiscoverSet(type, declaration, context.SemanticModel, DiscoveredTexts.OfBoth, cancellationToken),
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
