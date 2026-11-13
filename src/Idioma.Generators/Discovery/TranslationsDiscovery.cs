namespace CodeChops.Idioma.Generators;

/// <summary>
/// Finds the translation sets of the current compilation: concrete, non-generic, top-level, and assembly-accessible
/// classes implementing <c>ITranslationSet</c>. A set marked <c>[ExcludeFromTranslationDiscovery]</c> is found too, but
/// flagged (<see cref="DiscoveredTranslations.IsExcluded"/>): it is never registered or checked, and counts only as a
/// set of its language. A generic, nested, or file-local set can still be registered by hand, and takes no part in the
/// language checks, with two exceptions that the same pass finds (<see cref="Read"/>): a nested or file-local set that
/// rewords another assembly's texts, which an app would otherwise lose without a word, and a partial class of such a
/// shape, or abstract, that lists <c>ITranslationSet</c> itself as a default-language set does, and so gets no
/// contract.
/// </summary>
/// <remarks>
/// A contract written by <see cref="TranslationSetsGenerator"/> does not exist yet when the generators run (they all see
/// the same input compilation), so a same-assembly set implementing it (<c>ShopTranslationsNL : IShopTranslations</c>)
/// binds that base type to an error type. Such a candidate is kept with its unresolved base types, each with the
/// namespaces the compiler looks it up in, and the generators accept it when one of them binds to a contract the same
/// compilation generates (<see cref="FindGeneratedContract"/>). A class whose base type comes from another generator, or
/// is misspelled, is no set and is left out.
/// </remarks>
internal static class TranslationsDiscovery
{
    private const string PackageAssemblyName = "CodeChops.Idioma";
    private const string TranslationSetInterfaceFullName = "CodeChops.Idioma.ITranslationSet";
    private const string ExclusionAttributeFullName = "CodeChops.Idioma.ExcludeFromTranslationDiscoveryAttribute";
    private const string LanguageCodeMemberName = "LanguageCode";

