using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Discovers the sets of the current compilation: concrete, non-generic, top-level, assembly-accessible classes that
/// implement <c>ILocalizationSet</c>, themselves or through a base class. A class that implements only a contract, such
/// as a test double, is no set. A set marked <c>[ExcludeFromLocalizationDiscovery]</c> is flagged
/// (<see cref="DiscoveredLocalization.IsExcluded"/>), and counts only as a set of its language.
/// </summary>
/// <remarks>
/// <para>
/// A generic, nested or file-local set takes no part in the checks, with two exceptions (<see cref="Read"/>):
/// </para>
/// <list type="bullet">
/// <item>a nested or file-local rewording of another assembly's texts, which an app would otherwise lose silently;</item>
/// <item>a class of such a shape, or abstract, that lists <c>ILocalizationSet</c> and an unresolved contract, and so
/// gets no contract.</item>
/// </list>
/// <para>
/// A contract written by <see cref="LocalizationSetsPart"/> doesn't exist yet when the generator runs, so a set that
/// implements it binds that base type to an error type. The set is kept with its unresolved base types and tied to a
/// generated contract (<see cref="FindGeneratedContract"/>, <see cref="BindContracts"/>).
/// </para>
/// </remarks>
internal static partial class LocalizationDiscovery
{
    /// <summary>
    /// The pipeline step of <see cref="DiscoveredTexts.OfContract"/>, for a test to read.
    /// </summary>
    internal const string ContractTrackingName = "Discovery.OfContract";

    /// <summary>
    /// The pipeline step of <see cref="DiscoveredTexts.OfRegistrar"/>, for a test to read.
    /// </summary>
    internal const string RegistrarTrackingName = "Discovery.OfRegistrar";

