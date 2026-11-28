namespace CodeChops.Idioma.Generators;

// Resolves a class to a set: its shape, its base classes, the contracts it implements, and whether generated code can
// create it to register it.
/// <inheritdoc cref="LocalizationDiscovery"/>
internal static partial class LocalizationDiscovery
{
    /// <summary>
    /// What an abstract, generic, nested or file-local class gives discovery: a rewording of another assembly's texts
    /// that generated code can't reach (<c>LOC207</c>), or a class listing <c>ILocalizationSet</c> as a default-language
    /// set does, whose shape the generator can't extend (<c>LOC200</c>). <see langword="null"/> for anything else.
    /// </summary>
    // A nested set of the assembly's own contract is usually a test fixture registered by hand, so it isn't reported.
    // The analysis leaves out a class whose contract is generated from another set (UnextendableSet.UnresolvedBaseTypes).
    private static DiscoveryResult? DiscoverUnnameable(INamedTypeSymbol type, TypeDeclarationSyntax declaration,
        SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var reader = new WordingReader(semanticModel.Compilation, semanticModel);
        var typeName = type.ToDisplayString();

        // An override needs new T() to work. A class meant to start a contract lists an unresolved one, or is partial.
        // The base list is bound only when either could apply.
        var mayRegister = type is { IsAbstract: false, IsGenericType: false };
        var mayStartContract = declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                               || Enumerable.Any(type.Interfaces, static @interface => @interface.TypeKind is TypeKind.Error);

        // A nested set can only be registered by hand, and the registry reads its language from this description.
        var nestedSet = IsNameableNestedSet(type) && ImplementsLocalizationSet(type)
                        && GetLanguage(type, GetSourceAncestors(type, reader, cancellationToken)) is { } nestedLanguage
            ? new SetDescription(typeName, GetFullyQualifiedName(type), nestedLanguage)
            : (SetDescription?)null;

        if ((!mayRegister && !mayStartContract) || !ImplementsLocalizationSet(type) || IsExcluded(type))
            return nestedSet is null ? null : new DiscoveryResult(Set: null, UnnameableOverride: null, UnextendableSet: null, nestedSet);

        UnregistrableLocalization? unnameableOverride = null;
        if (mayRegister && IsOverride(type))
        {
            var problem = type.ContainingType is { } container
                ? $"it's nested in '{container.Name}', where generated code can't reach it"
                : "it's file-local, so generated code in another file can't name it";

            unnameableOverride = new UnregistrableLocalization(typeName, problem, DiagnosticLocation.From(type.Locations[0]));
        }

        UnextendableSet? unextendableSet = null;
        if (mayStartContract && StartsContract(type))
        {
            var ancestors = GetSourceAncestors(type, reader, cancellationToken);
            var baseTypeLocations = ImmutableArray.CreateBuilder<BaseTypeLocation>();

            // Of every part, so each part of a partial class finds the same class, and of its base classes.
            var unresolvedBaseTypes = type.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(cancellationToken))
                .OfType<TypeDeclarationSyntax>()
                .SelectMany(part => reader.FindModel(part.SyntaxTree) is { } model
                    ? GetUnresolvedBaseTypes(type, typeName, part, model, baseTypeLocations, cancellationToken)
                    : ImmutableArray<UnresolvedBaseType>.Empty)
                .Concat(ancestors.SelectMany(static ancestor => ancestor.UnresolvedBaseTypes))
                .Distinct()
                .ToImmutableArray();

            baseTypeLocations.AddRange(ancestors.SelectMany(static ancestor => ancestor.BaseTypeLocations));

            unextendableSet = new UnextendableSet(
                MetadataName: typeName,
                Name: type.Name,
                Problem: DescribeShape(type),
                Language: GetLanguage(type, ancestors),
                UnresolvedBaseTypes: new EquatableArray<UnresolvedBaseType>(unresolvedBaseTypes),
                Location: DiagnosticLocation.From(type.Locations[0]),
                BaseTypeLocations: new EquatableArray<BaseTypeLocation>(baseTypeLocations.ToImmutable()));
        }