    private static SymbolDisplayFormat FullyQualifiedFormat { get; } = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// A class as a cref names it from another namespace, with its type parameters: <c>Wording.DutchBase&lt;T&gt;</c>.
    /// </summary>
    private static SymbolDisplayFormat CrefFullNameFormat { get; } = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// A class as a cref names it from its own namespace, with its type parameters: <c>DutchBase&lt;T&gt;</c>.
    /// </summary>
    private static SymbolDisplayFormat CrefNameFormat { get; } = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// What one pass over the classes of the current compilation finds: the candidate sets, the nested or file-local
    /// sets that reword another assembly's texts, and the partial classes listing <c>ITranslationSet</c> that no
    /// contract can come from because of their shape.
    /// </summary>
    // One syntax provider for all three: a provider re-runs its transform for every class with a base list on every
    // edit, and each pass costs its own walk over them. The two generators calling this each build one; the binding the
    // first does is reused by the second.
    public static IncrementalValueProvider<Discovery> Read(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: IsCandidate,
                transform: Discover)
            .Where(static result => result is not null)
            .Select(static (result, _) => result!)
            .Collect()
            .Select(static (results, _) => new Discovery(
                new EquatableArray<DiscoveredTranslations>([.. results.Where(static result => result.Set is not null).Select(static result => result.Set!.Value)]),
                new EquatableArray<UnregistrableTranslations>([
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
                ])));
    }

    /// <summary>
    /// The contract a default-language set gives rise to, or <see langword="null"/> when its name does not end in the
    /// language. A two-letter language takes an upper-case suffix (<c>ShopTranslationsEN</c>), and a longer one any
    /// letter case that starts with a capital (<c>ShopTranslationsZhHans</c>), as a set's name gives it its language
    /// (<see cref="SupportedLanguages.EndsInTypeSuffix"/>).
    /// </summary>
    public static string? DeriveBaseName(string className, string language)
    {
        return SupportedLanguages.EndsInTypeSuffix(className, language)
            ? className.Substring(startIndex: 0, className.Length - SupportedLanguages.ToTypeSuffix(language).Length)
            : null;
    }

    /// <summary>
    /// The full names of the contracts the current compilation generates, from its sets and languages: what a
    /// same-assembly set can implement while it does not exist yet.
    /// </summary>
    public static ImmutableArray<string> GetGeneratedContractNames(IEnumerable<DiscoveredTranslations> sets, DeclaredLanguages? languages)
    {
        if (languages is not { Codes.Length: > 0 } declared)
            return ImmutableArray<string>.Empty;

        var names = ImmutableArray.CreateBuilder<string>();

        // An excluded set is registered by hand, if at all, so no contract comes from it. Nor from a set the generated
        // code cannot extend or create (LOC002).
        foreach (var set in sets)
        {
            if (!set.IsExcluded
                && IsDefaultSet(set, declared.Codes[0])
                && set is { IsPartial: true, RegistrationProblem: null }
                && DeriveBaseName(set.Name, set.Language!) is { } baseName)
            {
                names.Add(GetContractFullName(set.Namespace, baseName));
            }
        }

        return names.Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal).ToImmutableArray();
    }

    /// <summary>
    /// The full name of the contract generated from a default-language set: <c>Shop.IShopTranslations</c> for
    /// <c>Shop.ShopTranslationsEN</c>.
    /// </summary>
    public static string GetContractFullName(string @namespace, string baseName)
    {
        return @namespace.Length > 0 ? $"{@namespace}.I{baseName}" : $"I{baseName}";
    }

    /// <summary>
    /// The generated contract an unresolved base type will bind to once it exists: the first namespace in the
    /// compiler's lookup order that holds one of that name, or <see langword="null"/> when none does.
    /// </summary>
    // In lookup order rather than by name alone: two default sets of one name in different namespaces generate two
    // contracts of one name, and a set implementing one must not count as a language of the other.
    public static string? FindGeneratedContract(UnresolvedBaseType baseType, ImmutableArray<string> generatedContracts)
    {
        foreach (var @namespace in baseType.Namespaces)
        {
            var fullName = @namespace.Length > 0 ? $"{@namespace}.{baseType.Name}" : baseType.Name;

            if (generatedContracts.Contains(fullName, StringComparer.Ordinal))
                return fullName;
        }

        return null;
    }

    /// <summary>
    /// Whether a candidate is a set: it implements <c>ITranslationSet</c> in the input compilation, or one of its base
    /// types will bind to a contract the same compilation generates.
    /// </summary>
    public static bool IsSet(DiscoveredTranslations candidate, ImmutableArray<string> generatedContracts)
    {
        return candidate.ImplementsTranslations
               || candidate.UnresolvedBaseTypes.Any(baseType => FindGeneratedContract(baseType, generatedContracts) is not null);
    }

    /// <summary>
    /// Whether a set is where a contract comes from: it lists <c>ITranslationSet</c> itself, in the default language,
    /// implements no contract already (<see cref="ImplementsContract"/>), and rewords no set (<see cref="RewordsSet"/>).
    /// </summary>
    public static bool IsDefaultSet(DiscoveredTranslations set, string defaultLanguage)
    {
        return set.ListsTranslations
               && !ImplementsContract(set)
               && !RewordsSet(set)
               && set.Language is not null
               && String.Equals(set.Language, defaultLanguage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a set implements a contract the input compilation knows: one written by hand in its own assembly,
    /// whatever its accessibility, or a public one of any assembly. Listing <c>ITranslationSet</c> as well then adds
    /// nothing, as the contract derives from it.
    /// </summary>
    public static bool ImplementsContract(DiscoveredTranslations set)
    {
        return set.ContractNames.Length > 0 || set.OwnContractNames.Length > 0;
    }

    /// <summary>
    /// Whether a set derives from a concrete set that implements <c>ITranslationSet</c> in the input compilation.
    /// Listing <c>ITranslationSet</c> again is then how it implements <c>LanguageCode</c> (<c>en-GB</c> on <c>en</c>),
    /// not how it starts a contract.
    /// </summary>
    public static bool RewordsSet(DiscoveredTranslations set)
    {
        return set is { BaseSetName: not null, BaseSetContracts.Length: 0 };
    }

    public static string GetFullyQualifiedName(INamedTypeSymbol type) => type.ToDisplayString(FullyQualifiedFormat);

    /// <summary>
    /// Whether an assembly references this package: only such an assembly can hold its attributes or a generated
    /// registrar, which keeps the framework's assemblies out of every scan of the references.
    /// </summary>
    public static bool ReferencesPackage(IAssemblySymbol assembly)
    {
        return assembly.Modules.First().ReferencedAssemblies.Any(
            static identity => String.Equals(identity.Name, PackageAssemblyName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Cheap syntactic pre-filter: a class or record with a base list could implement the interface.
    /// </summary>
    private static bool IsCandidate(SyntaxNode node, CancellationToken _)
    {
        return node is ClassDeclarationSyntax { BaseList: not null } or RecordDeclarationSyntax { BaseList: not null };
    }

    /// <summary>
    /// Resolves a candidate to what discovery finds in it, or <see langword="null"/> when it holds nothing of interest.
    /// </summary>
    private static DiscoveryResult? Discover(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) is not INamedTypeSymbol { TypeKind: TypeKind.Class } type)
            return null;

        var declaration = (TypeDeclarationSyntax)context.Node;

        // Generated code writes new T(), so the type must be concrete and closed, and nameable from another file: a
        // nested set could be private, and a file-local type reports internal while no other file can name it.
        if (type.IsAbstract || type.IsGenericType || type.ContainingType is not null || type.IsFileLocal)
            return DiscoverUnnameable(type, declaration, context.SemanticModel.Compilation, cancellationToken);

        return DiscoverSet(type, declaration, context.SemanticModel, cancellationToken) is { } set
            ? new DiscoveryResult(set, UnnameableOverride: null, UnextendableSet: null)
            : null;
    }

    /// <summary>
    /// What an abstract, generic, nested, or file-local class gives discovery: a set rewording another assembly's texts
    /// where generated code cannot reach it (<c>LOC003</c>), or a partial class listing <c>ITranslationSet</c> as a
    /// default-language set does, which its shape keeps the generator from extending (<c>LOC002</c> in the default
    /// language); <see langword="null"/> for anything else, such as a test fixture.
    /// </summary>
    // Only overrides and partial classes: a nested set of the assembly's own contract is what a test fixture is,
    // registered by hand, and a language it was meant to give the assembly is reported as missing (LOC004) anyway.
    private static DiscoveryResult? DiscoverUnnameable(INamedTypeSymbol type, TypeDeclarationSyntax declaration,
        Compilation compilation, CancellationToken cancellationToken)
    {
        // An override needs new T() to work, a contract the partial modifier; the base list is bound only when either
        // could apply.
        var mayRegister = type is { IsAbstract: false, IsGenericType: false };
        var isPartial = declaration.Modifiers.Any(SyntaxKind.PartialKeyword);

        if ((!mayRegister && !isPartial) || !ImplementsTranslations(type) || IsExcluded(type))
            return null;

        UnregistrableTranslations? unnameableOverride = null;
        if (mayRegister && IsOverride(type))
        {
            var problem = type.ContainingType is { } container
                ? $"it is nested in '{container.Name}', where generated code cannot reach it"
                : "it is file-local, so generated code in another file cannot name it";

            unnameableOverride = new UnregistrableTranslations(type.ToDisplayString(), problem, DiagnosticLocation.From(type.Locations[0]));
        }

        UnextendableSet? unextendableSet = null;
        if (isPartial && StartsContract(type))
        {
            unextendableSet = new UnextendableSet(
                MetadataName: type.ToDisplayString(),
                Name: type.Name,
                Problem: DescribeShape(type),
                Language: GetLanguage(type, GetSourceAncestors(type, compilation, cancellationToken), compilation, cancellationToken),
                Location: DiagnosticLocation.From(type.Locations[0]));
        }

        return unnameableOverride is null && unextendableSet is null
            ? null
            : new DiscoveryResult(Set: null, unnameableOverride, unextendableSet);
    }

    /// <summary>
    /// Whether a class reads as the start of a contract, as a default-language set does: it lists
    /// <c>ITranslationSet</c> itself, implements no contract, and derives from no concrete set (see
    /// <see cref="IsDefaultSet"/>).
    /// </summary>
    private static bool StartsContract(INamedTypeSymbol type)
    {
        if (!Enumerable.Any(type.Interfaces, IsTranslationsInterface)
            || Enumerable.Any(type.AllInterfaces, static @interface => !IsTranslationsInterface(@interface) && ImplementsTranslations(@interface)))
        {
            return false;
        }

        return type.BaseType is not { IsAbstract: false, TypeKind: TypeKind.Class } baseType || !ImplementsTranslations(baseType);
    }

    /// <summary>
    /// What keeps the generator from extending a class, as the clause of the <c>LOC002</c> message.
    /// </summary>
    private static string DescribeShape(INamedTypeSymbol type)
    {
        return type switch
        {
            { ContainingType: { } container } => $"it is nested in '{container.Name}'",
            { IsFileLocal: true } => "it is file-local",
            { IsGenericType: true } => "it is generic",
            _ => "it is abstract",
        };
    }

    /// <summary>
    /// Resolves a concrete, closed, top-level class to a discovered set, or <see langword="null"/> when it is no set
    /// that could register itself. An excluded set is resolved too, and flagged.
    /// </summary>
    private static DiscoveredTranslations? DiscoverSet(INamedTypeSymbol type, TypeDeclarationSyntax declaration,
        SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var compilation = semanticModel.Compilation;
        var implementsTranslations = ImplementsTranslations(type);

        // Most classes with a base list are no set. Their base types are bound by now, so a class that implements no
        // ITranslationSet, and whose own and ancestors' base types all bind, is ruled out before any declaration of an
        // ancestor gets a semantic model of its own.
        if (!implementsTranslations && !HasUnboundBaseType(type))
            return null;

        var ancestors = GetSourceAncestors(type, compilation, cancellationToken);

        // A class implements a contract generated here through its own base list, or through that of a class it derives
        // from: an abstract base shared by the languages, or the set a regional set rewords.
        var inheritedBaseTypes = ancestors.SelectMany(static ancestor => ancestor.UnresolvedBaseTypes).ToImmutableArray();
        var unresolvedBaseTypes = GetUnresolvedBaseTypes(declaration, semanticModel, cancellationToken)
            .Concat(inheritedBaseTypes)
            .Distinct()
            .ToImmutableArray();

        if (!implementsTranslations && unresolvedBaseTypes.Length is 0)
            return null;

        var listsTranslations = Enumerable.Any(type.Interfaces, IsTranslationsInterface);
        var baseSet = type.BaseType is { IsAbstract: false, TypeKind: TypeKind.Class } baseType && (ImplementsTranslations(baseType) || inheritedBaseTypes.Length > 0)
            ? baseType
            : null;
        var unmappedLanguageCode = MapLanguageCode(type).Unmapped;

        return new DiscoveredTranslations(
            MetadataName: type.ToDisplayString(),
            FullyQualifiedName: GetFullyQualifiedName(type),
            Name: type.Name,
            Namespace: GetNamespaceName(type.ContainingNamespace),
            ImplementsTranslations: implementsTranslations,
            IsOverride: IsOverride(type),
            Depth: GetDepth(type, ancestors),
            UnresolvedBaseTypes: new EquatableArray<UnresolvedBaseType>(unresolvedBaseTypes),
            RegistrationProblem: GetRegistrationProblem(type, ancestors),
            IsExcluded: IsExcluded(type),
            Location: DiagnosticLocation.From(type.Locations[0]),
            Language: GetLanguage(type, ancestors, compilation, cancellationToken),
            RecordedLanguageCode: DeclaresLanguageCode(type) ? null : FindOwnRecordedLanguageCode(type),
            ListsTranslations: listsTranslations,
            ContractNames: new EquatableArray<string>(GetPublicContractNames(type)),
            OwnContractNames: new EquatableArray<string>(GetOwnContractNames(type)),
            AncestorNames: new EquatableArray<string>([.. ancestors.Select(static ancestor => ancestor.Type.ToDisplayString())]),
            BaseSetName: baseSet?.Name,
            BaseSetLanguage: baseSet is null ? null : GetLanguage(baseSet, [.. ancestors.Skip(1)], compilation, cancellationToken),
            BaseSetContracts: baseSet is null || ImplementsTranslations(baseSet)
                ? default
                : new EquatableArray<UnresolvedBaseType>(inheritedBaseTypes),
            UnmappedLanguageCodeType: unmappedLanguageCode?.ContainingType.Name,
            UnmappedLanguageCodeBaseType: unmappedLanguageCode?.ContainingType.BaseType?.Name,
            IsPartial: declaration.Modifiers.Any(SyntaxKind.PartialKeyword),
            IsRecord: type.IsRecord,
            IsSealed: type.IsSealed,
            IsPublic: type.DeclaredAccessibility is Accessibility.Public,
            StringProperties: new EquatableArray<string>(GetStringProperties(type)),
            InheritedTexts: new EquatableArray<InheritedText>(GetInheritedTexts(type)),
            Texts: new EquatableArray<TranslationsMember>(listsTranslations ? GetTextMembers(type, compilation, cancellationToken) : ImmutableArray<TranslationsMember>.Empty));
    }

    private static bool ImplementsTranslations(ITypeSymbol type)
    {
        return Enumerable.Any(type.AllInterfaces, IsTranslationsInterface);
    }

    /// <summary>
    /// Whether the type, or a class of this assembly it derives from, lists a base type that binds to nothing, as one
    /// implementing a contract generated here does.
    /// </summary>
    private static bool HasUnboundBaseType(INamedTypeSymbol type)
    {
        for (var current = type; current is { TypeKind: TypeKind.Class } && IsDeclaredIn(current, type.ContainingAssembly); current = current.BaseType)
            if (current.BaseType is { TypeKind: TypeKind.Error } || Enumerable.Any(current.Interfaces, static @interface => @interface.TypeKind is TypeKind.Error))
                return true;

        return false;
    }

    private static bool IsTranslationsInterface(INamedTypeSymbol @interface)
    {
        return String.Equals(@interface.ToDisplayString(), TranslationSetInterfaceFullName, StringComparison.Ordinal);
    }

    private static bool IsExcluded(INamedTypeSymbol type)
    {
        return Enumerable.Any(type.GetAttributes(), attribute
            => String.Equals(attribute.AttributeClass?.ToDisplayString(), ExclusionAttributeFullName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether the set rewords something of another assembly: it derives from a concrete set declared there, or
    /// implements a contract declared there. Such a set must win whichever assembly loads first. A set deriving from a
    /// set of its own assembly is no override (a library's regional set must not outrank an app's), and registers after
    /// its base instead (<see cref="GetDepth"/>).
    /// </summary>
    private static bool IsOverride(INamedTypeSymbol type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            if (!baseType.IsAbstract && ImplementsTranslations(baseType) && !IsDeclaredIn(baseType, type.ContainingAssembly))
                return true;

        return type.AllInterfaces.Any(@interface
            => !IsTranslationsInterface(@interface)
               && ImplementsTranslations(@interface)
               && !IsDeclaredIn(@interface, type.ContainingAssembly));
    }

    /// <summary>
    /// How many sets the type derives from. The generated registration adds a set after the sets it derives from, so a
    /// set rewording another of its own assembly wins within their tier.
    /// </summary>
    /// <param name="type">The set.</param>
    /// <param name="ancestors">The classes of this assembly it derives from, nearest first, as the start of its chain.</param>
    private static int GetDepth(INamedTypeSymbol type, ImmutableArray<SourceAncestor> ancestors)
    {
        var depth = 0;
        var index = 0;
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType, index++)
        {
            // A class of this assembly implementing a contract generated here binds that contract to nothing yet, so
            // what it lists, or what a class it derives from lists, has to stand in for the interface.
            var implementsGeneratedContract = index < ancestors.Length
                                              && ancestors.Skip(index).Any(static ancestor => ancestor.UnresolvedBaseTypes.Length > 0);

            if (implementsGeneratedContract || ImplementsTranslations(baseType))
                depth++;
        }

        return depth;
    }

    /// <summary>
    /// The classes of this assembly the type derives from, nearest first, each with the base types it lists itself that
    /// bind to nothing. Stops at the first class from elsewhere, whose base types all bind.
    /// </summary>
    private static ImmutableArray<SourceAncestor> GetSourceAncestors(INamedTypeSymbol type, Compilation compilation,
        CancellationToken cancellationToken)
    {
        var ancestors = ImmutableArray.CreateBuilder<SourceAncestor>();

        for (var baseType = type.BaseType; baseType is { TypeKind: TypeKind.Class } && IsDeclaredIn(baseType, type.ContainingAssembly); baseType = baseType.BaseType)
        {
            var unresolved = ImmutableArray.CreateBuilder<UnresolvedBaseType>();

            foreach (var reference in baseType.DeclaringSyntaxReferences)
                if (reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax declaration)
                    unresolved.AddRange(GetUnresolvedBaseTypes(declaration, compilation.GetSemanticModel(declaration.SyntaxTree), cancellationToken));

            ancestors.Add(new SourceAncestor(baseType, unresolved.ToImmutable()));
        }

        return ancestors.ToImmutable();
    }

    private static bool IsDeclaredIn(ISymbol symbol, IAssemblySymbol assembly)
    {
        return SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, assembly);
    }

    /// <summary>
    /// The contracts another assembly could implement too: the public interfaces deriving from <c>ITranslationSet</c> that
    /// the type resolves in the input compilation.
    /// </summary>
    private static ImmutableArray<string> GetPublicContractNames(INamedTypeSymbol type)
    {
        return type.AllInterfaces
            .Where(static @interface => !IsTranslationsInterface(@interface)
                                        && ImplementsTranslations(@interface)
                                        && @interface.DeclaredAccessibility is Accessibility.Public)
            .Select(static @interface => @interface.ToDisplayString())
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>
    /// The contracts written by hand in the type's own assembly: the interfaces deriving from <c>ITranslationSet</c>
    /// declared there, whatever their accessibility. A contract the generator writes is not among them, as it does not
    /// exist in the input compilation.
    /// </summary>
    private static ImmutableArray<string> GetOwnContractNames(INamedTypeSymbol type)
    {
        return type.AllInterfaces
            .Where(@interface => !IsTranslationsInterface(@interface)
                                 && ImplementsTranslations(@interface)
                                 && IsDeclaredIn(@interface, type.ContainingAssembly))
            .Select(static @interface => @interface.ToDisplayString())
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>
    /// The language the set serves, as <c>ITranslationSet.LanguageCode</c> will at run time: its own constant when it
    /// implements the property, else the code the build records for it or a set it derives from
    /// (<see cref="FindRecordedLanguageCode"/>), else the last two letters of its name. <see langword="null"/> when that
    /// cannot be known here: the property is no constant, or comes from another assembly. A <c>LanguageCode</c> that
    /// implements nothing counts as meant (<c>LOC015</c> reports it), so the language checks add no errors of their own
    /// about it.
    /// </summary>
    /// <param name="type">The set.</param>
    /// <param name="ancestors">The classes of this assembly it derives from, nearest first.</param>
    /// <param name="compilation">The compilation, to read a constant.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    private static string? GetLanguage(INamedTypeSymbol type, ImmutableArray<SourceAncestor> ancestors, Compilation compilation, CancellationToken cancellationToken)
    {
        var mapping = MapLanguageCode(type);

        if ((mapping.Unmapped ?? mapping.Implementation ?? FindInterfaceLanguageCode(type)) is { } property)
            return ReadConstant(property, compilation, cancellationToken);

        if (FindRecordedLanguageCode(type, ancestors) is { } recorded)
            return recorded;

        return EndsInTwoAsciiLetters(type.Name)
            ? type.Name.Substring(type.Name.Length - 2).ToLowerInvariant()
            : null;

        static string? ReadConstant(IPropertySymbol property, Compilation compilation, CancellationToken cancellationToken)
        {
            return property.DeclaringSyntaxReferences.Length > 0
                   && property.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is PropertyDeclarationSyntax declaration
                ? GetConstantText(declaration, compilation, cancellationToken)
                : null;
        }
    }

    /// <summary>
    /// The code the build records for a set at run time (<c>GeneratedCode.RegisterLanguageCode</c>), or else for
    /// the nearest set it derives from that has one, as <c>ITranslationSet.GetLanguageCode</c> looks it up; <see langword="null"/>
    /// when none has. Only a set the generated registrar can name gets a record: one that discovery finds.
    /// </summary>
    /// <param name="type">The set.</param>
    /// <param name="ancestors">The classes of this assembly it derives from, nearest first.</param>
    private static string? FindRecordedLanguageCode(INamedTypeSymbol type, ImmutableArray<SourceAncestor> ancestors)
    {
        if (!DeclaresLanguageCode(type) && FindOwnRecordedLanguageCode(type) is { } own)
            return own;

        var index = 0;
        for (var baseType = type.BaseType; baseType is { TypeKind: TypeKind.Class }; baseType = baseType.BaseType, index++)
        {
            // A class of this assembly implementing a contract generated here is a set as well (see GetDepth).
            var isSet = ImplementsTranslations(baseType)
                        || (index < ancestors.Length && ancestors.Skip(index).Any(static ancestor => ancestor.UnresolvedBaseTypes.Length > 0));

            if (isSet && IsNameableSet(baseType) && !DeclaresLanguageCode(baseType) && FindOwnRecordedLanguageCode(baseType) is { } inherited)
                return inherited;
        }

        return null;
    }

    /// <summary>
    /// The code a set's own name gives it among the languages its assembly declares that are not two letters
    /// (<c>ShopTranslationsEnGB</c> for <c>en-GB</c>), or <see langword="null"/>: what the generated registrar of that
    /// assembly records for it.
    /// </summary>
    private static string? FindOwnRecordedLanguageCode(INamedTypeSymbol type)
    {
        return SupportedLanguages.FindNamedLanguage(type.Name, SupportedLanguages.ReadCodes(type.ContainingAssembly));
    }

    /// <summary>
    /// Whether discovery finds a set of this shape, so that generated code can name it: concrete, closed, top-level, and
    /// not file-local.
    /// </summary>
    private static bool IsNameableSet(INamedTypeSymbol type)
    {
        return type is { IsAbstract: false, TypeKind: TypeKind.Class, IsGenericType: false, ContainingType: null, IsFileLocal: false };
    }

    /// <summary>
    /// Why generated code cannot register the set, as the clause of the <c>LOC003</c> message, or
    /// <see langword="null"/> when it can.
    /// </summary>
    private static string? GetRegistrationProblem(INamedTypeSymbol type, ImmutableArray<SourceAncestor> ancestors)
    {
        // new T() also calls a constructor whose parameters all have defaults or take params, when it is the only such
        // constructor; two of them, without a parameterless one, make the call ambiguous.
        var callable = type.InstanceConstructors
            .Where(static constructor => constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal
                                         && constructor.Parameters.All(static parameter => parameter.IsOptional || parameter.IsParams))
            .ToArray();

        if (callable.Length is not 1 && !callable.Any(static constructor => constructor.Parameters.Length is 0))
            return "it has no parameterless constructor";

        // At run time this would throw while the assembly loads, where the cause is hard to see.
        if (!EndsInTwoAsciiLetters(type.Name) && !DeclaresLanguageCode(type) && FindRecordedLanguageCode(type, ancestors) is null)
            return "its name does not end in a language code (two letters, or a longer code the assembly declares, as in ShopTranslationsEnGB), and it does not implement LanguageCode";

        return null;
    }

    private static bool EndsInTwoAsciiLetters(string name)
    {
        return name.Length >= 2 && Ascii.IsLetter(name[name.Length - 1]) && Ascii.IsLetter(name[name.Length - 2]);
    }

    private static bool DeclaresLanguageCode(INamedTypeSymbol type)
    {
        return MapLanguageCode(type).Implementation is not null || FindInterfaceLanguageCode(type) is not null;
    }

    /// <summary>
    /// Which <c>LanguageCode</c> of the set's classes implements <c>ITranslationSet.LanguageCode</c> at run time
    /// (<see langword="null"/>: the default, from the type's name), and the most derived one that implements nothing.
    /// </summary>
    // A class maps the interface where it lists it (ITranslationSet, a contract, or a contract generated here, which binds
    // to nothing yet), looking up from there. A class deriving from it without listing the interface again inherits that
    // mapping, so a LanguageCode it declares implements nothing unless it overrides the implementation; an explicit one
    // does not compile there (CS0540).
    private static LanguageCodeMapping MapLanguageCode(INamedTypeSymbol type)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var current = type; current is not null; current = current.BaseType)
            chain.Add(current);

        var listing = chain.FindIndex(ListsContract);
        if (listing < 0)
            return default;

        IPropertySymbol? implementation = null;
        for (var i = listing; i < chain.Count && implementation is null; i++)
            implementation = FindDeclaredLanguageCode(chain[i]);

        IPropertySymbol? @override = null;
        IPropertySymbol? unmapped = null;
        for (var i = 0; i < listing; i++)
        {
            if (FindDeclaredLanguageCode(chain[i]) is not { } declared)
                continue;

            if (implementation is not null && Overrides(declared, implementation))
                @override ??= declared;
            else
                unmapped ??= declared;
        }

        return new LanguageCodeMapping(@override ?? implementation, unmapped);

        static bool ListsContract(INamedTypeSymbol type)
        {
            return type.BaseType is { TypeKind: TypeKind.Error }
                   || Enumerable.Any(type.Interfaces, static @interface => @interface.TypeKind is TypeKind.Error || IsTranslationsInterface(@interface) || ImplementsTranslations(@interface));
        }

        static bool Overrides(IPropertySymbol declared, IPropertySymbol implementation)
        {
            for (var overridden = declared.OverriddenProperty; overridden is not null; overridden = overridden.OverriddenProperty)
                if (SymbolEqualityComparer.Default.Equals(overridden.OriginalDefinition, implementation.OriginalDefinition))
                    return true;

            return false;
        }
    }

    /// <summary>
    /// A contract's own implementation of <c>LanguageCode</c> (<c>string ITranslationSet.LanguageCode =&gt; "nl";</c>
    /// in an interface), which applies when no class of the set implements it.
    /// </summary>
    private static IPropertySymbol? FindInterfaceLanguageCode(INamedTypeSymbol type)
    {
        foreach (var @interface in type.AllInterfaces)
            if (!IsTranslationsInterface(@interface) && FindDeclaredLanguageCode(@interface) is { } property)
                return property;

        return null;
    }

    /// <summary>
    /// The <c>LanguageCode</c> a type declares itself: a public instance property of that name, or an explicit
    /// implementation of the interface's.
    /// </summary>
    private static IPropertySymbol? FindDeclaredLanguageCode(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol { IsStatic: false } property)
                continue;

            if ((property.DeclaredAccessibility is Accessibility.Public && String.Equals(property.Name, LanguageCodeMemberName, StringComparison.Ordinal))
                || Enumerable.Any(property.ExplicitInterfaceImplementations, static implemented => String.Equals(implemented.Name, LanguageCodeMemberName, StringComparison.Ordinal)))
            {
                return property;
            }
        }

        return null;
    }

    /// <summary>
    /// The base-list entries of this declaration that bind to no type, each with the namespaces the compiler looks it up
    /// in: what a base list looks like when it names a contract another generator writes.
    /// </summary>
    private static ImmutableArray<UnresolvedBaseType> GetUnresolvedBaseTypes(TypeDeclarationSyntax declaration,
        SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (declaration.BaseList is null)
            return ImmutableArray<UnresolvedBaseType>.Empty;

        var types = ImmutableArray.CreateBuilder<UnresolvedBaseType>();

        foreach (var baseType in declaration.BaseList.Types)
        {
            if (semanticModel.GetTypeInfo(baseType.Type, cancellationToken).Type is { TypeKind: TypeKind.Error, Name.Length: > 0 } unresolved)
            {
                var namespaces = GetLookupNamespaces(baseType.Type, semanticModel, cancellationToken);
                types.Add(new UnresolvedBaseType(unresolved.Name, new EquatableArray<string>(namespaces)));
            }
        }

        return types.ToImmutable();
    }

    /// <summary>
    /// The namespaces the compiler looks a base type up in, in its order: the qualifier's namespace for a qualified
    /// name (<c>Shop.IShopTranslations</c>, <c>IShopTranslations</c>); for a simple name each enclosing namespace from
    /// the innermost out, each followed by the usings of its declaration, then the global namespace, then what the file
    /// and the global usings import. Empty when the name cannot bind to a namespace's member.
    /// </summary>
    private static ImmutableArray<string> GetLookupNamespaces(TypeSyntax type, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case QualifiedNameSyntax qualified:
                return semanticModel.GetSymbolInfo(qualified.Left, cancellationToken).Symbol is INamespaceSymbol qualifier
                    ? [GetNamespaceName(qualifier)]
                    : ImmutableArray<string>.Empty;

            case AliasQualifiedNameSyntax aliased:
                return aliased.Alias.Identifier.IsKind(SyntaxKind.GlobalKeyword)
                    ? [""]
                    : ImmutableArray<string>.Empty;

            case IdentifierNameSyntax:
                break;

            default:
                return ImmutableArray<string>.Empty;
        }

        var namespaces = new List<string>();

        foreach (var declaration in type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration, cancellationToken) is not INamespaceSymbol declared)
                continue;

            namespaces.Add(GetNamespaceName(declared));

            foreach (var directive in declaration.Usings)
            {
                if (directive.Alias is null
                    && directive.StaticKeyword.IsKind(SyntaxKind.None)
                    && directive.Name is { } name
                    && semanticModel.GetSymbolInfo(name, cancellationToken).Symbol is INamespaceSymbol imported)
                {
                    namespaces.Add(GetNamespaceName(imported));
                }
            }

            // namespace A.B { } is namespace A { namespace B { } }: A is searched next, with no usings of its own.
            var outer = declared;
            for (var parts = CountNameParts(declaration.Name); parts > 1 && outer.ContainingNamespace is { } containing; parts--)
            {
                outer = containing;
                namespaces.Add(GetNamespaceName(outer));
            }
        }

        namespaces.Add("");

        // Every scope's imports: those of the namespace declarations are already listed above, which keeps them first.
        foreach (var scope in semanticModel.GetImportScopes(type.SpanStart, cancellationToken))
            foreach (var import in scope.Imports)
                if (import.NamespaceOrType is INamespaceSymbol imported)
                    namespaces.Add(GetNamespaceName(imported));

        return namespaces.Distinct(StringComparer.Ordinal).ToImmutableArray();

        static int CountNameParts(NameSyntax name) => name is QualifiedNameSyntax qualified ? CountNameParts(qualified.Left) + 1 : 1;
    }

    private static string GetNamespaceName(INamespaceSymbol @namespace)
    {
        return @namespace.IsGlobalNamespace ? "" : @namespace.ToDisplayString();
    }

    /// <summary>
    /// The public string properties a cref can name on this set: those the class declares itself. A cref does not find
    /// a member through a class that only inherits it (<c>ShopTranslationsEnGB.Checkout</c>, declared by
    /// <c>ShopTranslationsEN</c>, is <c>CS1574</c>), so those are named where they are declared
    /// (<see cref="GetInheritedTexts"/>). An explicit interface implementation has no such name.
    /// </summary>
    private static ImmutableArray<string> GetStringProperties(INamedTypeSymbol type)
    {
        var names = ImmutableArray.CreateBuilder<string>();

        foreach (var member in type.GetMembers())
            if (IsPublicStringProperty(member, out var property) && !String.Equals(property.Name, LanguageCodeMemberName, StringComparison.Ordinal))
                names.Add(property.Name);

        return names.Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal).ToImmutableArray();
    }

    /// <summary>
    /// The public string properties this set inherits, each with the nearest class declaring it: where a cref names a
    /// text the set does not declare itself, such as <c>ShopTranslationsEN.Checkout</c> for a British set that leaves
    /// <c>Checkout</c> as it is. A text that the set, or a class between it and the one declaring the text, implements
    /// explicitly is left out: that implementation is its wording, and has no name.
    /// </summary>
    private static ImmutableArray<InheritedText> GetInheritedTexts(INamedTypeSymbol type)
    {
        var texts = ImmutableArray.CreateBuilder<InheritedText>();

        // The names a nearer class has settled: those it declares, and those it implements explicitly.
        var settled = new HashSet<string>(StringComparer.Ordinal) { LanguageCodeMemberName };

        for (var current = type; current is not null; current = current.BaseType)
        {
            var members = current.GetMembers();

            foreach (var member in members)
            {
                if (!IsPublicStringProperty(member, out var property) || !settled.Add(property.Name) || SymbolEqualityComparer.Default.Equals(current, type))
                    continue;

                texts.Add(new InheritedText(
                    Name: property.Name,
                    Namespace: GetNamespaceName(current.ContainingNamespace),
                    TypeName: GetCrefName(current, CrefNameFormat),
                    FullyQualifiedTypeName: GetCrefName(current, CrefFullNameFormat)));
            }

            // After the properties a cref can name, so that a class holding both is linked at the one with a name.
            foreach (var member in members)
                if (member is IPropertySymbol { IsStatic: false } property && GetExplicitlyImplementedName(property) is { } name)
                    settled.Add(name);
        }

        return texts.OrderBy(static text => text.Name, StringComparer.Ordinal).ToImmutableArray();
    }

    /// <summary>
    /// A class as a cref names it, its type parameters in braces: <c>Wording.DutchBase{T}</c>.
    /// </summary>
    private static string GetCrefName(INamedTypeSymbol type, SymbolDisplayFormat format)
    {
        return type.OriginalDefinition.ToDisplayString(format).Replace('<', '{').Replace('>', '}');
    }

    /// <summary>
    /// The name of the member a property implements explicitly (<c>Checkout</c> for
    /// <c>string IShopTranslations.Checkout</c>), or <see langword="null"/> for any other property.
    /// </summary>
    // Read from the property's own name, which holds the interface as it is written: the symbol names no implemented
    // member for a contract generated here, which binds to nothing yet.
    private static string? GetExplicitlyImplementedName(IPropertySymbol property)
    {
        var separator = property.Name.LastIndexOf('.');

        return separator < 0 ? null : property.Name.Substring(separator + 1);
    }

    /// <summary>
    /// The texts: the public, instance, non-indexer <see cref="string"/> properties with a public getter declared by
    /// the class itself, in declaration order (an inherited property belongs to the base's own contract).
    /// <c>LanguageCode</c> (the <c>ITranslationSet</c> member, which a set may override) and explicit interface
    /// implementations are not texts.
    /// </summary>
    private static ImmutableArray<TranslationsMember> GetTextMembers(INamedTypeSymbol type, Compilation compilation,
        CancellationToken cancellationToken)
    {
        var members = ImmutableArray.CreateBuilder<TranslationsMember>();

        foreach (var member in type.GetMembers())
        {
            if (!IsPublicStringProperty(member, out var property) || String.Equals(property.Name, LanguageCodeMemberName, StringComparison.Ordinal))
                continue;

            var declaration = property.DeclaringSyntaxReferences.Length > 0
                ? property.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) as PropertyDeclarationSyntax
                : null;

            members.Add(new TranslationsMember(
                Name: property.Name,
                HasDocComment: declaration is not null && HasDocComment(declaration),
                DefaultText: declaration is not null ? GetConstantText(declaration, compilation, cancellationToken) : null));
        }

        return members.ToImmutable();
    }

    private static bool IsPublicStringProperty(ISymbol member, out IPropertySymbol property)
    {
        property = (member as IPropertySymbol)!;

        return member is IPropertySymbol
        {
            DeclaredAccessibility: Accessibility.Public,
            IsStatic: false,
            IsIndexer: false,
            GetMethod.DeclaredAccessibility: Accessibility.Public,
            ExplicitInterfaceImplementations.Length: 0,
            Type.SpecialType: SpecialType.System_String,
        };
    }

    /// <summary>
    /// Whether the property carries a doc comment. Checked on the trivia rather than through the symbol: a project that
    /// emits no documentation file parses <c>///</c> as a plain comment, and the symbol reports none.
    /// </summary>
    private static bool HasDocComment(PropertyDeclarationSyntax declaration)
    {
        foreach (var trivia in declaration.GetLeadingTrivia())
        {
            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                return true;

            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString().StartsWith("///", StringComparison.Ordinal))
                return true;

            if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) && trivia.ToString().StartsWith("/**", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// The property's wording when it is a constant (<c>=> "Submit"</c>, a getter returning a literal or a
    /// <see langword="const"/>, or an initializer such as <c>{ get; } = "Submit";</c>), else <see langword="null"/>.
    /// </summary>
    // The same shapes as FormatPlaceholdersAnalyzer reads in the sets of the other languages: wording the analyzer
    // reads there and this did not read here would be held to no placeholders at all.
    private static string? GetConstantText(PropertyDeclarationSyntax declaration, Compilation compilation,
        CancellationToken cancellationToken)
    {
        var expression = declaration.ExpressionBody?.Expression ?? declaration.Initializer?.Value;

        if (expression is null && declaration.AccessorList is not null)
        {
            foreach (var accessor in declaration.AccessorList.Accessors)
            {
                if (!accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
                    continue;

                expression = accessor.ExpressionBody?.Expression
                             ?? (accessor.Body is { Statements: [ReturnStatementSyntax { Expression: { } returned }] } ? returned : null);
            }
        }

        // A property of another assembly has its syntax here only where an IDE references that project as a
        // compilation, and then in a tree this compilation has no semantic model for. Left unread, as in a build,
        // which references the project's assembly and sees no syntax at all.
        if (expression is null || !compilation.ContainsSyntaxTree(expression.SyntaxTree))
            return null;

        var constant = compilation.GetSemanticModel(expression.SyntaxTree).GetConstantValue(expression, cancellationToken);

        return constant.HasValue ? constant.Value as string : null;
    }
}

/// <summary>
/// A candidate set: its names; whether it implements <c>ITranslationSet</c> in the input compilation, and the base
/// types that bind to nothing which it or a class it derives from lists; whether it wins over the default tier, and how
/// many sets it derives from; why it cannot register itself, if so; whether it is marked
/// <c>[ExcludeFromTranslationDiscovery]</c>, which leaves it unregistered and unchecked, counting only as a set of its
/// language; the language code the generated registrar records for it, when its name ends in a declared code that is
/// not two letters and it does not implement <c>LanguageCode</c>; and what the language checks and the generated
/// contract need to know about it, among which the public contracts it implements, those written by hand in its
/// assembly, the classes of its assembly it derives from, the set it derives from directly (with the unresolved base
/// types through which that one would implement a contract generated here, empty when it implements
/// <c>ITranslationSet</c> already), and the class whose <c>LanguageCode</c> implements nothing, if any.
/// </summary>
internal readonly record struct DiscoveredTranslations(
    string MetadataName,
    string FullyQualifiedName,
    string Name,
    string Namespace,
    bool ImplementsTranslations,
    bool IsOverride,
    int Depth,
    EquatableArray<UnresolvedBaseType> UnresolvedBaseTypes,
    string? RegistrationProblem,
    bool IsExcluded,
    DiagnosticLocation Location,
    string? Language,
    string? RecordedLanguageCode,
    bool ListsTranslations,
    EquatableArray<string> ContractNames,
    EquatableArray<string> OwnContractNames,
    EquatableArray<string> AncestorNames,
    string? BaseSetName,
    string? BaseSetLanguage,
    EquatableArray<UnresolvedBaseType> BaseSetContracts,
    string? UnmappedLanguageCodeType,
    string? UnmappedLanguageCodeBaseType,
    bool IsPartial,
    bool IsRecord,
    bool IsSealed,
    bool IsPublic,
    EquatableArray<string> StringProperties,
    EquatableArray<InheritedText> InheritedTexts,
    EquatableArray<TranslationsMember> Texts);

/// <summary>
/// A text a set inherits, with the nearest class declaring it as a cref names that class: its namespace (<c>""</c> for
/// the global namespace), its name there, and its full name, the last two with containing types and with type
/// parameters in braces (<c>DutchBase{T}</c>).
/// </summary>
internal readonly record struct InheritedText(
    string Name,
    string Namespace,
    string TypeName,
    string FullyQualifiedTypeName);

/// <summary>
/// A base type a candidate lists that binds to nothing in the input compilation: its simple name, and the namespaces the
/// compiler looks it up in, in order (<c>""</c> for the global namespace).
/// </summary>
internal readonly record struct UnresolvedBaseType(
    string Name,
    EquatableArray<string> Namespaces);

/// <summary>
/// A class of the current assembly a candidate derives from, with the base types it lists itself that bind to nothing.
/// Lives within one transform only, so it may hold a symbol.
/// </summary>
internal readonly record struct SourceAncestor(
    INamedTypeSymbol Type,
    ImmutableArray<UnresolvedBaseType> UnresolvedBaseTypes);

/// <summary>
/// Which <c>LanguageCode</c> property implements <c>ITranslationSet.LanguageCode</c> for a set
/// (<see langword="null"/>: the default, from the type's name), and the most derived one that implements nothing.
/// </summary>
internal readonly record struct LanguageCodeMapping(
    IPropertySymbol? Implementation,
    IPropertySymbol? Unmapped);

/// <summary>
/// One text of a default-language set: its property name, whether the property carries a doc comment (then the
/// generated members inherit it) and, when the property returns a constant, its wording (then a property without a doc
/// comment is documented with that wording).
/// </summary>
internal readonly record struct TranslationsMember(
    string Name,
    bool HasDocComment,
    string? DefaultText);

/// <summary>
/// A set the generated code registers, in which tier, and how many sets it derives from. Holds no location, so that
/// an edit in the file of a set does not rewrite the generated code.
/// </summary>
internal readonly record struct RegisteredTranslations(
    string MetadataName,
    string FullyQualifiedName,
    bool IsOverride,
    int Depth);

/// <summary>
/// A set that cannot register itself: what <c>LOC003</c> reports.
/// </summary>
internal readonly record struct UnregistrableTranslations(
    string MetadataName,
    string Problem,
    DiagnosticLocation Location);

/// <summary>
/// A partial class listing <c>ITranslationSet</c> as a default-language set does, which the generator cannot extend for
/// its shape (nested, file-local, generic, or abstract): what <c>LOC002</c> reports when its language is the default
/// language. Its language is <see langword="null"/> when the build cannot read it.
/// </summary>
internal readonly record struct UnextendableSet(
    string MetadataName,
    string Name,
    string Problem,
    string? Language,
    DiagnosticLocation Location);

/// <summary>
/// What discovery finds in the current compilation: the candidate sets, the nested or file-local sets rewording another
/// assembly's texts (<c>LOC003</c>), and the partial classes the generator cannot extend (<c>LOC002</c>), the last two
/// ordered by name.
/// </summary>
internal readonly record struct Discovery(
    EquatableArray<DiscoveredTranslations> Sets,
    EquatableArray<UnregistrableTranslations> UnnameableOverrides,
    EquatableArray<UnextendableSet> UnextendableSets);

/// <summary>
/// What discovery finds in one class: a candidate set, or what is reported about a class generated code cannot name.
/// </summary>
internal sealed record DiscoveryResult(
    DiscoveredTranslations? Set,
    UnregistrableTranslations? UnnameableOverride,
    UnextendableSet? UnextendableSet);