    /// <summary>
    /// Per file, whether a base type leads to a set (<see cref="LeadsToSet"/>), by its scope and the words it's written in.
    /// </summary>
    // The same words in the same scope name the same type, so each is bound once per file instead of once per class.
    // Held by the semantic model, which is made anew for every file of every compilation, so nothing outlives it.
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
    /// What one pass over the classes of the compilation finds (<see cref="Discovery"/>), once in each part's share
    /// (<see cref="DiscoveredTexts"/>).
    /// </summary>
    // One syntax provider, because each provider walks every class with a base list on every edit. The transform first
    // asks what the base list names (MayBeSet), and resolves the class only when that leads on. Each part takes its own
    // share (ForPart), so an edit that changes only what one part uses leaves the other's pipeline as it was.
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
                ]),
                new EquatableArray<SetDescription>([
                    .. results
                        .Where(static result => result.NestedSet is not null)
                        .Select(static result => result.NestedSet!.Value)
                        .Distinct()
                        .OrderBy(static set => set.MetadataName, StringComparer.Ordinal),
                ])))
            .WithTrackingName(trackingName);

        return valueProvider;
    }

    /// <summary>
    /// The contract a default-language set gives rise to, or <see langword="null"/> when its name doesn't end in the
    /// language in PascalCase (<c>ShopLocalizationZhHans</c>, see <see cref="SupportedLanguages.EndsInTypeSuffix"/>).
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
    /// The contracts the current compilation generates, for both parts of the generator. It reads the registrar's share
    /// (<see cref="DiscoveredTexts.OfRegistrar"/>), so it doesn't run again when a wording changes.
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
    /// The pipeline step of <see cref="ReadGeneratedContracts"/>, for a test to read.
    /// </summary>
    internal const string GeneratedContractsTrackingName = "GeneratedContracts";

    /// <summary>
    /// The contracts the current compilation generates, from its sets and languages.
    /// </summary>
    public static GeneratedContracts GetGeneratedContracts(IEnumerable<DiscoveredLocalization> sets, DeclaredLanguages? languages,
        EquatableArray<ContractBinding> bindings)
    {
        if (languages is not { } declared)
            return new GeneratedContracts(ImmutableArray<string>.Empty, bindings);

        var starts = GetContractStarts(sets, declared);

        // A second set of the default language has the shape of a default set too. So a second pass leaves out each set
        // that lists only contracts the first pass found for other sets.
        var candidates = new GeneratedContracts(GetContractNames(starts), bindings);
        var contractNames = GetContractNames(starts.Where(set => IsDefaultSet(set, declared.DefaultLanguage, candidates)));

        return new GeneratedContracts(contractNames, bindings);
    }

    /// <summary>
    /// The sets that have the shape of a default-language set, from which the contracts come.
    /// </summary>
    private static DiscoveredLocalization[] GetContractStarts(IEnumerable<DiscoveredLocalization> sets, DeclaredLanguages declared)
    {
        // No contract comes from an excluded set. A set generated code can't create (LOC200) still gives its contract.
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
    /// Whether a class lists contracts generated here and none of them is <paramref name="ownContract"/>. It's then a set
    /// of those contracts, and starts no contract of its own.
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
    /// contract of that name, or where two share it, the one the compiler bound (<see cref="BindContracts"/>).
    /// </summary>
    // A simple name counts even where the set can't reach the contract, because the compiler then reports that.
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

        // Two share the name, so the bound one, if the compilation still generates it.
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
    /// Whether a contract comes from a set: it has the shape of a default-language set (<see cref="ReadsAsDefaultSet"/>),
    /// and doesn't list only contracts of other sets (<see cref="ListsOnlyOtherGeneratedContracts"/>).
    /// </summary>
    public static bool IsDefaultSet(DiscoveredLocalization set, string? defaultLanguage, GeneratedContracts generatedContracts)
    {
        var isSet = ReadsAsDefaultSet(set, defaultLanguage)
                    && !ListsOnlyOtherGeneratedContracts(set.UnresolvedBaseTypes, generatedContracts, GetOwnGeneratedContract(set));

        return isSet;
    }

    /// <summary>
    /// Whether a set has the shape of the set a contract comes from: it lists <c>ILocalizationSet</c>
    /// (<see cref="ListsLocalizationSet"/>), is of the default language, implements no contract
    /// (<see cref="ImplementsContract"/>), and isn't a rewording (<see cref="RewordsSet"/>). Without a
    /// <see cref="DeclaredLanguages.DefaultLanguage"/>, a set of any language counts, so the contract is still written
    /// while the attribute is wrong.
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
    /// Whether a set reads as a default-language set whose name gives it no language, such as <c>ShopTexts</c>. It
    /// lists <c>ILocalizationSet</c> and an unresolved contract no other set starts. It gets no contract, which
    /// <c>LOC200</c> reports with the name to give it.
    /// </summary>
    // A set that lists a contract another set starts is a set of that contract instead, which LOC207 reports.
    public static bool IsDefaultSetWithoutLanguage(DiscoveredLocalization set, GeneratedContracts generatedContracts)
    {
        var isDefaultSetWithoutLanguage = set is { Language: null, ListsLocalizationSet: true, IsExcluded: false, UnresolvedBaseTypes.Length: > 0 }
            && !ImplementsContract(set)
            && !RewordsSet(set)
            && !set.UnresolvedBaseTypes.Any(baseType => FindGeneratedContract(baseType, generatedContracts) is not null);

        return isDefaultSetWithoutLanguage;
    }

    /// <summary>
    /// What keeps generated code from creating a default-language set, as the clause of the <c>LOC200</c> message, or
    /// <see langword="null"/>. <c>GeneratedCode.GetDefaultSet</c> asks for a public parameterless constructor.
    /// </summary>
    public static string? GetCreationProblem(DiscoveredLocalization set)
    {
        var problem = set.RegistrationProblem
               ?? (set.HasPublicParameterlessConstructor ? null : "it has no public parameterless constructor");

        return problem;
    }

    /// <summary>
    /// Whether a set implements a contract the input compilation knows, so it starts none of its own. That's one written
    /// by hand in its assembly, a public one, or an internal one it sees through <c>InternalsVisibleTo</c>.
    /// </summary>
    public static bool ImplementsContract(DiscoveredLocalization set)
    {
        var implementsContract = set.ContractNames.Length > 0 || set.OwnContractNames.Length > 0 || set.ImplementsInternalContract;
        return implementsContract;
    }

    /// <summary>
    /// Whether a set derives from a concrete set that implements <c>ILocalizationSet</c> in the input compilation.
    /// Listing <c>ILocalizationSet</c> again then doesn't start a contract.
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
    /// Whether an assembly references this package, and so can hold its attributes or a generated registrar.
    /// </summary>
    public static bool ReferencesPackage(IAssemblySymbol assembly)
    {
        return assembly.Modules.First().ReferencedAssemblies.Any(
            static identity => String.Equals(identity.Name, PackageNames.Assembly, StringComparison.Ordinal));
    }

    /// <summary>
    /// Syntactic pre-filter: a class or record class with a base list could implement the interface.
    /// </summary>
    private static bool IsCandidate(SyntaxNode node, CancellationToken _)
    {
        var isCandidate = node is ClassDeclarationSyntax { BaseList: not null }
            or RecordDeclarationSyntax { BaseList: not null, ClassOrStructKeyword.RawKind: not (int)SyntaxKind.StructKeyword };

        return isCandidate;
    }

    /// <summary>
    /// What one part of the generator reads of what discovery found in a class. The registrar's share holds no location,
    /// so it leaves out <see cref="DiscoveryResult.UnnameableOverride"/>, which is only reported.
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
    /// The share of a set that one part of the generator reads, of what <see cref="DiscoverSet"/> reads for both.
    /// </summary>
    internal static DiscoveredLocalization ForPart(DiscoveredLocalization set, DiscoveredTexts texts)
    {
        var forPart = texts switch
        {
            // The registrar reads only EnumNames of the texts, so an enum doesn't get a second GetText. Every field left
            // is a name or a number, which an edit of a wording leaves as it was.
            DiscoveredTexts.OfRegistrar => set with { OfContract = default, Locations = default },
            _ => set,
        };

        return forPart;
    }

    /// <summary>
    /// Resolves a candidate to what discovery finds in it for both parts of the generator, or <see langword="null"/>
    /// when it holds nothing of interest. A class whose file and whose bases' files didn't change gives what it gave
    /// before, where no declaration changed elsewhere (<see cref="TransformCache"/>).
    /// </summary>
    private static DiscoveryResult? ReadClass(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;

        return TransformCache.GetOrRead(declaration, context.SemanticModel.Compilation, (declaration, context.SemanticModel),
            static (state, cancellationToken) => ReadClass(state.declaration, state.SemanticModel, cancellationToken), cancellationToken);
    }

    private static DiscoveryResult? ReadClass(TypeDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (!MayBeSet(declaration, semanticModel, cancellationToken)
            || semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol { TypeKind: TypeKind.Class } type)
        {
            return null;
        }

        RecordDeclarations(type);

        // Generated code writes new T(), so the type must be concrete, closed and nameable from another file.
        if (type.IsAbstract || type.IsGenericType || type.ContainingType is not null || type.IsFileLocal)
            return DiscoverUnnameable(type, declaration, semanticModel, cancellationToken);

        // A class that implements only a contract, such as a test double, is no set. Ruled out before an ancestor's file
        // gets a semantic model.
        if (!ImplementsLocalizationSet(type))
            return null;

        var result = new DiscoveryResult(
            DiscoverSet(type, declaration, semanticModel, cancellationToken),
            UnnameableOverride: null,
            UnextendableSet: null);

        return result;
    }

    /// <summary>
    /// Records the files of every part of a class and of the classes of its assembly it derives from, whose members
    /// discovery reads, such as a doc comment on a text (<see cref="TransformCache.RecordRead"/>).
    /// </summary>
    private static void RecordDeclarations(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is { TypeKind: TypeKind.Class } && IsDeclaredIn(current, type.ContainingAssembly); current = current.BaseType)
        {
            foreach (var reference in current.DeclaringSyntaxReferences)
                TransformCache.RecordRead(reference.SyntaxTree);
        }
    }

    /// <summary>
    /// Whether one of the base types a declaration lists leads to a set (<see cref="LeadsToSet"/>). A partial class is
    /// found through the part that does.
    /// </summary>
    private static bool MayBeSet(TypeDeclarationSyntax declaration, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        // A generic class's type parameters can change what its base list names, so it isn't cached.
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

            // Each file has its own model, and the lock is for a host that reads one file from two threads.
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
    /// Whether a base type leads to a set: it is or implements <c>ILocalizationSet</c> or a contract, it's a class with an
    /// unresolved base type, or it's unresolved itself.
    /// </summary>
    // Whatever doesn't bind counts, so nothing is passed by for a reason this doesn't know.
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