        var result = unnameableOverride is null && unextendableSet is null && nestedSet is null
            ? null
            : new DiscoveryResult(Set: null, unnameableOverride, unextendableSet, nestedSet);

        return result;
    }

    /// <summary>
    /// Whether a class reads as the start of a contract (see <see cref="IsDefaultSet"/>): it lists
    /// <c>ILocalizationSet</c> itself, implements no contract, and derives from no concrete set. The analysis checks the
    /// rest once every set is found (<see cref="ListsOnlyOtherGeneratedContracts"/>).
    /// </summary>
    private static bool StartsContract(INamedTypeSymbol type)
    {
        if (!Enumerable.Any(type.Interfaces, IsLocalizationSetInterface) || ImplementsAnyContract(type))
            return false;

        var result = type.BaseType is not { IsAbstract: false, TypeKind: TypeKind.Class } baseType || !ImplementsLocalizationSet(baseType);
        return result;
    }

    /// <summary>
    /// Whether a set lists <c>ILocalizationSet</c>, itself or through an abstract base class, as
    /// <c>ShopLocalizationEn : ShopBase</c> does. A concrete base class is a set the set rewords
    /// (<see cref="RewordsSet"/>), and so is one above an abstract base, so what they list doesn't count.
    /// </summary>
    private static bool ListsLocalizationSet(INamedTypeSymbol type)
    {
        if (Enumerable.Any(type.Interfaces, IsLocalizationSetInterface))
            return true;

        for (var current = type.BaseType; current is { IsAbstract: true, TypeKind: TypeKind.Class }; current = current.BaseType)
        {
            if (!Enumerable.Any(current.Interfaces, IsLocalizationSetInterface))
                continue;

            for (var above = current.BaseType; above is { TypeKind: TypeKind.Class }; above = above.BaseType)
                if (!above.IsAbstract && ImplementsLocalizationSet(above))
                    return false;

            return true;
        }

        return false;
    }

    /// <summary>
    /// What keeps the generator from extending a class, as the clause of the <c>LOC200</c> message.
    /// </summary>
    private static string DescribeShape(INamedTypeSymbol type)
    {
        var shape = type switch
        {
            { ContainingType: { } container } => $"it's nested in '{container.Name}'",
            { IsFileLocal: true } => "it's file-local",
            { IsGenericType: true } => "it's generic",
            _ => "it's abstract",
        };

        return shape;
    }

    /// <summary>
    /// Resolves a concrete, closed, top-level class that implements <c>ILocalizationSet</c> to a set, with what both parts
    /// of the generator use (<see cref="ForPart"/>). An excluded set is resolved too, and flagged.
    /// </summary>
    private static DiscoveredLocalization DiscoverSet(INamedTypeSymbol type, TypeDeclarationSyntax declaration,
        SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var reader = new WordingReader(semanticModel.Compilation, semanticModel);
        var ancestors = GetSourceAncestors(type, reader, cancellationToken);
        var metadataName = type.ToDisplayString();
        var baseTypeLocations = ImmutableArray.CreateBuilder<BaseTypeLocation>();

        // A generated contract can also be listed by a base class, such as a shared abstract base or the set a regional
        // set rewords.
        var inheritedBaseTypes = ancestors.SelectMany(static ancestor => ancestor.UnresolvedBaseTypes).ToImmutableArray();
        var unresolvedBaseTypes = GetUnresolvedBaseTypes(type, metadataName, declaration, semanticModel, baseTypeLocations, cancellationToken)
            .Concat(inheritedBaseTypes)
            .Distinct()
            .ToImmutableArray();

        baseTypeLocations.AddRange(ancestors.SelectMany(static ancestor => ancestor.BaseTypeLocations));

        var listsLocalizationSet = ListsLocalizationSet(type);
        var baseSet = type.BaseType is { IsAbstract: false, TypeKind: TypeKind.Class } baseType
                      && (ImplementsLocalizationSet(baseType) || ImplementsAnyContract(baseType) || inheritedBaseTypes.Length > 0)
            ? baseType
            : null;

        var constructor = FindConstructorCalledWithoutArguments(type, semanticModel, cancellationToken);
        var language = GetLanguage(type, ancestors);
        var location = DiagnosticLocation.From(type.Locations[0]);
        var depth = GetDepth(type, ancestors);
        var enums = listsLocalizationSet ? GetLocalizedEnums(type) : ImmutableArray<LocalizedEnum>.Empty;

        var set = new DiscoveredLocalization(
            MetadataName: metadataName,
            FullyQualifiedName: GetFullyQualifiedName(type),
            Name: type.Name,
            Namespace: GetNamespaceName(type.ContainingNamespace),
            IsOverride: IsOverride(type),
            Depth: depth,
            UnresolvedBaseTypes: new EquatableArray<UnresolvedBaseType>(unresolvedBaseTypes),
            RegistrationProblem: GetRegistrationProblem(type, constructor, hasLanguage: language is not null),
            HasPublicParameterlessConstructor: Enumerable.Any(type.InstanceConstructors, static candidate => candidate is { Parameters.Length: 0, DeclaredAccessibility: Accessibility.Public }),
            IsExcluded: IsExcluded(type),
            Locations: new SetLocations(
                Set: location,
                BaseTypes: new EquatableArray<BaseTypeLocation>(baseTypeLocations.ToImmutable())),
            Language: language,
            ListsLocalizationSet: listsLocalizationSet,
            ContractNames: new EquatableArray<string>(GetPublicContractNames(type)),
            OwnContractNames: new EquatableArray<string>(GetOwnContractNames(type)),
            ImplementsInternalContract: ImplementsInternalContract(type),
            AncestorNames: new EquatableArray<string>([.. ancestors.Select(static ancestor => ancestor.Name)]),
            BaseSet: baseSet is null
                ? null
                : new BaseSet(
                    Name: baseSet.Name,
                    Language: GetLanguage(baseSet, [.. ancestors.Skip(1)]),
                    Contracts: ImplementsLocalizationSet(baseSet) ? default : new EquatableArray<UnresolvedBaseType>(inheritedBaseTypes)),
            BaseSetNames: new EquatableArray<string>(GetBaseSetNames(type)),
            Reworded: FindRewordedSet(type) is { } reworded
                ? new RewordedSet(reworded.ToDisplayString(), reworded.ContainingAssembly.Identity.Name, GetLanguage(reworded, []))
                : null,
            IsRecord: type.IsRecord,
            IsSealed: type.IsSealed,
            IsPublic: type.DeclaredAccessibility is Accessibility.Public,
            EnumNames: new EquatableArray<string>([.. enums.Select(static @enum => @enum.FullyQualifiedName)]),
            OfContract: new SetTextsOfContract(
                DeclaredTexts: new EquatableArray<string>(GetDeclaredTexts(type)),
                InheritedTexts: new EquatableArray<InheritedText>(GetInheritedTexts(type)),
                Wordings: new EquatableArray<TextWording>(GetWordings(type, reader, cancellationToken)),
                Texts: default,
                UnsupportedTexts: default,
                Usings: default,
                Enums: new EquatableArray<LocalizedEnum>(enums),
                TypeNamedLikeTexts: null,
                TypeNamedLikeContract: language is not null ? FindTypeNamedLikeContract(type, language) : null,
                ReservedTexts: default,
                UndeclaredTexts: default,
                TextsOutsideEnums: default));

        // Only a set a contract may come from gets its texts read (ReadsAsDefaultSet), because they take longest to read
        // and discovery runs on every edit.
        if (!listsLocalizationSet || !ReadsAsDefaultSet(set, SupportedLanguages.ReadDefaultLanguage(type.ContainingAssembly)))
            return set;

        var texts = GetTextMembers(type, enums, reader, cancellationToken, out var unsupportedTexts, out var usings);

        var withTexts = set with
        {
            OfContract = set.OfContract with
            {
                Texts = new EquatableArray<LocalizationMember>(texts),
                UnsupportedTexts = new EquatableArray<UnsupportedText>(unsupportedTexts),
                Usings = usings,
                TypeNamedLikeTexts = language is not null ? FindTypeNamedLikeTexts(type, language, cancellationToken) : null,
                ReservedTexts = new EquatableArray<ReservedText>(language is null ? ImmutableArray<ReservedText>.Empty : GetReservedTexts(type, language, texts, hasEnums: enums.Length > 0)),
                UndeclaredTexts = new EquatableArray<UndeclaredText>(GetUndeclaredTexts(type)),
                TextsOutsideEnums = new EquatableArray<TextOutsideEnums>(language is null ? ImmutableArray<TextOutsideEnums>.Empty : GetTextsOutsideEnums(type, language, texts)),
            },
        };

        return withTexts;
    }

    /// <summary>
    /// The type or namespace that already has the name of a default-language set's generated class, as a message names
    /// it (<c>the enum 'OrderStatus'</c>), or <see langword="null"/> when the name is free. Such a clash is <c>CS0101</c>.
    /// </summary>
    // A static partial class of that name is a part of the generated class itself.
    private static string? FindTypeNamedLikeTexts(INamedTypeSymbol type, string language, CancellationToken cancellationToken)
    {
        if (DeriveBaseName(type.Name, language) is not { } textsName)
            return null;

        foreach (var other in type.ContainingNamespace.GetTypeMembers(textsName, arity: 0))
        {
            var isPartOfGeneratedClass = other is { TypeKind: TypeKind.Class, IsStatic: true }
                && other.DeclaringSyntaxReferences.All(reference => reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax part && part.Modifiers.Any(SyntaxKind.PartialKeyword));

            if (isPartOfGeneratedClass)
                continue;

            var kind = other switch
            {
                { TypeKind: TypeKind.Enum } => "enum",
                { TypeKind: TypeKind.Interface } => "interface",
                { TypeKind: TypeKind.Delegate } => "delegate",
                { IsRecord: true } => "record",
                { TypeKind: TypeKind.Struct } => "struct",
                _ => "class",
            };

            return $"the {kind} '{other.Name}'";
        }

        // A namespace of this compilation, as the folder Texts next to TextsEn gives MyApp.Texts.
        foreach (var other in type.ContainingNamespace.GetNamespaceMembers())
        {
            if (String.Equals(other.Name, textsName, StringComparison.Ordinal) && other.Locations.Any(static location => location.IsInSource))
                return $"the namespace '{GetNamespaceName(other)}'";
        }

        // The set's enum in another namespace, named by its simple name. The generated class would shadow it, so the
        // set's ILocalizationSet<OrderStatus> would name that class (CS0718).
        foreach (var @interface in type.AllInterfaces)
        {
            if (PackageNames.IsLocalizationSetOfEnum(@interface)
                && @interface.TypeArguments[0] is INamedTypeSymbol { TypeKind: TypeKind.Enum, ContainingType: null } @enum
                && String.Equals(@enum.Name, textsName, StringComparison.Ordinal)
                && ListsSetOfEnumBySimpleName(type, textsName, cancellationToken))
            {
                return $"the enum '{@enum.ToDisplayString()}' it holds the texts of";
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a set lists <c>ILocalizationSet&lt;TEnum&gt;</c> with the enum written by its simple name, as far as the
    /// syntax tells.
    /// </summary>
    private static bool ListsSetOfEnumBySimpleName(INamedTypeSymbol type, string enumName, CancellationToken cancellationToken)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax { BaseList.Types: var baseTypes })
                continue;

            foreach (var baseType in baseTypes)
            {
                var listed = baseType.Type switch
                {
                    GenericNameSyntax name => name,
                    QualifiedNameSyntax { Right: GenericNameSyntax name } => name,
                    AliasQualifiedNameSyntax { Name: GenericNameSyntax name } => name,
                    _ => null,
                };

                if (listed is { TypeArgumentList.Arguments: [IdentifierNameSyntax argument] }
                    && String.Equals(listed.Identifier.ValueText, PackageNames.LocalizationSetName, StringComparison.Ordinal)
                    && String.Equals(argument.Identifier.ValueText, enumName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The type that already has the name of a default-language set's contract, as a message names it, or
    /// <see langword="null"/> when the name is free (<c>CS0101</c>). A contract written by hand doesn't count, because a
    /// set that implements it starts no contract.
    /// </summary>
    private static string? FindTypeNamedLikeContract(INamedTypeSymbol type, string language)
    {
        if (DeriveBaseName(type.Name, language) is not { } textsName)
            return null;

        foreach (var other in type.ContainingNamespace.GetTypeMembers($"I{textsName}", arity: 0))
        {
            if (PackageNames.IsContract(other))
                continue;

            var kind = other switch
            {
                { TypeKind: TypeKind.Enum } => "enum",
                { TypeKind: TypeKind.Interface } => "interface",
                { TypeKind: TypeKind.Delegate } => "delegate",
                { IsRecord: true } => "record",
                { TypeKind: TypeKind.Struct } => "struct",
                _ => "class",
            };

            return $"the {kind} '{other.Name}'";
        }

        return null;
    }

    /// <summary>
    /// The texts of a set with a name its generated class can't give them (<c>LOC300</c>). Each is reported at its
    /// member, or at the set when that member is outside this compilation.
    /// </summary>
    private static ImmutableArray<ReservedText> GetReservedTexts(INamedTypeSymbol type, string language, ImmutableArray<LocalizationMember> texts,
        bool hasEnums)
    {
        if (DeriveBaseName(type.Name, language) is not { } textsName)
            return ImmutableArray<ReservedText>.Empty;

        var reserved = ImmutableArray.CreateBuilder<ReservedText>();

        foreach (var text in texts)
        {
            if (!LocalizationSetsAnalysis.IsReservedTextName(text.Name, textsName, hasLookup: hasEnums))
                continue;

            var declared = type.GetMembers(text.Name).FirstOrDefault(static member => member is IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary })
                           ?? type.AllInterfaces
                               .Where(PackageNames.IsLocalizationSetOfEnum)
                               .SelectMany(setOfEnum => setOfEnum.TypeArguments[0].GetMembers(text.Name))
                               .FirstOrDefault(static member => member is IFieldSymbol);

            var location = declared?.Locations.FirstOrDefault(static location => location.IsInSource) ?? type.Locations[0];
            reserved.Add(new ReservedText(text.Name, DiagnosticLocation.From(location)));
        }

        return reserved.ToImmutable();
    }

    /// <summary>
    /// The texts a set of an enum declares that aren't a member of its enums (<c>LOC305</c>). A reserved name is left
    /// to <c>LOC300</c>.
    /// </summary>
    private static ImmutableArray<TextOutsideEnums> GetTextsOutsideEnums(INamedTypeSymbol type, string language, ImmutableArray<LocalizationMember> texts)
    {
        var enums = TextOutsideContractAnalyzer.GetEnums(type);

        if (enums.Length is 0 || DeriveBaseName(type.Name, language) is not { } textsName)
            return ImmutableArray<TextOutsideEnums>.Empty;

        var outside = ImmutableArray.CreateBuilder<TextOutsideEnums>();

        foreach (var text in texts)
        {
            if (TextOutsideContractAnalyzer.IsMemberOfAny(enums, text.Name)
                || LocalizationSetsAnalysis.IsReservedTextName(text.Name, textsName, hasLookup: true)
                || type.GetMembers(text.Name).FirstOrDefault(static member => member is IPropertySymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary }) is not { } declared
                || declared.Locations.FirstOrDefault(static location => location.IsInSource) is not { } location)
            {
                continue;
            }

            outside.Add(new TextOutsideEnums(
                text.Name,
                DiagnosticLocation.From(location),
                TextOutsideContractAnalyzer.JoinNames(enums),
                TextOutsideContractAnalyzer.AdviseOnEnums(enums, text.Name, type.ContainingAssembly)));
        }

        return outside.ToImmutable();
    }

    private static bool ImplementsLocalizationSet(ITypeSymbol type)
    {
        var implements = Enumerable.Any(type.AllInterfaces, IsLocalizationSetInterface);
        return implements;
    }

    /// <summary>
    /// Whether a type implements a contract that binds in the input compilation (<see cref="PackageNames.IsContract"/>).
    /// </summary>
    private static bool ImplementsAnyContract(ITypeSymbol type)
    {
        var implements = Enumerable.Any(type.AllInterfaces, PackageNames.IsContract);
        return implements;
    }

    /// <summary>
    /// Whether the type, or a base class of this assembly, lists a base type that binds to nothing.
    /// </summary>
    private static bool HasUnboundBaseType(INamedTypeSymbol type)
    {
        for (var current = type; current is { TypeKind: TypeKind.Class } && IsDeclaredIn(current, type.ContainingAssembly); current = current.BaseType)
            if (current.BaseType is { TypeKind: TypeKind.Error } || Enumerable.Any(current.Interfaces, static @interface => @interface.TypeKind is TypeKind.Error))
                return true;

        return false;
    }

    private static bool IsLocalizationSetInterface(INamedTypeSymbol @interface)
    {
        var isSet = PackageNames.IsLocalizationSet(@interface) || PackageNames.IsLocalizationSetOfEnum(@interface);
        return isSet;
    }

    /// <summary>
    /// Whether a set, or a member of one, is marked <c>[ExcludeFromLocalizationDiscovery]</c>.
    /// </summary>
    public static bool IsExcluded(ISymbol symbol)
    {
        var isExcluded = Enumerable.Any(symbol.GetAttributes(), attribute
            => String.Equals(attribute.AttributeClass?.ToDisplayString(), PackageNames.ExcludeFromLocalizationDiscoveryAttribute, StringComparison.Ordinal));

        return isExcluded;
    }

    /// <summary>
    /// Whether the set rewords another assembly's set or contract, so it must win whichever assembly loads first. A
    /// rewording of a set of its own assembly registers after its base instead (<see cref="GetDepth"/>), so a library's
    /// regional set doesn't outrank an app's.
    /// </summary>
    private static bool IsOverride(INamedTypeSymbol type)
    {
        if (FindRewordedSet(type) is not null)
            return true;

        var isOverride = type.AllInterfaces.Any(@interface
            => PackageNames.IsContract(@interface)
               && !IsDeclaredIn(@interface, type.ContainingAssembly));

        return isOverride;
    }

    /// <summary>
    /// The nearest concrete set of another assembly that a set derives from, which it rewords, or <see langword="null"/>.
    /// </summary>
    private static INamedTypeSymbol? FindRewordedSet(INamedTypeSymbol type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            if (!baseType.IsAbstract && ImplementsLocalizationSet(baseType) && !IsDeclaredIn(baseType, type.ContainingAssembly))
                return baseType;

        return null;
    }

    /// <summary>
    /// The full names of the concrete sets a set derives from, nearest first, of any assembly.
    /// </summary>
    private static ImmutableArray<string> GetBaseSetNames(INamedTypeSymbol type)
    {
        var names = ImmutableArray.CreateBuilder<string>();

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            if (!baseType.IsAbstract && ImplementsLocalizationSet(baseType))
                names.Add(baseType.ToDisplayString());

        return names.ToImmutable();
    }

    /// <summary>
    /// How many concrete sets the type derives from. A set is registered after its base sets, so a rewording wins within
    /// their tier.
    /// </summary>
    /// <param name="ancestors">The classes of this assembly it derives from, nearest first.</param>
    private static int GetDepth(INamedTypeSymbol type, ImmutableArray<SourceAncestor> ancestors)
    {
        var depth = 0;
        var index = 0;
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType, index++)
        {
            if (baseType.IsAbstract)
                continue;

            // A generated contract doesn't bind yet, so an unresolved base type stands in for the interface.
            var implementsGeneratedContract = index < ancestors.Length
                                              && ancestors.Skip(index).Any(static ancestor => ancestor.UnresolvedBaseTypes.Length > 0);

            if (implementsGeneratedContract || ImplementsLocalizationSet(baseType))
                depth++;
        }

        return depth;
    }

    /// <summary>
    /// The classes of this assembly the type derives from, nearest first, each with its unresolved base types. Stops at
    /// the first class from elsewhere.
    /// </summary>
    private static ImmutableArray<SourceAncestor> GetSourceAncestors(INamedTypeSymbol type, WordingReader reader,
        CancellationToken cancellationToken)
    {
        var ancestors = ImmutableArray.CreateBuilder<SourceAncestor>();

        for (var baseType = type.BaseType; baseType is { TypeKind: TypeKind.Class } && IsDeclaredIn(baseType, type.ContainingAssembly); baseType = baseType.BaseType)
        {
            var name = baseType.ToDisplayString();
            var unresolved = ImmutableArray.CreateBuilder<UnresolvedBaseType>();
            var locations = ImmutableArray.CreateBuilder<BaseTypeLocation>();

            foreach (var reference in baseType.DeclaringSyntaxReferences)
                if (reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax declaration && reader.FindModel(declaration.SyntaxTree) is { } model)
                    unresolved.AddRange(GetUnresolvedBaseTypes(baseType, name, declaration, model, locations, cancellationToken));

            ancestors.Add(new SourceAncestor(name,unresolved.ToImmutable(), locations.ToImmutable()));
        }

        return ancestors.ToImmutable();
    }

    private static bool IsDeclaredIn(ISymbol symbol, IAssemblySymbol assembly)
    {
        var isDeclaredIn = SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, assembly);
        return isDeclaredIn;
    }

    /// <summary>
    /// The public contracts the type implements (<see cref="PackageNames.IsContract"/>), which another assembly could
    /// implement too.
    /// </summary>
    private static ImmutableArray<string> GetPublicContractNames(INamedTypeSymbol type)
    {
        var contractNames = type.AllInterfaces
            .Where(static @interface => PackageNames.IsContract(@interface)
                                        && @interface.DeclaredAccessibility is Accessibility.Public)
            .Select(static @interface => @interface.ToDisplayString())
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray();

        return contractNames;
    }

    /// <summary>
    /// Whether the type implements another assembly's internal contract through <c>InternalsVisibleTo</c>, as a test
    /// project's set does. It isn't among <see cref="GetPublicContractNames"/>, because no other assembly sees it.
    /// </summary>
    private static bool ImplementsInternalContract(INamedTypeSymbol type)
    {
        var implementsInternalContract = type.AllInterfaces.Any(@interface => PackageNames.IsContract(@interface)
                                                                              && @interface.DeclaredAccessibility is not Accessibility.Public
                                                                              && !IsDeclaredIn(@interface, type.ContainingAssembly));
        return implementsInternalContract;
    }

    /// <summary>
    /// The contracts written by hand in the type's own assembly (<see cref="PackageNames.IsContract"/>), whatever their
    /// accessibility.
    /// </summary>
    private static ImmutableArray<string> GetOwnContractNames(INamedTypeSymbol type)
    {
        var contractNames = type.AllInterfaces
            .Where(@interface => PackageNames.IsContract(@interface)
                                 && IsDeclaredIn(@interface, type.ContainingAssembly))
            .Select(static @interface => @interface.ToDisplayString())
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToImmutableArray();

        return contractNames;
    }

    /// <summary>
    /// Whether generated code can name a set of this shape: concrete, closed, top-level and not file-local.
    /// </summary>
    private static bool IsNameableSet(INamedTypeSymbol type)
    {
        var isNameable = type is { IsAbstract: false, TypeKind: TypeKind.Class, IsGenericType: false, ContainingType: null, IsFileLocal: false };
        return isNameable;
    }

    /// <summary>
    /// Whether generated code in another file of the assembly can name a nested set: a concrete class that isn't generic,
    /// public or internal, in classes that are public or internal and not generic or file-local.
    /// </summary>
    private static bool IsNameableNestedSet(INamedTypeSymbol type)
    {
        if (type is not { IsAbstract: false, TypeKind: TypeKind.Class, IsGenericType: false, ContainingType: not null })
            return false;

        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType || current.IsFileLocal
                || current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Why generated code can't register the set, as the <c>LOC207</c> clause, or <see langword="null"/>.
    /// </summary>
    /// <param name="constructor">What <c>new T()</c> calls (<see cref="FindConstructorCalledWithoutArguments"/>).</param>
    /// <param name="hasLanguage">Whether the set has a language.</param>
    private static string? GetRegistrationProblem(INamedTypeSymbol type, IMethodSymbol? constructor, bool hasLanguage)
    {
        if (constructor is null)
            return "it has no parameterless constructor";

        // new T() compiles only when the constructor sets the required members itself.
        if (FindUnsetRequiredMember(type, constructor) is { } required)
            return $"it has a required member ('{required}'), which generated code can't set";

        // Else it would throw while the assembly loads, where the cause is hard to see.
        if (!hasLanguage)
            return NoLanguageCodeProblem;

        return null;
    }

    /// <summary>
    /// Why a set without a language code can't be registered, as the clause of the <c>LOC207</c> message.
    /// </summary>
    internal const string NoLanguageCodeProblem = "its name doesn't end in a language code that starts with a capital (two letters, as in ShopLocalizationNl, or a longer code the assembly declares, as in ShopLocalizationEnGb)";

    /// <summary>
    /// The constructor <c>new T()</c> calls from generated code in another file, or <see langword="null"/> when that
    /// doesn't compile.
    /// </summary>
    private static IMethodSymbol? FindConstructorCalledWithoutArguments(INamedTypeSymbol type, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        // A constructor whose parameters all have defaults or take params is called without arguments too.
        IMethodSymbol? callable = null;
        var count = 0;

        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)
                || !constructor.Parameters.All(static parameter => parameter.IsOptional || parameter.IsParams))
            {
                continue;
            }

            // The one that is better than every other.
            if (constructor.Parameters.Length is 0)
                return constructor;

            callable = constructor;
            count++;
        }

        if (count < 2)
            return callable;

        // The compiler picks among several. It's bound at the start of the file, outside every type, as generated code
        // is, so a private constructor is out of reach.
        var creation = SyntaxFactory.ParseExpression($"new {type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}()");

        var methodSymbol = semanticModel.GetSpeculativeSymbolInfo(position: 0, creation, SpeculativeBindingOption.BindAsExpression).Symbol as IMethodSymbol;
        return methodSymbol;
    }

    /// <summary>
    /// A required member that <c>new T()</c> leaves unset (<c>CS9035</c>), or <see langword="null"/>.
    /// </summary>
    private static string? FindUnsetRequiredMember(INamedTypeSymbol type, IMethodSymbol constructor)
    {
        if (Enumerable.Any(constructor.GetAttributes(), static attribute
                => String.Equals(attribute.AttributeClass?.ToDisplayString(), "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute", StringComparison.Ordinal)))
        {
            return null;
        }

        for (var current = type; current is not null; current = current.BaseType)
            foreach (var member in current.GetMembers())
                if (member is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true })
                    return member.Name;

        return null;
    }
}
