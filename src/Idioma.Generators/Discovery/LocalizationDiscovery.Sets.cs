namespace CodeChops.Idioma.Generators;

// The part of LocalizationDiscovery that resolves a class to a set: its shape, the classes it derives from, the
// contracts it implements, and whether generated code can create it to register it. A plain comment: the compiler
// merges the XML docs of every partial declaration, and the type's summary lives in LocalizationDiscovery.cs.
/// <inheritdoc cref="LocalizationDiscovery"/>
internal static partial class LocalizationDiscovery
{
    /// <summary>
    /// What an abstract, generic, nested, or file-local class gives discovery: a set rewording another assembly's texts
    /// where generated code can't reach it (<c>LOC208</c>), or a class listing <c>ILocalizationSet</c> as a
    /// default-language set does, which its shape keeps the generator from extending (<c>LOC200</c> in the default
    /// language); <see langword="null"/> for anything else, such as a test fixture.
    /// </summary>
    // Only overrides and classes that may start a contract: a nested set of the assembly's own contract is what a test
    // fixture is, registered by hand, and a language it was meant to give the assembly is reported as missing (LOC201)
    // anyway. Such a fixture lists ILocalizationSet and the contract, as a default-language set does, so the analysis
    // leaves out a class whose contract is generated from another set (UnextendableSet.UnresolvedBaseTypes).
    private static DiscoveryResult? DiscoverUnnameable(INamedTypeSymbol type, TypeDeclarationSyntax declaration,
        SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var reader = new WordingReader(semanticModel.Compilation, semanticModel);
        var typeName = type.ToDisplayString();

        // An override needs new T() to work. A class meant to start a contract lists one that binds to nothing yet, as
        // the default-language set lists the contract generated from it, or is partial, as such a set once was. The
        // base list is bound only when either could apply.
        var mayRegister = type is { IsAbstract: false, IsGenericType: false };
        var mayStartContract = declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                               || Enumerable.Any(type.Interfaces, static @interface => @interface.TypeKind is TypeKind.Error);

        if ((!mayRegister && !mayStartContract) || !ImplementsLocalizationSet(type) || IsExcluded(type))
            return null;

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

            // Of every part, so that each part of a partial class finds the same class. With those of the classes it
            // derives from, as for a set (DiscoverSet).
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
                Language: GetLanguage(type, ancestors, reader, cancellationToken),
                UnresolvedBaseTypes: new EquatableArray<UnresolvedBaseType>(unresolvedBaseTypes),
                Location: DiagnosticLocation.From(type.Locations[0]),
                BaseTypeLocations: new EquatableArray<BaseTypeLocation>(baseTypeLocations.ToImmutable()));
        }

        var result = unnameableOverride is null && unextendableSet is null
            ? null
            : new DiscoveryResult(Set: null, unnameableOverride, unextendableSet);

        return result;
    }

    /// <summary>
    /// Whether a class reads as the start of a contract, as a default-language set does: it lists
    /// <c>ILocalizationSet</c> itself, implements no contract, and derives from no concrete set (see
    /// <see cref="IsDefaultSet"/>). Whether a contract it lists is generated from another set is known only once every
    /// set is found, so the analysis asks that (<see cref="ListsOnlyOtherGeneratedContracts"/>).
    /// </summary>
    private static bool StartsContract(INamedTypeSymbol type)
    {
        if (!Enumerable.Any(type.Interfaces, IsLocalizationSetInterface) || ImplementsAnyContract(type))
            return false;

        var result = type.BaseType is not { IsAbstract: false, TypeKind: TypeKind.Class } baseType || !ImplementsLocalizationSet(baseType);
        return result;
    }

    /// <summary>
    /// Whether a set lists <c>ILocalizationSet</c>: itself, or through an abstract class it derives from, as
    /// <c>ShopLocalizationEn : ShopBase</c> does next to <c>abstract class ShopBase : ILocalizationSet,
    /// IShopLocalization</c>. An abstract class is no set, so the first concrete class that derives from it lists what
    /// it lists.
    /// </summary>
    /// <remarks>
    /// Only abstract classes are passed through. A concrete class the set derives from is a set of its own, which the
    /// set rewords (<see cref="RewordsSet"/>). An abstract class that derives from a concrete set rewords that set too,
    /// so what it lists doesn't count.
    /// </remarks>
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
    /// Resolves a concrete, closed, top-level class that implements <c>ILocalizationSet</c> to a discovered set. An
    /// excluded set is resolved too, and flagged. It reads what both parts of the generator use, and each part takes
    /// its own share of it (<see cref="ForPart"/>).
    /// </summary>
    private static DiscoveredLocalization DiscoverSet(INamedTypeSymbol type, TypeDeclarationSyntax declaration,
        SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        // One reader for the whole class: a model per file, and each wording read once.
        var reader = new WordingReader(semanticModel.Compilation, semanticModel);
        var ancestors = GetSourceAncestors(type, reader, cancellationToken);
        var metadataName = type.ToDisplayString();
        var baseTypeLocations = ImmutableArray.CreateBuilder<BaseTypeLocation>();

        // A class implements a contract generated here through its own base list, or through that of a class it derives
        // from: an abstract base shared by the languages, or the set a regional set rewords.
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

        // The LocalizationLanguageCode of the set once, and the code the build describes a set without one with. Its
        // language, its description, and whether it can register all ask for them.
        var languageCode = ReadSetLanguageCode(type);
        var declaresLanguageCode = languageCode.IsDeclared;
        var describedCode = declaresLanguageCode ? null : FindDescribedLanguageCode(type, ancestors, declaresLanguageCode: false);
        var unmappedLanguageCode = languageCode.Mapping.Unmapped;
        var constructor = FindConstructorCalledWithoutArguments(type, semanticModel, cancellationToken);
        var language = GetLanguage(type, languageCode, describedCode, reader, cancellationToken, out var invalidLanguageCode, out var unknownLanguageCode);
        var describedLanguageCode = declaresLanguageCode ? ReadImplementedLanguageCode(languageCode, reader, cancellationToken) : describedCode ?? GetNameLanguage(type.Name);

        // Not for a constant that is no culture name, which LOC207 reports as an error.
        var hasUnreadableLanguageCode = declaresLanguageCode && describedLanguageCode is null && invalidLanguageCode is null;
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
            RegistrationProblem: GetRegistrationProblem(type, constructor, hasLanguageCode: declaresLanguageCode || describedCode is not null, hasUnreadableLanguageCode),
            IsObsoleteAsError: IsObsoleteAsError(type),
            SuppressedDiagnostics: new EquatableArray<string>(GetSuppressedDiagnostics(type)),
            HasPublicParameterlessConstructor: Enumerable.Any(type.InstanceConstructors, static candidate => candidate is { Parameters.Length: 0, DeclaredAccessibility: Accessibility.Public }),
            IsExcluded: IsExcluded(type),
            Locations: new SetLocations(
                Set: location,
                InvalidLanguageCode: invalidLanguageCode,
                UnknownLanguageCode: unknownLanguageCode,
                UnmappedLanguageCode: unmappedLanguageCode is { ContainingType: { } declaring }
                    ? new UnmappedLanguageCode(
                        DeclaringType: declaring.Name,
                        DeclaringTypeBase: declaring.BaseType?.Name ?? "object",
                        // At the class that declares it, so that the sets deriving from that class report one error,
                        // where the fix is. A class of another assembly has no place in this one, and is reported at
                        // the set.
                        Location: IsDeclaredIn(declaring, type.ContainingAssembly) ? DiagnosticLocation.From(declaring.Locations[0]) : location)
                    : null,
                BaseTypes: new EquatableArray<BaseTypeLocation>(baseTypeLocations.ToImmutable())),
            Language: language,
            // Not for a LocalizationLanguageCode the build can't read: such a set has a language, which only the run time knows.
            NameGivesNoLanguage: language is null && invalidLanguageCode is null && !declaresLanguageCode && unmappedLanguageCode is null,
            DescribedLanguageCode: describedLanguageCode,
            // Only for a set deriving from another: the registry asks it of such a set alone, and a set deriving from
            // none would list every text, in every language, in the generated code.
            OwnTexts: new EquatableArray<string>(depth > 0 ? GetOwnTexts(type) : ImmutableArray<string>.Empty),
            ListsLocalizationSet: listsLocalizationSet,
            ContractNames: new EquatableArray<string>(GetPublicContractNames(type)),
            OwnContractNames: new EquatableArray<string>(GetOwnContractNames(type)),
            ImplementsInternalContract: ImplementsInternalContract(type),
            AncestorNames: new EquatableArray<string>([.. ancestors.Select(static ancestor => ancestor.Name)]),
            BaseSet: baseSet is null
                ? null
                : new BaseSet(
                    Name: baseSet.Name,
                    Language: GetLanguage(baseSet, [.. ancestors.Skip(1)], reader, cancellationToken),
                    Contracts: ImplementsLocalizationSet(baseSet) ? default : new EquatableArray<UnresolvedBaseType>(inheritedBaseTypes)),
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

        // The texts a contract is written from, which only a set a contract may come from has: one in the default
        // language, or in any language while the build can't read the default one, as the analysis takes them
        // (ReadsAsDefaultSet). Discovery reads every set again on every edit, and the texts are what it reads longest,
        // so a set of another language is left without them.
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
    /// The type or namespace that has the name the generated class of a default-language set would get, as a message
    /// names it (<c>the enum 'OrderStatus'</c>, <c>the namespace 'MyApp.Texts'</c>), or <see langword="null"/> when the
    /// name is free. <c>OrderStatusEn</c> next to the enum <c>OrderStatus</c> is such a set: the generated class would
    /// be a second <c>OrderStatus</c> (<c>CS0101</c>).
    /// </summary>
    // A static partial class of that name is the generated class itself, where a helper sits next to the texts. A type
    // with type parameters is another type, and so is one of another assembly, which the namespace of this class
    // doesn't hold.
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

        // A namespace of this compilation holds the name too, as the folder Texts next to TextsEn gives MyApp.Texts.
        foreach (var other in type.ContainingNamespace.GetNamespaceMembers())
        {
            if (String.Equals(other.Name, textsName, StringComparison.Ordinal) && other.Locations.Any(static location => location.IsInSource))
                return $"the namespace '{GetNamespaceName(other)}'";
        }

        // The enum whose texts the set holds, where it lies in another namespace and the set names it by its name
        // alone, as behind a using. Inside the set's namespace the generated class would stand before the enum, so the
        // set's own ILocalizationSet<OrderStatus> would name that class (CS0718).
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
    /// Whether a set lists <c>ILocalizationSet&lt;TEnum&gt;</c> with the enum written by its name alone, and not with
    /// its namespace in front. All the syntax can tell.
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
    /// The type that has the name the contract of a default-language set would get, as a message names it
    /// (<c>the interface 'IShopLocalization'</c>), or <see langword="null"/> when the name is free. The generated
    /// contract would be a second type of that name (<c>CS0101</c>). A contract written by hand isn't such a type: a set
    /// that implements it starts no contract.
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
    /// The texts of a set with a name its generated class can't give them (<c>LOC300</c>), each with where it's
    /// written: the member of the set, or the member of the enum for a text the set doesn't declare. A member outside
    /// this compilation has no place in it, so the set stands in.
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
    /// The texts a set of an enum declares that aren't a member of its enums (<c>LOC305</c>), each with where it's
    /// written. Not a text with a name its generated class can't give it, which <c>LOC300</c> reports.
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
    /// Whether the set rewords something of another assembly: it derives from a concrete set declared there, or
    /// implements a contract declared there. Such a set must win whichever assembly loads first. A set deriving from a
    /// set of its own assembly is no override (a library's regional set must not outrank an app's), and registers after
    /// its base instead (<see cref="GetDepth"/>).
    /// </summary>
    private static bool IsOverride(INamedTypeSymbol type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            if (!baseType.IsAbstract && ImplementsLocalizationSet(baseType) && !IsDeclaredIn(baseType, type.ContainingAssembly))
                return true;

        var isOverride = type.AllInterfaces.Any(@interface
            => PackageNames.IsContract(@interface)
               && !IsDeclaredIn(@interface, type.ContainingAssembly));

        return isOverride;
    }

    /// <summary>
    /// How many sets the type derives from. The generated registration adds a set after the sets it derives from, so a
    /// set rewording another of its own assembly wins within their tier.
    /// </summary>
    /// <remarks>
    /// An abstract class isn't counted, such as a base that the sets of every language share. It's never registered,
    /// so a set deriving from it rewords no set, and words every text itself. Counted, it would make the registrar
    /// name every text of every such set (<see cref="DiscoveredLocalization.OwnTexts"/>), which nothing reads.
    /// </remarks>
    /// <param name="type">The set.</param>
    /// <param name="ancestors">The classes of this assembly it derives from, nearest first, as the start of its chain.</param>
    private static int GetDepth(INamedTypeSymbol type, ImmutableArray<SourceAncestor> ancestors)
    {
        var depth = 0;
        var index = 0;
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType, index++)
        {
            if (baseType.IsAbstract)
                continue;

            // A class of this assembly implementing a contract generated here binds that contract to nothing yet, so
            // what it lists, or what a class it derives from lists, has to stand in for the interface.
            var implementsGeneratedContract = index < ancestors.Length
                                              && ancestors.Skip(index).Any(static ancestor => ancestor.UnresolvedBaseTypes.Length > 0);

            if (implementsGeneratedContract || ImplementsLocalizationSet(baseType))
                depth++;
        }

        return depth;
    }

    /// <summary>
    /// The classes of this assembly the type derives from, nearest first, each with the base types it lists itself that
    /// bind to nothing. Stops at the first class from elsewhere, whose base types all bind.
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

            ancestors.Add(new SourceAncestor(baseType, name, unresolved.ToImmutable(), locations.ToImmutable()));
        }

        return ancestors.ToImmutable();
    }

    private static bool IsDeclaredIn(ISymbol symbol, IAssemblySymbol assembly)
    {
        var isDeclaredIn = SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, assembly);
        return isDeclaredIn;
    }

    /// <summary>
    /// The contracts another assembly could implement too: the public contracts (<see cref="PackageNames.IsContract"/>)
    /// that the type resolves in the input compilation.
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
    /// Whether the type implements a contract of another assembly that isn't public, which it can only see through
    /// <c>InternalsVisibleTo</c>, as the set of a test project does for the internal contract of the library it tests.
    /// Such a contract counts for none of the assemblies referencing this one, so it isn't among
    /// <see cref="GetPublicContractNames"/>.
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
    /// accessibility. A contract the generator writes is not among them, as it does not exist in the input compilation.
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
    /// Whether discovery finds a set of this shape, so that generated code can name it: concrete, closed, top-level, and
    /// not file-local.
    /// </summary>
    private static bool IsNameableSet(INamedTypeSymbol type)
    {
        var isNameable = type is { IsAbstract: false, TypeKind: TypeKind.Class, IsGenericType: false, ContainingType: null, IsFileLocal: false };
        return isNameable;
    }

    /// <summary>
    /// Why generated code can't register the set, as the clause of the <c>LOC208</c> message, or
    /// <see langword="null"/> when it can.
    /// </summary>
    /// <param name="type">The set.</param>
    /// <param name="constructor">
    /// The constructor <c>new T()</c> calls (<see cref="FindConstructorCalledWithoutArguments"/>), or
    /// <see langword="null"/> when it calls none.
    /// </param>
    /// <param name="hasLanguageCode">
    /// Whether the set implements <c>LocalizationLanguageCode</c>, or the build describes it with a code
    /// (<see cref="FindDescribedLanguageCode(INamedTypeSymbol, ImmutableArray{SourceAncestor}, bool)"/>).
    /// </param>
    /// <param name="hasUnreadableLanguageCode">
    /// Whether the set implements <c>LocalizationLanguageCode</c> with something other than a constant the build can
    /// read, such as a value read at run time, or an implementation in another assembly.
    /// </param>
    private static string? GetRegistrationProblem(INamedTypeSymbol type, IMethodSymbol? constructor, bool hasLanguageCode,
        bool hasUnreadableLanguageCode)
    {
        // Naming it is an error (CS0619), which no pragma turns off.
        if (IsObsoleteAsError(type))
            return ObsoleteAsErrorProblem;

        if (constructor is null)
            return "it has no parameterless constructor";

        // new T() sets no member, and compiles only when the constructor says it sets the required ones itself.
        if (FindUnsetRequiredMember(type, constructor) is { } required)
            return $"it has a required member ('{required}'), which generated code can't set";

        // At run time this would throw while the assembly loads, where the cause is hard to see.
        if (!EndsInTwoLetterSuffix(type.Name) && !hasLanguageCode)
            return NoLanguageCodeProblem;

        // The registry files a set under the language the build describes it with, without creating it. Creating it to
        // ask its language ran its constructor while the registry was being built.
        if (hasUnreadableLanguageCode)
            return UnreadableLanguageCodeProblem;

        return null;
    }

    /// <summary>
    /// Why a set whose language code the build can't read can't be registered, as the clause of the <c>LOC208</c>
    /// message.
    /// </summary>
    internal const string UnreadableLanguageCodeProblem = "its LocalizationLanguageCode isn't a constant the build can read";

    /// <summary>
    /// Why a set without a language code can't be registered, as the clause of the <c>LOC208</c> message.
    /// </summary>
    internal const string NoLanguageCodeProblem = "its name doesn't end in a language code that starts with a capital (two letters, as in ShopLocalizationNl, or a longer code the assembly declares, as in ShopLocalizationEnGb), and it doesn't implement LocalizationLanguageCode";

    /// <summary>
    /// Why a set marked <c>[Obsolete]</c> as an error can't be registered, as the clause of a message.
    /// </summary>
    internal const string ObsoleteAsErrorProblem = "it's marked [Obsolete] as an error, and generated code can't name such a class";

    /// <summary>
    /// Whether a type or a member is marked <c>[Obsolete]</c> as an error, as in
    /// <c>[Obsolete("Use another set", error: true)]</c>. Naming it is an error (<c>CS0619</c>), which no pragma turns off.
    /// </summary>
    internal static bool IsObsoleteAsError(ISymbol symbol)
    {
        var isObsoleteAsError = symbol.GetAttributes().Any(static attribute =>
            attribute.AttributeClass is { Name: "ObsoleteAttribute", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } }
            && attribute.ConstructorArguments is [_, { Value: true }]);

        return isObsoleteAsError;
    }

    /// <summary>
    /// What writing a type in generated code reports besides <c>CS0612</c> and <c>CS0618</c>
    /// (<see cref="GetSuppressedDiagnostics(ISymbol)"/>), for the type, the types around it, and the types it's made of,
    /// such as the element of an array or a type argument. <c>Payment&lt;Money&gt;[]</c> reports what <c>Payment</c> and
    /// <c>Money</c> are marked with.
    /// </summary>
    internal static ImmutableArray<string> GetSuppressedDiagnosticsOfType(ITypeSymbol type)
    {
        var suppressed = new List<string>();
        Add(type, suppressed);

        return [.. suppressed.Distinct(StringComparer.Ordinal)];

        static void Add(ITypeSymbol type, List<string> suppressed)
        {
            switch (type)
            {
                case IArrayTypeSymbol array:
                    Add(array.ElementType, suppressed);
                    break;

                case INamedTypeSymbol named:
                    for (var current = named; current is not null; current = current.ContainingType)
                        suppressed.AddRange(GetSuppressedDiagnostics(current.OriginalDefinition));

                    foreach (var argument in named.TypeArguments)
                        Add(argument, suppressed);

                    break;
            }
        }
    }

    /// <summary>
    /// The diagnostics that naming a type or a member reports, other than <c>CS0612</c> and <c>CS0618</c>, which every
    /// generated file turns off: the id of <c>[Experimental("SHOP001")]</c>, and the <c>DiagnosticId</c> of
    /// <c>[Obsolete]</c>. The author of a set or a member marked so can't act on them in generated code, and a build
    /// that treats warnings as errors would stop at them.
    /// </summary>
    internal static ImmutableArray<string> GetSuppressedDiagnostics(ISymbol symbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var attribute in symbol.GetAttributes())
        {
            switch (attribute.AttributeClass)
            {
                case { Name: "ExperimentalAttribute", ContainingNamespace: { Name: "CodeAnalysis", ContainingNamespace: { Name: "Diagnostics", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } } }
                    when attribute.ConstructorArguments is [{ Value: string id }] && IsDiagnosticId(id):
                    builder.Add(id);
                    break;

                case { Name: "ObsoleteAttribute", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } }:
                    foreach (var argument in attribute.NamedArguments)
                        if (argument is { Key: "DiagnosticId", Value.Value: string id } && IsDiagnosticId(id))
                            builder.Add(id);

                    break;
            }
        }

        return builder.ToImmutable();

        // Written into a pragma, so only what a pragma can hold: letters, digits and underscores.
        static bool IsDiagnosticId(string id) => id.Length > 0 && id.All(static c => c is '_' || Char.IsLetterOrDigit(c));
    }

    /// <summary>
    /// The constructor <c>new T()</c> calls from generated code, which lies in another file of the set's assembly, or
    /// <see langword="null"/> when that does not compile: no constructor it can reach takes no arguments, or several do
    /// and none is better than the others.
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

        // Several of them: which one new T() calls, if any, is for the compiler to say, as it will for the generated
        // code. A default is better than params, and two defaults are ambiguous. Bound at the start of the file, outside
        // every type, as generated code is: there a private constructor is out of reach.
        var creation = SyntaxFactory.ParseExpression($"new {type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}()");

        var methodSymbol = semanticModel.GetSpeculativeSymbolInfo(position: 0, creation, SpeculativeBindingOption.BindAsExpression).Symbol as IMethodSymbol;
        return methodSymbol;
    }

    /// <summary>
    /// A required member of the set that <c>new T()</c> leaves unset (<c>CS9035</c>), or <see langword="null"/> when
    /// it has none, or the constructor sets them all (<c>[SetsRequiredMembers]</c>).
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
