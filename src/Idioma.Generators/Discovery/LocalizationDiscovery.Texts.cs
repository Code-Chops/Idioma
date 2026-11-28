namespace CodeChops.Idioma.Generators;

// Reads the texts of a set: which members are texts, which it declares or inherits, and their wordings.
/// <inheritdoc cref="LocalizationDiscovery"/>
internal static partial class LocalizationDiscovery
{
    /// <summary>
    /// The texts a cref can name on this set, which are the ones the class declares itself (<see cref="IsNameableText"/>).
    /// A cref doesn't find an inherited member (<c>CS1574</c>), so those are named where they're declared
    /// (<see cref="GetInheritedTexts"/>).
    /// </summary>
    private static ImmutableArray<string> GetDeclaredTexts(INamedTypeSymbol type)
    {
        var names = ImmutableArray.CreateBuilder<string>();

        foreach (var member in type.GetMembers())
            if (IsNameableText(member))
                names.Add(member.Name);

        var declaredTexts = names.Distinct(StringComparer.Ordinal).OrderBy(static name => name, StringComparer.Ordinal).ToImmutableArray();
        return declaredTexts;
    }

    /// <summary>
    /// Whether a member is a text a cref can name by its name alone: a public string property, or a text method whose
    /// class declares no other member of that name (<c>CS0419</c>).
    /// </summary>
    private static bool IsNameableText(ISymbol member)
    {
        var isNameableText = IsPublicStringProperty(member, out _)
                             || (TextParameters.IsTextMethod(member, out var method) && TextParameters.HasNameOfItsOwn(method));

        return isNameableText;
    }

