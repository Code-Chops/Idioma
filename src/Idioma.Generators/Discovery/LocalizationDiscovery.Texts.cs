using System.Globalization;

namespace CodeChops.Idioma.Generators;

// The part of LocalizationDiscovery that reads the texts of a set: which members are texts, which it declares or
// inherits, and their wording, for the doc comments of the generated members. A plain comment: the compiler merges the
// XML docs of every partial declaration, and the type's summary lives in LocalizationDiscovery.cs.
/// <inheritdoc cref="LocalizationDiscovery"/>
internal static partial class LocalizationDiscovery
{
    /// <summary>
    /// The texts a cref can name on this set: the public string properties the class declares itself, and its methods
    /// of the shape of a text that takes values (<see cref="TextParameters.IsTextMethod"/>). A cref does not find a
    /// member through a class that only inherits it (<c>ShopLocalizationEnGb.Checkout</c>, declared by
    /// <c>ShopLocalizationEn</c>, is <c>CS1574</c>), so those are named where they are declared
    /// (<see cref="GetInheritedTexts"/>). An explicit interface implementation has no such name, and a method that
    /// shares its name with another has none of its own (<c>CS0419</c>).
    /// </summary>
    private static ImmutableArray<string> GetDeclaredTexts(INamedTypeSymbol type)
    {
        var names = ImmutableArray.CreateBuilder<string>();

        foreach (var member in type.GetMembers())
            if (IsNameableText(member) && !String.Equals(member.Name, LanguageCodeMemberName, StringComparison.Ordinal))
                names.Add(member.Name);

        var declaredTexts = names.Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal).ToImmutableArray();
        return declaredTexts;
    }

    /// <summary>
    /// Whether a member is a text a cref can name by its name alone: a public string property, or a method of the
    /// shape of a text that takes values whose class declares no other member of that name.
    /// </summary>
    private static bool IsNameableText(ISymbol member)
    {
        var isNameableText = IsPublicStringProperty(member, out _)
                             || (TextParameters.IsTextMethod(member, out var method) && TextParameters.HasNameOfItsOwn(method));

        return isNameableText;
    }

    /// <summary>
    /// The texts this set inherits (public string properties, and methods of the shape of a text that takes values),
    /// each with the nearest class declaring it: where a cref names a text the set does not declare itself, such as
    /// <c>ShopLocalizationEn.Checkout</c> for a British set that leaves <c>Checkout</c> as it is. A text that the set,
    /// or a class between it and the one declaring the text, implements explicitly is left out, because that
    /// implementation is its wording, and has no name. So is a method that shares its name with another of its class,
    /// which a cref can't name by its name alone.
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
                if (!IsNameableText(member) || !settled.Add(member.Name) || SymbolEqualityComparer.Default.Equals(current, type))
                    continue;

                texts.Add(new InheritedText(
                    Name: member.Name,
                    Namespace: GetNamespaceName(current.ContainingNamespace),
                    TypeName: GetCrefName(current)));
            }

            // After the texts a cref can name, so that a class holding both is linked at the one with a name. A method
            // that shares its name with another settles it too: the nearest class has the wording, name or no name.
            foreach (var member in members)
            {
                if (member is IPropertySymbol { IsStatic: false } or IMethodSymbol { IsStatic: false, MethodKind: MethodKind.ExplicitInterfaceImplementation }
                    && GetExplicitlyImplementedName(member) is { } name)
                {
                    settled.Add(name);
                }
                else if (TextParameters.IsTextMethod(member, out _))
                {
                    settled.Add(member.Name);
                }
            }
        }

        var orderedTexts = texts.OrderBy(static text => text.Name, StringComparer.Ordinal).ToImmutableArray();
        return orderedTexts;
    }

    /// <summary>
    /// The names of the texts a set's own class words, rather than leaving them to the class it derives from. They're
    /// the public string properties and the methods of the shape of a text that it declares, overrides included, and
    /// the members it implements explicitly. The generated registrar passes them on for a set deriving from another, so
    /// the registry knows which texts the set rewords without calling it.
    /// </summary>
    private static ImmutableArray<string> GetOwnTexts(INamedTypeSymbol type)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var member in type.GetMembers())
        {
            // A member that hides a text words nothing, because the contract calls the member it hides (LOC302).
            // Passed on, it would keep the registry from the wording a rewording of the language gives that text.
            if (HidesText(member, type))
                continue;

            if (IsPublicStringProperty(member, out _) || TextParameters.IsTextMethod(member, out _))
            {
                names.Add(member.Name);
            }
            else if (member is IPropertySymbol { IsStatic: false, Type.SpecialType: SpecialType.System_String }
                         or IMethodSymbol { IsStatic: false, MethodKind: MethodKind.ExplicitInterfaceImplementation, ReturnType.SpecialType: SpecialType.System_String }
                     && GetExplicitlyImplementedName(member) is { } name)
            {
                names.Add(name);
            }
        }

        names.Remove(LanguageCodeMemberName);

        return [.. names];
    }

    /// <summary>
    /// The texts a set inherits from the classes of its assembly it derives from, without declaring them: public string
    /// properties, and methods of the shape of a text that takes values. A contract takes only the texts its set
    /// declares, so it leaves these out, and no other language has to word them (<c>LOC303</c>).
    /// </summary>
    /// <remarks>
    /// A member the set declares again, overrides, or implements explicitly settles its name, and so does one of a
    /// nearer class. A member marked <c>[ExcludeFromLocalizationDiscovery]</c> is no text. A class of another assembly
    /// isn't read, because its members weren't written as texts, such as <c>Exception.Message</c>.
    /// </remarks>
    private static ImmutableArray<UndeclaredText> GetUndeclaredTexts(INamedTypeSymbol type)
    {
        var settled = new HashSet<string>(StringComparer.Ordinal) { LanguageCodeMemberName };

        foreach (var member in type.GetMembers())
        {
            settled.Add(member.Name);

            if (GetExplicitlyImplementedName(member) is { } name)
                settled.Add(name);
        }

        var undeclared = ImmutableArray.CreateBuilder<UndeclaredText>();

        for (var current = type.BaseType; current is { SpecialType: not SpecialType.System_Object }; current = current.BaseType)
        {
            if (!SymbolEqualityComparer.Default.Equals(current.ContainingAssembly, type.ContainingAssembly))
                break;

            // Each name once, from the nearest class that has a member of it.
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var member in current.GetMembers())
            {
                if (settled.Contains(member.Name))
                    continue;

                names.Add(member.Name);

                // Once per name, though a method may have overloads.
                if (!IsExcluded(member)
                    && (IsPublicStringProperty(member, out _) || TextParameters.IsTextMethod(member, out _))
                    && !undeclared.Any(text => String.Equals(text.Name, member.Name, StringComparison.Ordinal)))
                {
                    undeclared.Add(new UndeclaredText(member.Name, current.Name, DiagnosticLocation.From(member.Locations[0])));
                }
            }

            settled.UnionWith(names);
        }

        return undeclared.ToImmutable();
    }

    /// <summary>
    /// The part of a partial property or method that implements it, which holds its body, or the member itself when
    /// it isn't partial. A class lists a partial member by the part that declares it.
    /// </summary>
    internal static ISymbol GetImplementingPart(ISymbol member)
    {
        var implementingPart = member switch
        {
            IMethodSymbol { PartialImplementationPart: { } method } => method,
            IPropertySymbol { PartialImplementationPart: { } property } => property,
            _ => member,
        };

        return implementingPart;
    }

    /// <summary>
    /// Whether a member of a set hides a text of a class it derives from, rather than overriding it:
    /// <c>public new string Color => "Colour";</c> in a British set deriving from the English one. A contract then calls
    /// the member it hides, so this member words nothing. It does when the set lists a contract again, because that maps
    /// the contract to the nearest member of the name: this one.
    /// </summary>
    /// <remarks>
    /// A contract generated here binds to nothing yet, and counts as listed again. So does any interface of the set's
    /// own base list besides <c>ILocalizationSet</c>, which a set lists again to give its language a code.
    /// </remarks>
    internal static bool HidesText(ISymbol member, INamedTypeSymbol type)
    {
        if (member is not (IPropertySymbol { IsStatic: false, IsOverride: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public }
            or IMethodSymbol { IsStatic: false, IsOverride: false, MethodKind: MethodKind.Ordinary, DeclaredAccessibility: Accessibility.Public }))
        {
            return false;
        }

        if (Enumerable.Any(type.Interfaces, static @interface => !IsLocalizationSetInterface(@interface)))
            return false;

        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            foreach (var hidden in current.GetMembers(member.Name))
            {
                var isHidden = (member, hidden) switch
                {
                    (IPropertySymbol, IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public }) => true,
                    (IMethodSymbol method, IMethodSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public } other) => HaveSameParameters(method, other),
                    _ => false,
                };

                if (isHidden)
                    return true;
            }
        }

        return false;

        static bool HaveSameParameters(IMethodSymbol method, IMethodSymbol other)
        {
            if (method.Parameters.Length != other.Parameters.Length)
                return false;

            for (var i = 0; i < method.Parameters.Length; i++)
                if (!SymbolEqualityComparer.Default.Equals(method.Parameters[i].Type, other.Parameters[i].Type) || method.Parameters[i].RefKind != other.Parameters[i].RefKind)
                    return false;

            return true;
        }
    }

    /// <summary>
    /// The wording this set gives each of its texts that returns a constant, from the nearest class implementing the
    /// text, in the order of the names: its own property, the property it inherits (<see cref="GetInheritedTexts"/>), or
    /// an explicit implementation, which wins over a property of the same name in its class. A text a class implements
    /// explicitly for two interfaces has no one wording, and a text whose wording is no constant, or is declared where
    /// this compilation has no syntax for it, has none either. A text that takes values has the wording its method
    /// writes, with each value in braces (<see cref="MethodWording"/>), unless its class has two methods of its name.
    /// </summary>
    private static ImmutableArray<TextWording> GetWordings(INamedTypeSymbol type, WordingReader reader, CancellationToken cancellationToken)
    {
        var wordings = ImmutableArray.CreateBuilder<TextWording>();

        // The names a nearer class has settled, with a wording or without one.
        var settled = new HashSet<string>(StringComparer.Ordinal) { LanguageCodeMemberName };

        for (var current = type; current is not null; current = current.BaseType)
        {
            var members = current.GetMembers();

            // Null for a name the class implements explicitly more than once.
            var explicitImplementations = new Dictionary<string, ISymbol?>(StringComparer.Ordinal);
            foreach (var member in members)
            {
                if (member is IPropertySymbol { IsStatic: false, Type.SpecialType: SpecialType.System_String }
                        or IMethodSymbol { IsStatic: false, MethodKind: MethodKind.ExplicitInterfaceImplementation, ReturnType.SpecialType: SpecialType.System_String }
                    && GetExplicitlyImplementedName(member) is { } name)
                {
                    explicitImplementations[name] = explicitImplementations.ContainsKey(name) ? null : member;
                }
            }

            foreach (var pair in explicitImplementations)
                if (settled.Add(pair.Key) && pair.Value is not null && reader.Read(pair.Value, cancellationToken) is { } wording)
                    wordings.Add(new TextWording(pair.Key, wording));

            foreach (var member in members)
                if (IsPublicStringProperty(member, out _) && settled.Add(member.Name) && reader.Read(member, cancellationToken) is { } wording)
                    wordings.Add(new TextWording(member.Name, wording));

            // Two methods of one name have no one wording, and settle the name all the same.
            foreach (var member in members)
            {
                if (TextParameters.IsTextMethod(member, out var method)
                    && settled.Add(method.Name)
                    && TextParameters.HasNameOfItsOwn(method)
                    && reader.Read(method, cancellationToken) is { } wording)
                {
                    wordings.Add(new TextWording(method.Name, wording));
                }
            }
        }

        var orderedWordings = wordings.OrderBy(static wording => wording.Name, StringComparer.Ordinal).ToImmutableArray();
        return orderedWordings;
    }

    /// <summary>
    /// A class as a cref names it inside its namespace, its type parameters in braces: <c>DutchBase{T}</c>.
    /// </summary>
    private static string GetCrefName(INamedTypeSymbol type)
    {
        var crefName = type.OriginalDefinition.ToDisplayString(CrefNameFormat).Replace('<', '{').Replace('>', '}');
        return crefName;
    }

    /// <summary>
    /// The name of the member a property or method implements explicitly (<c>Checkout</c> for
    /// <c>string IShopLocalization.Checkout</c>), or <see langword="null"/> for any other member.
    /// </summary>
    // Read from the member's own name, which holds the interface as it is written: the symbol names no implemented
    // member for a contract generated here, which binds to nothing yet.
    private static string? GetExplicitlyImplementedName(ISymbol member)
    {
        var separator = member.Name.LastIndexOf('.');

        var explicitlyImplementedName = separator < 0 ? null : member.Name.Substring(separator + 1);
        return explicitlyImplementedName;
    }

    /// <summary>
    /// The texts: the public, instance, non-indexer <see cref="string"/> properties with a public getter declared by
    /// the class itself, and its methods of the shape of a text that takes values
    /// (<see cref="TextParameters.IsTextMethod"/>), in the order they're written, by file and then by position (an inherited member belongs to the base's
    /// own contract). <c>LocalizationLanguageCode</c> (the <c>ILocalizationSet</c> member, which a set may override) and explicit
    /// interface implementations are not texts, and neither is a member marked
    /// <c>[ExcludeFromLocalizationDiscovery]</c>. A method of that shape that can't be a text
    /// (<see cref="TextParameters.FindProblem"/>) is left out, and handed back to be reported (<c>LOC400</c>). So is
    /// a text written as a method without parameters (<see cref="TextParameters.IsTextWithoutParameters"/>). With
    /// them come the using directives the contract repeats (<see cref="RepeatedUsings"/>).
    /// </summary>
    /// <remarks>
    /// A set of the texts of an enum (<see cref="GetLocalizedEnums"/>) has a text for every member of that enum, as a
    /// property named after the member. A member the class declares no such text for gets one all the same, after the
    /// texts that are written, in the order of the enum. The contract then asks every set for it, the
    /// default-language set too (<c>CS0535</c>). A method with the name of a member can't be its text, and is handed
    /// back to be reported (<c>LOC400</c>).
    /// </remarks>
    private static ImmutableArray<LocalizationMember> GetTextMembers(INamedTypeSymbol type, ImmutableArray<LocalizedEnum> enums, WordingReader reader,
        CancellationToken cancellationToken, out ImmutableArray<UnsupportedText> unsupported, out RepeatedUsings usings)
    {
        // Each member of an enum by its name, with the enum it's of.
        var enumMembers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var @enum in enums)
            foreach (var enumMember in @enum.Members)
                if (!enumMembers.ContainsKey(enumMember.Name))
                    enumMembers.Add(enumMember.Name, @enum.Name);

        var members = ImmutableArray.CreateBuilder<(Location Location, LocalizationMember Member)>();
        var unsupportedTexts = ImmutableArray.CreateBuilder<UnsupportedText>();
        var usingsOfFile = ImmutableArray.CreateBuilder<string>();
        var usingsOfNamespace = ImmutableArray.CreateBuilder<string>();

        foreach (var member in type.GetMembers())
        {
            // A member marked [ExcludeFromLocalizationDiscovery] is no text, whatever its shape: a helper that has to
            // be public.
            if (String.Equals(member.Name, LanguageCodeMemberName, StringComparison.Ordinal) || IsExcluded(member))
                continue;

            // A partial member is read where it's implemented, because the part that declares it has no body.
            var implementingPart = GetImplementingPart(member);
            var declaration = implementingPart.DeclaringSyntaxReferences.Length > 0
                ? implementingPart.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) as MemberDeclarationSyntax
                : null;

            if (IsPublicStringProperty(member, out var property))
            {
                members.Add((DeclaredAt(member), new LocalizationMember(
                    Name: property.Name,
                    HasDocComment: declaration is not null && HasDocComment(declaration),
                    DefaultText: declaration is PropertyDeclarationSyntax ? reader.Read(declaration, cancellationToken) : null,
                    Parameters: default,
                    Marks: ReadMarks(property),
                    IsObsoleteAsError: IsObsoleteAsError(property),
                    SuppressedDiagnostics: new EquatableArray<string>(GetSuppressedDiagnostics(property)))));
            }
            else if (TextParameters.IsTextMethod(member, out var method))
            {
                // The text of an enum member takes no values, so that the text of a value can be looked up.
                if (enumMembers.TryGetValue(method.Name, out var enumName))
                {
                    unsupportedTexts.Add(new UnsupportedText(
                        method.Name,
                        $"it has the name of '{enumName}.{method.Name}', and the text of an enum member is a property, because it takes no values",
                        DiagnosticLocation.From(method.Locations[0])));

                    continue;
                }

                if (TextParameters.FindProblem(method) is { } problem)
                {
                    unsupportedTexts.Add(new UnsupportedText(method.Name, problem, DiagnosticLocation.From(method.Locations[0])));
                    continue;
                }

                // A type another generator writes binds to nothing yet, and is written by the words the method writes
                // it in. They name it only next to the using directives of the method, which the contract repeats.
                if (declaration is MethodDeclarationSyntax written && TextParameters.HasUnboundType(method))
                {
                    var (ofFile, ofNamespace) = TextParameters.ReadUsings(written);
                    usingsOfFile.AddRange(ofFile);
                    usingsOfNamespace.AddRange(ofNamespace);
                }

                members.Add((DeclaredAt(member), new LocalizationMember(
                    Name: method.Name,
                    HasDocComment: declaration is not null && HasDocComment(declaration),
                    DefaultText: declaration is MethodDeclarationSyntax ? reader.Read(declaration, cancellationToken) : null,
                    Parameters: TextParameters.Read(
                        (IMethodSymbol)implementingPart,  // The part whose body is read, as its parameters are what the body names.
                        declaration as MethodDeclarationSyntax,
                        // Only in a tree of this compilation, as for the wording (WordingReader.FindModel).
                        declaration is null ? null : reader.FindModel(declaration.SyntaxTree),
                        cancellationToken),
                    Marks: ReadMarks(method),
                    IsObsoleteAsError: IsObsoleteAsError(method),
                    SuppressedDiagnostics: new EquatableArray<string>(GetSuppressedDiagnostics(method)))));
            }
            else if (TextParameters.IsTextWithoutParameters(member, out var withoutParameters))
            {
                // No text, and reported: what calls it as one would find nothing by its name (CS0117).
                unsupportedTexts.Add(new UnsupportedText(withoutParameters.Name, TextParameters.WithoutParametersProblem, DiagnosticLocation.From(withoutParameters.Locations[0])));
            }
        }

        unsupported = unsupportedTexts.ToImmutable();

        // Each directive once, though two texts may bring it, or two files of a partial set.
        usings = new RepeatedUsings(
            new EquatableArray<string>([.. usingsOfFile.Distinct(StringComparer.Ordinal)]),
            new EquatableArray<string>([.. usingsOfNamespace.Distinct(StringComparer.Ordinal)]));

        // As written on the set, by file and then by position, so that a partial set orders the same on every build.
        // A member from a referenced assembly has no file, and keeps the order its metadata gives.
        var ordered = members.ToImmutable()
            .OrderBy(static item => item.Location.IsInSource ? item.Location.SourceTree!.FilePath : "", StringComparer.Ordinal)
            .ThenBy(static item => item.Location.IsInSource ? item.Location.SourceSpan.Start : 0)
            .Select(static item => item.Member)
            .ToImmutableArray();

        if (enumMembers.Count is 0)
            return ordered;

        // After the texts that are written: a text for each member of an enum that the class declares none for.
        var texts = ordered.ToBuilder();
        var names = new HashSet<string>(ordered.Select(static member => member.Name), StringComparer.Ordinal);

        foreach (var @enum in enums)
            foreach (var enumMember in @enum.Members)
                if (names.Add(enumMember.Name))
                    texts.Add(new LocalizationMember(Name: enumMember.Name, HasDocComment: false, DefaultText: null, Parameters: default));

        return texts.ToImmutable();
    }

    /// <summary>
    /// The enums whose members a set has a text for: the type argument of each <c>ILocalizationSet&lt;TEnum&gt;</c> it
    /// implements, in the order of their full names. An argument that binds to no enum, as one another generator
    /// writes, is left out.
    /// </summary>
    private static ImmutableArray<LocalizedEnum> GetLocalizedEnums(INamedTypeSymbol type)
    {
        var enums = ImmutableArray.CreateBuilder<LocalizedEnum>();
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var @interface in type.AllInterfaces)
        {
            if (!PackageNames.IsLocalizationSetOfEnum(@interface)
                || @interface.TypeArguments[0] is not INamedTypeSymbol { TypeKind: TypeKind.Enum } @enum)
            {
                continue;
            }

            var fullyQualifiedName = @enum.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
            if (!found.Add(fullyQualifiedName))
                continue;

            var members = ImmutableArray.CreateBuilder<LocalizedEnumMember>();
            var values = new HashSet<object>();
            var suppressed = new List<string>(GetSuppressedDiagnosticsOfType(@enum));

            foreach (var member in @enum.GetMembers())
            {
                if (member is not IFieldSymbol { HasConstantValue: true, ConstantValue: { } value })
                    continue;

                members.Add(new LocalizedEnumMember(
                    member.Name,
                    IsFirstOfItsValue: values.Add(value),
                    ObsoleteAsErrorValue: IsObsoleteAsError(member) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null));

                suppressed.AddRange(GetSuppressedDiagnostics(member));
            }

            var holder = FindGetTextOfItsAssembly(@enum, type.ContainingAssembly);

            enums.Add(new LocalizedEnum(
                Type: TextParameters.ReadType(@enum),
                FullyQualifiedName: fullyQualifiedName,
                Name: @enum.ToDisplayString(CrefNameFormat),
                Namespace: GetNamespaceName(@enum.ContainingNamespace),
                IsPublic: IsPublic(@enum),
                Members: new EquatableArray<LocalizedEnumMember>(members.ToImmutable()),
                SuppressedDiagnostics: new EquatableArray<string>([.. suppressed.Distinct(StringComparer.Ordinal)]),
                HasTextsOfItsAssembly: holder is not null,
                ReachesGetTextOfItsAssembly: holder is not null
                                             && (holder.DeclaredAccessibility is Accessibility.Public || holder.ContainingAssembly.GivesAccessTo(type.ContainingAssembly))));
        }

        var ordered = enums.OrderBy(static @enum => @enum.FullyQualifiedName, StringComparer.Ordinal).ToImmutableArray();
        return ordered;

        // The class of another assembly that gives the enum its GetText() from the [Localized] of its members, by the
        // name its generator gives it (LocalizedMembers), or null. Only an assembly that uses Idioma writes one.
        static INamedTypeSymbol? FindGetTextOfItsAssembly(INamedTypeSymbol @enum, IAssemblySymbol setAssembly)
        {
            if (SymbolEqualityComparer.Default.Equals(@enum.ContainingAssembly, setAssembly))
                return null;

            var chain = new List<string>();

            for (var current = @enum; current is not null; current = current.ContainingType)
            {
                // An enum inside a type with type parameters gets no GetText().
                if (current.Arity > 0)
                    return null;

                chain.Insert(0, current.Name);
            }

            var namespaceName = @enum.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() + "." : "";
            var holder = @enum.ContainingAssembly.GetTypeByMetadataName(namespaceName + String.Join("_", chain) + LocalizedMembers.ExtensionHolderSuffix);

            return holder is { IsStatic: true, ContainingType: null } ? holder : null;
        }

        // Whether code outside the assembly can name the enum: it's public, and so is every type that holds it.
        static bool IsPublic(INamedTypeSymbol @enum)
        {
            for (var current = @enum; current is not null; current = current.ContainingType)
                if (current.DeclaredAccessibility is not Accessibility.Public)
                    return false;

            return true;
        }
    }

    /// <summary>
    /// Where a member is written, or <see cref="Location.None"/> when it comes from a referenced assembly.
    /// </summary>
    private static Location DeclaredAt(ISymbol member)
    {
        var location = member.Locations.FirstOrDefault(static location => location.IsInSource) ?? Location.None;
        return location;
    }

    /// <summary>
    /// Whether a member has the shape of a text without values: a public instance property of type
    /// <see cref="string"/>, with a public getter, that isn't an indexer or an explicit interface implementation.
    /// </summary>
    public static bool IsPublicStringProperty(ISymbol member, out IPropertySymbol property)
    {
        property = (member as IPropertySymbol)!;

        var isPublicStringProperty = member is IPropertySymbol
        {
            DeclaredAccessibility: Accessibility.Public,
            IsStatic: false,
            IsIndexer: false,
            GetMethod.DeclaredAccessibility: Accessibility.Public,
            ExplicitInterfaceImplementations.Length: 0,
            Type.SpecialType: SpecialType.System_String,
        };

        return isPublicStringProperty;
    }

    /// <summary>
    /// Whether the property or method carries a doc comment. Checked on the trivia rather than through the symbol,
    /// because a project that emits no documentation file parses <c>///</c> as a plain comment, and the symbol reports
    /// none.
    /// </summary>
    private static bool HasDocComment(MemberDeclarationSyntax declaration)
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
    /// The attributes <c>[Obsolete]</c>, <c>[Experimental]</c> and <c>[StringSyntax]</c> of a text, as the generated
    /// member repeats them, or <see langword="null"/> when it has none of them, with their arguments as constants. The
    /// file of the generated class imports <c>System</c>, as for every type of the framework it names, and not the
    /// namespace of the other two, which it writes in full.
    /// </summary>
    /// <remarks>
    /// <c>[Obsolete]</c> and <c>[Experimental]</c> tell the code that shows the text. <c>[StringSyntax]</c> tells it what
    /// the wording is, such as a composite format whose placeholders an attribute fills in. That one keeps the analyzer
    /// from reporting the placeholders in every language (<c>LOC403</c>). Only its first argument is repeated, which a
    /// property can be given alone.
    /// </remarks>
    private static string? ReadMarks(ISymbol text)
    {
        var marks = new StringBuilder();

        foreach (var attribute in text.GetAttributes())
        {
            var name = attribute.AttributeClass switch
            {
                { Name: "ObsoleteAttribute", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } => "Obsolete",
                { Name: "ExperimentalAttribute", ContainingNamespace: { Name: "CodeAnalysis", ContainingNamespace: { Name: "Diagnostics", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } } }
                    => "System.Diagnostics.CodeAnalysis.Experimental",
                { Name: "StringSyntaxAttribute", ContainingNamespace: { Name: "CodeAnalysis", ContainingNamespace: { Name: "Diagnostics", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } } }
                    when attribute.ConstructorArguments is [{ Kind: TypedConstantKind.Primitive, Value: string syntax }]
                    => $"System.Diagnostics.CodeAnalysis.StringSyntax({SymbolDisplay.FormatLiteral(syntax, quote: true)})",
                _ => null,
            };

            if (name is null || attribute.ConstructorArguments.Any(static argument => argument.Kind is TypedConstantKind.Error))
                continue;

            // Written whole, with its one argument.
            if (name.StartsWith("System.Diagnostics.CodeAnalysis.StringSyntax(", StringComparison.Ordinal))
            {
                if (marks.Length > 0)
                    marks.Append(' ');

                marks.Append($"[{name}]");
                continue;
            }

            var arguments = attribute.ConstructorArguments.Select(static argument => argument.ToCSharpString())
                .Concat(attribute.NamedArguments.Select(static argument => $"{argument.Key} = {argument.Value.ToCSharpString()}"));

            if (marks.Length > 0)
                marks.Append(' ');

            marks.Append($"[{name}({String.Join(", ", arguments)})]");
        }

        return marks.Length > 0 ? marks.ToString() : null;
    }
}