    /// <summary>
    /// The texts this set inherits, each with the nearest class declaring it, as a cref names it
    /// (<c>ShopLocalizationEn.Checkout</c> for a British set). A text a nearer class implements explicitly is left out,
    /// because that implementation has its wording and no name.
    /// </summary>
    private static ImmutableArray<InheritedText> GetInheritedTexts(INamedTypeSymbol type)
    {
        var texts = ImmutableArray.CreateBuilder<InheritedText>();

        // The names a nearer class has declared or implemented explicitly.
        var settled = new HashSet<string>(StringComparer.Ordinal);

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

            // After the nameable texts, so a class holding both is linked at the one with a name. An overloaded method
            // settles its name too, because the nearest class has the wording.
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
    /// The texts a set inherits from base classes of its assembly without declaring them, which its contract leaves out
    /// (<c>LOC303</c>). A class of another assembly isn't read, because its members, such as <c>Exception.Message</c>,
    /// weren't written as texts.
    /// </summary>
    private static ImmutableArray<UndeclaredText> GetUndeclaredTexts(INamedTypeSymbol type)
    {
        var settled = new HashSet<string>(StringComparer.Ordinal);

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
    /// The implementing part of a partial property or method, which holds its body, or the member itself.
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
    /// The constant wording of each text, from the nearest class implementing it, ordered by name. An explicit
    /// implementation wins over a property of the same name in its class. A text with parameters has the wording its
    /// method writes (<see cref="MethodWording"/>). A text has none when it isn't a constant, has no syntax here, or is
    /// implemented twice.
    /// </summary>
    private static ImmutableArray<TextWording> GetWordings(INamedTypeSymbol type, WordingReader reader, CancellationToken cancellationToken)
    {
        var wordings = ImmutableArray.CreateBuilder<TextWording>();

        // The names a nearer class has settled, with a wording or without one.
        var settled = new HashSet<string>(StringComparer.Ordinal);

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
    /// <c>string IShopLocalization.Checkout</c>), or <see langword="null"/>.
    /// </summary>
    // Read from the member's name, because a generated contract doesn't bind yet, so the symbol names no implemented member.
    private static string? GetExplicitlyImplementedName(ISymbol member)
    {
        var separator = member.Name.LastIndexOf('.');

        var explicitlyImplementedName = separator < 0 ? null : member.Name.Substring(separator + 1);
        return explicitlyImplementedName;
    }

    /// <summary>
    /// The texts the class declares itself (<see cref="IsPublicStringProperty"/>, <see cref="TextParameters.IsTextMethod"/>),
    /// by file and then by position. A method that can't be a text (<see cref="TextParameters.FindProblem"/>,
    /// <see cref="TextParameters.IsTextWithoutParameters"/>) is handed back to be reported (<c>LOC400</c>). With them come
    /// the using directives the contract repeats (<see cref="RepeatedUsings"/>).
    /// </summary>
    /// <remarks>
    /// A set of an enum gets a text for each enum member it declares none for, after the others, so the contract asks
    /// every set for it (<c>CS0535</c>).
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
            if (IsExcluded(member))
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
                    Marks: ReadMarks(property))));
            }
            else if (TextParameters.IsTextMethod(member, out var method))
            {
                // The text of an enum member has no parameters, so it can be looked up by value.
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

                // A type another generator writes is copied as written, so the contract repeats the method's usings.
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
                        // Only in a tree of this compilation (WordingReader.FindModel).
                        declaration is null ? null : reader.FindModel(declaration.SyntaxTree),
                        cancellationToken),
                    Marks: ReadMarks(method))));
            }
            else if (TextParameters.IsTextWithoutParameters(member, out var withoutParameters))
            {
                // Reported, because code calling it as a text would find nothing (CS0117).
                unsupportedTexts.Add(new UnsupportedText(withoutParameters.Name, TextParameters.WithoutParametersProblem, DiagnosticLocation.From(withoutParameters.Locations[0])));
            }
        }

        unsupported = unsupportedTexts.ToImmutable();

        usings = new RepeatedUsings(
            new EquatableArray<string>([.. usingsOfFile.Distinct(StringComparer.Ordinal)]),
            new EquatableArray<string>([.. usingsOfNamespace.Distinct(StringComparer.Ordinal)]));

        // By file and then by position, so a partial set orders the same on every build.
        var ordered = members.ToImmutable()
            .OrderBy(static item => item.Location.IsInSource ? item.Location.SourceTree!.FilePath : "", StringComparer.Ordinal)
            .ThenBy(static item => item.Location.IsInSource ? item.Location.SourceSpan.Start : 0)
            .Select(static item => item.Member)
            .ToImmutableArray();

        if (enumMembers.Count is 0)
            return ordered;

        // A text for each enum member the class declares none for.
        var texts = ordered.ToBuilder();
        var names = new HashSet<string>(ordered.Select(static member => member.Name), StringComparer.Ordinal);

        foreach (var @enum in enums)
            foreach (var enumMember in @enum.Members)
                if (names.Add(enumMember.Name))
                    texts.Add(new LocalizationMember(Name: enumMember.Name, HasDocComment: false, DefaultText: null, Parameters: default));

        return texts.ToImmutable();
    }

    /// <summary>
    /// The enum of each <c>ILocalizationSet&lt;TEnum&gt;</c> a set implements, ordered by full name. An argument that
    /// binds to no enum is left out.
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

            foreach (var member in @enum.GetMembers())
            {
                if (member is not IFieldSymbol { HasConstantValue: true, ConstantValue: { } value })
                    continue;

                members.Add(new LocalizedEnumMember(member.Name, IsFirstOfItsValue: values.Add(value)));
            }

            var holder = FindGetTextOfItsAssembly(@enum, type.ContainingAssembly);
            var isPublic = IsPublic(@enum);

            // None where the enum's assembly has a GetText() this one can call, and internal for an enum of another
            // assembly.
            var getText = holder is not null && (holder.DeclaredAccessibility is Accessibility.Public || holder.ContainingAssembly.GivesAccessTo(type.ContainingAssembly))
                ? GetTextAccessibility.None
                : isPublic && SymbolEqualityComparer.Default.Equals(@enum.ContainingAssembly, type.ContainingAssembly)
                    ? GetTextAccessibility.Public
                    : GetTextAccessibility.Internal;

            enums.Add(new LocalizedEnum(
                Type: TextParameters.ReadType(@enum),
                FullyQualifiedName: fullyQualifiedName,
                Name: @enum.ToDisplayString(CrefNameFormat),
                Namespace: GetNamespaceName(@enum.ContainingNamespace),
                IsPublic: isPublic,
                Members: new EquatableArray<LocalizedEnumMember>(members.ToImmutable()),
                HasTextsOfItsAssembly: holder is not null,
                GetText: getText));
        }

        var ordered = enums.OrderBy(static @enum => @enum.FullyQualifiedName, StringComparer.Ordinal).ToImmutableArray();
        return ordered;

        // The class of another assembly that gives the enum its GetText() (LocalizedMembers), or null.
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

        // Whether the enum and every type that holds it are public.
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
    /// Whether a member has the shape of a text without parameters: a public instance <see cref="string"/> property with
    /// a public getter, that isn't an indexer or an explicit interface implementation.
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
    /// Whether the property or method carries a doc comment. Checked on the trivia, because a project without a
    /// documentation file parses <c>///</c> as a plain comment.
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
    /// The <c>[Obsolete]</c> and <c>[StringSyntax]</c> of a text as the generated member repeats them, or
    /// <see langword="null"/>. The generated file imports <c>System</c>, so <c>[StringSyntax]</c> is written in full.
    /// </summary>
    /// <remarks>
    /// <c>[StringSyntax]</c> keeps the analyzer from reporting a composite format's placeholders in every language
    /// (<c>LOC403</c>). Only its first argument is repeated.
    /// </remarks>
    private static string? ReadMarks(ISymbol text)
    {
        var marks = new StringBuilder();

        foreach (var attribute in text.GetAttributes())
        {
            var name = attribute.AttributeClass switch
            {
                { Name: "ObsoleteAttribute", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } => "Obsolete",
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
