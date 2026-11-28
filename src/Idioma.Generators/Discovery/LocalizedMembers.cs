using System.Globalization;
using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads the members marked <c>[Localized]</c>: a property, a field, an enum member or a type, each with its wording per
/// language.
/// </summary>
/// <remarks>
/// The attribute is generated per assembly (<see cref="LocalizedTextFactoryGenerator"/>), so it doesn't exist yet when a
/// generator runs (<see cref="WillBindToGenerated"/>). The wording is written into the assembly as code
/// (<see cref="LocalizedMembersSource"/>), so nothing reads the attribute at run time. Only
/// <see cref="LocalizationRegistrationPart"/> reads the members, and without locations, so moving an attribute leaves
/// the later steps as they were.
/// </remarks>
internal static class LocalizedMembers
{
    /// <summary>
    /// Per enum, the position of each of its members, in the order they're written.
    /// </summary>
    // Cached, because each marked member asks for its own position. Held by the enum's symbol, so it goes with the
    // compilation.
    private static readonly ConditionalWeakTable<INamedTypeSymbol, Dictionary<ISymbol, int>> EnumOrdinals = new();

    public const string AttributeTypeName = "LocalizedAttribute";

    /// <summary>
    /// The name the attribute is written by: <c>[Localized(en: "City", nl: "Plaats")]</c>.
    /// </summary>
    public const string AttributeName = "Localized";

    /// <summary>
    /// The name of the extension method that gives an enum value its text: <c>status.GetText()</c>.
    /// </summary>
    public const string ExtensionName = LocalizationSetsAnalysis.ExtensionName;

    /// <summary>
    /// The suffix of the class that holds an enum's extension method.
    /// </summary>
    public const string ExtensionHolderSuffix = "LocalizedExtensions";

    /// <summary>
    /// The namespace of the class that registers an assembly's member texts, which is that of its registrar.
    /// </summary>
    public const string MemberTextsNamespace = PackageNames.Namespace + ".Generated";

    /// <summary>
    /// The members marked <c>[Localized]</c> in the current compilation, also those whose attribute doesn't compile.
    /// </summary>
    private static IncrementalValueProvider<EquatableArray<LocalizedMember>> Read(IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => IsNamedLocalized(node), static (context, cancellationToken) => ReadMembers(context, cancellationToken))
            .SelectMany(static (members, _) => members.AsImmutableArray())
            .Collect()
            .Select(static (members, _) => new EquatableArray<LocalizedMember>(members));
    }

    /// <summary>
    /// The texts to generate: every member whose attribute gives a wording for each language of the assembly, grouped
    /// by type.
    /// </summary>
    /// <param name="languages">The languages the assembly declares (<see cref="SupportedLanguages.Read"/>).</param>
    public static IncrementalValueProvider<LocalizedMembersModel> ReadModel(IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<DeclaredLanguages?> languages)
    {
        var codes = languages.Select(static (languages, _) => languages?.Codes ?? default);
        var assemblyName = context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName);

        return Read(context)
            .Combine(codes)
            .Combine(assemblyName)
            .Select(static (data, _) => BuildModel(data.Left.Left, data.Left.Right, data.Right));
    }

    /// <summary>
    /// The model without the extension method of an enum that a set of the assembly lists
    /// (<c>ILocalizationSet&lt;TEnum&gt;</c>), because that set's generated class gives it a <c>GetText</c> already
    /// (<c>CS0121</c>). The texts of its members are still registered.
    /// </summary>
    public static LocalizedMembersModel WithoutExtensionsOf(LocalizedMembersModel model, EquatableArray<string> enumsWithSets)
    {
        if (enumsWithSets.Length is 0 || !model.Types.Any(type => type.Extension is not null && enumsWithSets.AsImmutableArray().Contains(type.Reference, StringComparer.Ordinal)))
            return model;

        var types = model.Types
            .Select(type => type.Extension is not null && enumsWithSets.AsImmutableArray().Contains(type.Reference, StringComparer.Ordinal) ? type with { Extension = null } : type)
            .ToImmutableArray();

        return model with { Types = new EquatableArray<LocalizedType>(types) };
    }

    /// <summary>
    /// The name of the class that registers an assembly's member texts: <c>LocalizedMembers_My_App</c> for
    /// <c>My-App</c>. Built from the assembly's name, so a test project that sees a library's internals has another one.
    /// </summary>
    public static string GetMemberTextsClassName(string? assemblyName)
    {
        var name = $"LocalizedMembers_{CodeEscaping.ToIdentifierPart(assemblyName)}";
        return name;
    }

    /// <summary>
    /// Whether a type is the generated attribute, as an analyzer sees it: a class of that name in
    /// <c>CodeChops.Idioma</c>, declared in this assembly.
    /// </summary>
    public static bool IsGeneratedAttribute(INamedTypeSymbol? type, IAssemblySymbol assembly)
    {
        var isGeneratedAttribute = type is { Name: AttributeTypeName, Arity: 0, ContainingType: null }
                                   && IsPackageNamespace(type.ContainingNamespace)
                                   && SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, assembly);

        return isGeneratedAttribute;
    }

    private static bool IsPackageNamespace(INamespaceSymbol? @namespace)
    {
        var isPackageNamespace = @namespace is { Name: "Idioma", ContainingNamespace: { Name: "CodeChops", ContainingNamespace.IsGlobalNamespace: true } };
        return isPackageNamespace;
    }

    /// <summary>
    /// Whether a node is an attribute written as <c>Localized</c> or <c>LocalizedAttribute</c>, alone or behind
    /// <c>CodeChops.Idioma</c>, as far as the syntax tells.
    /// </summary>
    public static bool IsNamedLocalized(SyntaxNode node)
    {
        if (node is not AttributeSyntax attribute)
            return false;

        var name = attribute.Name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax { Right: IdentifierNameSyntax right } qualified when IsPackageNamespaceName(qualified.Left) => right.Identifier.ValueText,
            _ => null,
        };

        var isNamedLocalized = name is AttributeName or AttributeTypeName;
        return isNamedLocalized;

        static bool IsPackageNamespaceName(NameSyntax name) => name.ToString() is "CodeChops.Idioma" or "global::CodeChops.Idioma";
    }

    /// <summary>
    /// Whether an attribute named <c>Localized</c> binds to nothing in the compilation a generator sees.
    /// </summary>
    public static bool IsUnbound(AttributeSyntax attribute, SemanticModel model, CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(attribute, cancellationToken);

        var isUnbound = symbol.Symbol is null && symbol.CandidateSymbols.IsEmpty;
        return isUnbound;
    }

    /// <summary>
    /// Whether the build will bind an attribute named <c>Localized</c> to the generated one, which doesn't exist yet.
    /// </summary>
    /// <remarks>
    /// An attribute that binds to nothing here counts. Without a using of <c>CodeChops.Idioma</c>, the compiler then
    /// reports <c>CS0246</c>. One that binds to another namespace's attribute counts only when <c>CodeChops.Idioma</c>
    /// is found first, from the inside out. Where both are found at the same level, it's <c>CS0104</c>, and it doesn't
    /// count.
    /// </remarks>
    public static bool WillBindToGenerated(AttributeSyntax attribute, SemanticModel model, CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(attribute, cancellationToken);

        if (symbol.Symbol is null && symbol.CandidateSymbols.IsEmpty)
            return true;

        // A qualified name names a type that exists already, which the generated one doesn't.
        if (attribute.Name is not IdentifierNameSyntax
            || (symbol.Symbol ?? symbol.CandidateSymbols[0]).ContainingType is not { ContainingType: null, ContainingNamespace: { IsGlobalNamespace: false } otherNamespace })
        {
            return false;
        }

        INamespaceSymbol? declared = null;

        for (var node = attribute.Parent; node is not null; node = node.Parent)
        {
            if (node is not BaseNamespaceDeclarationSyntax declaration)
                continue;

            // Innermost first: namespace Shop.Domain looks in Shop.Domain, then its usings, then Shop.
            declared ??= model.GetDeclaredSymbol(declaration, cancellationToken) as INamespaceSymbol;
            var outer = node.Parent?.AncestorsAndSelf().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault() is { } outerDeclaration
                ? model.GetDeclaredSymbol(outerDeclaration, cancellationToken) as INamespaceSymbol
                : null;

            if (Finds(declared) is { } found)
                return found;

            foreach (var directive in declaration.Usings)
            {
                // An alias of that name wins over both.
                if (directive.Alias is { Name.Identifier.ValueText: AttributeName or AttributeTypeName })
                    return false;
            }

            var importsGenerated = false;
            var importsOther = false;

            foreach (var directive in declaration.Usings)
            {
                if (directive.Alias is not null || !directive.StaticKeyword.IsKind(SyntaxKind.None) || directive.Name is not { } name)
                    continue;

                var imported = model.GetSymbolInfo(name, cancellationToken).Symbol as INamespaceSymbol;
                importsGenerated |= IsPackageNamespace(imported);
                importsOther |= SymbolEqualityComparer.Default.Equals(imported, otherNamespace);
            }

            if (importsGenerated || importsOther)
                return importsGenerated && !importsOther;

            // The outer parts of a dotted namespace name, up to the next declaration.
            for (var around = declared?.ContainingNamespace; around is { IsGlobalNamespace: false } && !SymbolEqualityComparer.Default.Equals(around, outer); around = around.ContainingNamespace)
                if (Finds(around) is { } foundAround)
                    return foundAround;

            declared = outer;
        }

        // At the top of the file the other one is in reach, and the generated one at most beside it.
        return false;

        // Whether a namespace holds the generated attribute (true) or the other one (false), or neither (null).
        bool? Finds(INamespaceSymbol? @namespace) =>
            IsPackageNamespace(@namespace) ? true
            : SymbolEqualityComparer.Default.Equals(@namespace, otherNamespace) ? false
            : null;
    }

    /// <summary>
    /// The members an attribute marks: a type, a property, an enum member, each field of a field declaration, or the
    /// property a record's parameter declares. Empty for anything else.
    /// </summary>
    public static ImmutableArray<ISymbol> FindMarked(AttributeSyntax attribute, SemanticModel model, CancellationToken cancellationToken)
    {
        if (attribute.Parent is not AttributeListSyntax list)
            return ImmutableArray<ISymbol>.Empty;

        var target = list.Target?.Identifier.ValueText;
        var symbols = ImmutableArray.CreateBuilder<ISymbol>();

        switch (list.Parent)
        {
            case BaseTypeDeclarationSyntax type when target is null or "type":
                AddDeclared(type);
                break;

            case PropertyDeclarationSyntax property when target is null or "property":
                AddDeclared(property);
                break;

            case EnumMemberDeclarationSyntax member when target is null or "field":
                AddDeclared(member);
                break;

            case FieldDeclarationSyntax field when target is null or "field":
                foreach (var variable in field.Declaration.Variables)
                    AddDeclared(variable);

                break;

            // The property of a record, which its parameter declares: record Line([property: Localized(...)] int Quantity).
            case ParameterSyntax parameter when target is "property":
                if (model.GetDeclaredSymbol(parameter, cancellationToken) is { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor, ContainingType: { IsRecord: true } record } } declared
                    && record.GetMembers(declared.Name).OfType<IPropertySymbol>().FirstOrDefault() is { } recordProperty)
                {
                    symbols.Add(recordProperty);
                }

                break;
        }

        return symbols.ToImmutable();

        void AddDeclared(SyntaxNode declaration)
        {
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is { } declared)
                symbols.Add(declared);
        }
    }

    /// <summary>
    /// Why generated code at the top level can't name the type of a marked member, or the marked type, with advice, or
    /// <see langword="null"/>.
    /// </summary>
    public static UnnameableType? DescribeUnnameable(ISymbol marked)
    {
        var type = marked as INamedTypeSymbol ?? marked.ContainingType;

        var unnameable = type is null ? null : DescribeUnnameable(GetChain(type));
        return unnameable;
    }

    // Given again where no declaration changed (TransformCache), because binding every attribute on every edit cost about
    // a tenth of an edit.
    private static EquatableArray<LocalizedMember> ReadMembers(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var attribute = (AttributeSyntax)context.Node;

        return TransformCache.GetOrRead(attribute, context.SemanticModel.Compilation, (attribute, context.SemanticModel),
            static (state, cancellationToken) => ReadMembers(state.attribute, state.SemanticModel, cancellationToken), cancellationToken);
    }

    private static EquatableArray<LocalizedMember> ReadMembers(AttributeSyntax attribute, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (!WillBindToGenerated(attribute, semanticModel, cancellationToken))
            return default;

        var symbols = FindMarked(attribute, semanticModel, cancellationToken);

        if (symbols.Length is 0)
            return default;

        var wordings = ReadWordings(attribute, semanticModel, cancellationToken);
        var members = ImmutableArray.CreateBuilder<LocalizedMember>(symbols.Length);

        foreach (var symbol in symbols)
            if (ReadMember(symbol, wordings) is { } member)
                members.Add(member);

        return new EquatableArray<LocalizedMember>(members.ToImmutable());
    }

    private static EquatableArray<LocalizedArgument> ReadWordings(AttributeSyntax attribute, SemanticModel model, CancellationToken cancellationToken)
    {
        if (attribute.ArgumentList is not { } arguments)
            return default;

        var wordings = ImmutableArray.CreateBuilder<LocalizedArgument>(arguments.Arguments.Count);

        foreach (var argument in arguments.Arguments)
        {
            // The attribute has no properties, so "name = value" doesn't compile.
            var constant = argument.NameEquals is null ? model.GetConstantValue(argument.Expression, cancellationToken) : default;

            wordings.Add(new LocalizedArgument(
                Parameter: argument.NameColon?.Name.Identifier.ValueText,
                Wording: constant is { HasValue: true, Value: string wording } ? wording : null));
        }

        return new EquatableArray<LocalizedArgument>(wordings.ToImmutable());
    }

    /// <summary>
    /// A type and the types it's nested in, the outermost first.
    /// </summary>
    private static List<INamedTypeSymbol> GetChain(INamedTypeSymbol type)
    {
        var chain = new List<INamedTypeSymbol>();

        for (var current = type; current is not null; current = current.ContainingType)
            chain.Insert(0, current);

        return chain;
    }

    private static LocalizedMember? ReadMember(ISymbol symbol, EquatableArray<LocalizedArgument> wordings)
    {
        var type = symbol as INamedTypeSymbol ?? symbol.ContainingType;

        if (type is null)
            return null;

        var chain = GetChain(type);

        var @namespace = type.ContainingNamespace is { IsGlobalNamespace: false } containing
            ? containing.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted))
            : "";

        // Without the @ of an escaped keyword, because a key is a name, not code.
        var keyNamespace = type.ContainingNamespace is { IsGlobalNamespace: false } ? type.ContainingNamespace.ToDisplayString().Replace("@", "") + "." : "";
        var prefix = @namespace.Length is 0 ? "" : @namespace + ".";
        var isInGenericType = chain.Any(static part => part.Arity > 0);

        // As a typeof names it: Shop.Domain.Page<> for a generic type.
        var reference = prefix + String.Join(".", chain.Select(static part =>
            CodeEscaping.EscapeIdentifier(part.Name) + (part.Arity > 0 ? $"<{new string(',', part.Arity - 1)}>" : "")));

        var isMember = symbol is not INamedTypeSymbol;

        LocalizedMemberEnum? enumMember = null;

        if (type.TypeKind is TypeKind.Enum)
        {
            var field = symbol as IFieldSymbol;

            enumMember = new LocalizedMemberEnum(
                Namespace: @namespace,
                Name: String.Join(".", chain.Select(static part => CodeEscaping.EscapeIdentifier(part.Name))),
                HolderName: String.Join("_", chain.Select(static part => part.Name)) + ExtensionHolderSuffix,
                IsPublic: chain.TrueForAll(static part => part.DeclaredAccessibility is Accessibility.Public),
                CanExtend: !isInGenericType,
                Ordinal: field is null ? -1 : GetOrdinal(type, field),
                Value: field is { HasConstantValue: true } ? Convert.ToString(field.ConstantValue, CultureInfo.InvariantCulture) : null);
        }

        var member = new LocalizedMember(
            TypeReference: reference,
            TypeKey: keyNamespace + String.Join(".", chain.Select(static part => part.MetadataName)),
            MemberName: isMember ? symbol.Name : null,
            Wordings: wordings,
            IsNameable: DescribeUnnameable(chain) is null,
            Enum: enumMember);

        return member;
    }

    /// <summary>
    /// The position of an enum member in the order they're written, or -1.
    /// </summary>
    private static int GetOrdinal(INamedTypeSymbol @enum, IFieldSymbol field)
    {
        var ordinals = EnumOrdinals.GetValue(@enum, static @enum =>
        {
            var ordinals = new Dictionary<ISymbol, int>(SymbolEqualityComparer.Default);

            foreach (var member in @enum.GetMembers())
                if (member is IFieldSymbol && !ordinals.ContainsKey(member))
                    ordinals.Add(member, ordinals.Count);

            return ordinals;
        });

        var ordinal = ordinals.TryGetValue(field, out var found) ? found : -1;
        return ordinal;
    }

    private static UnnameableType? DescribeUnnameable(List<INamedTypeSymbol> chain)
    {
        foreach (var part in chain)
        {
            if (part.IsFileLocal)
                return new UnnameableType(part.Name, "file-local", $"Remove 'file' from '{part.Name}'");

            var kind = part.DeclaredAccessibility switch
            {
                Accessibility.Private => "private",
                Accessibility.Protected => "protected",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => null,
            };

            if (kind is not null)
                return new UnnameableType(part.Name, kind, $"Make '{part.Name}' internal or public");
        }

        return null;
    }

    private static LocalizedMembersModel BuildModel(EquatableArray<LocalizedMember> members, EquatableArray<string> codes, string? assemblyName)
    {
        var memberTextsClassName = GetMemberTextsClassName(assemblyName);

        if (codes.Length is 0 || members.Length is 0)
            return new LocalizedMembersModel(memberTextsClassName, codes, default);

        var types = new SortedDictionary<string, List<(LocalizedMember Member, EquatableArray<string> Wordings)>>(StringComparer.Ordinal);
        var seen = new HashSet<(string Type, string? Member)>();

        foreach (var member in members)
        {
            if (!member.IsNameable || ResolveWordings(member, codes) is not { } wordings || !seen.Add((member.TypeKey, member.MemberName)))
                continue;

            if (!types.TryGetValue(member.TypeKey, out var texts))
                types.Add(member.TypeKey, texts = []);

            texts.Add((member, wordings));
        }

        var model = ImmutableArray.CreateBuilder<LocalizedType>(types.Count);

        foreach (var type in types.Values)
        {
            var first = type[0].Member;

            // The type's own text first, then enum members in declared order, and other members by name. That order
            // doesn't depend on which file of a partial type comes first.
            var texts = type
                .OrderBy(static text => text.Member.MemberName is null ? 0 : 1)
                .ThenBy(static text => text.Member.Enum?.Ordinal ?? 0)
                .ThenBy(static text => text.Member.MemberName, StringComparer.Ordinal)
                .Select(static text => new LocalizedText(text.Member.MemberName, text.Wordings, text.Member.Enum?.Value))
                .ToImmutableArray();

            var extension = first.Enum is { CanExtend: true } @enum && texts.Any(static text => text.MemberName is not null)
                ? new LocalizedEnumExtension(@enum.Namespace, @enum.Name, @enum.HolderName, @enum.IsPublic)
                : (LocalizedEnumExtension?)null;

            model.Add(new LocalizedType(first.TypeReference, new EquatableArray<LocalizedText>(texts), extension));
        }

        return new LocalizedMembersModel(memberTextsClassName, codes, new EquatableArray<LocalizedType>(model.ToImmutable()));
    }

    /// <summary>
    /// The wording of a member for each language, in the order of <paramref name="codes"/>, or <see langword="null"/>
    /// when its attribute doesn't compile, such as one that leaves a language out.
    /// </summary>
    private static EquatableArray<string>? ResolveWordings(LocalizedMember member, EquatableArray<string> codes)
    {
        var wordings = new string?[codes.Length];

        for (var i = 0; i < member.Wordings.Length; i++)
        {
            var argument = member.Wordings[i];

            // An argument without a name takes the parameter of its place (LOC500).
            var index = argument.Parameter is null ? i : FindParameter(codes, argument.Parameter);

            if (index < 0 || index >= codes.Length || wordings[index] is not null || argument.Wording is null)
                return null;

            wordings[index] = argument.Wording;
        }

        if (wordings.Any(static wording => wording is null))
            return null;

        return new EquatableArray<string>(wordings.Select(static wording => wording!).ToImmutableArray());
    }

    /// <summary>
    /// The index of the language a parameter of the generated attribute stands for, or -1.
    /// </summary>
    private static int FindParameter(EquatableArray<string> codes, string parameter)
    {
        // A name in the syntax comes without the @ that escapes a keyword.
        for (var i = 0; i < codes.Length; i++)
            if (String.Equals(SupportedLanguages.ToParameterName(codes[i]).TrimStart('@'), parameter, StringComparison.Ordinal))
                return i;

        return -1;
    }
}

/// <summary>
/// A member marked <c>[Localized]</c>, as its attribute writes it.
/// </summary>
/// <param name="TypeReference">The type as top-level code names it, such as <c>Shop.Domain.Page&lt;&gt;</c>.</param>
/// <param name="TypeKey">The type's full name as a text's key writes it, such as <c>Shop.Domain.Page`1</c>.</param>
/// <param name="MemberName">The name of the member, or <see langword="null"/> for the type's own text.</param>
/// <param name="Wordings">The arguments of the attribute, in the order they're written.</param>
/// <param name="Enum">What an enum's extension method needs, for an enum member or an enum's own text.</param>
internal readonly record struct LocalizedMember(
    string TypeReference,
    string TypeKey,
    string? MemberName,
    EquatableArray<LocalizedArgument> Wordings,
    bool IsNameable,
    LocalizedMemberEnum? Enum);

/// <summary>
/// An argument of <c>[Localized]</c>. <see cref="Parameter"/> is <see langword="null"/> for an argument without a name,
/// and <see cref="Wording"/> for one that isn't a non-null constant.
/// </summary>
internal readonly record struct LocalizedArgument(string? Parameter, string? Wording);

/// <summary>
/// A type generated code can't name, its kind, such as <c>private</c>, and advice.
/// </summary>
internal readonly record struct UnnameableType(string Name, string Kind, string Advice);

/// <summary>
/// What an enum's extension method needs to know about a member of it, or about the enum's own text.
/// </summary>
/// <param name="Name">The enum as code in its namespace names it, such as <c>Order.Status</c>.</param>
/// <param name="CanExtend">False for an enum inside a generic type.</param>
/// <param name="Ordinal">The place of the member in its enum, or -1 for the enum's own text.</param>
internal readonly record struct LocalizedMemberEnum(
    string Namespace,
    string Name,
    string HolderName,
    bool IsPublic,
    bool CanExtend,
    int Ordinal,
    string? Value);

/// <summary>
/// The texts to generate for the members of an assembly. <see cref="Types"/> are ordered by full name.
/// </summary>
internal readonly record struct LocalizedMembersModel(
    string MemberTextsClassName,
    EquatableArray<string> Codes,
    EquatableArray<LocalizedType> Types)
{
    /// <summary>
    /// Whether the assembly has a text to register, and so gets the class that registers them.
    /// </summary>
    public bool HasTexts => this.Types.Length > 0;
}

/// <summary>
/// A type with texts, and the extension method to write for an enum.
/// </summary>
internal readonly record struct LocalizedType(
    string Reference,
    EquatableArray<LocalizedText> Texts,
    LocalizedEnumExtension? Extension);

/// <summary>
/// A text of a member, or of the type when <see cref="MemberName"/> is <see langword="null"/>. <see cref="Wordings"/>
/// follow the assembly's languages. <see cref="EnumValue"/> is an enum member's value as a literal, so members that
/// share a value get one arm.
/// </summary>
internal readonly record struct LocalizedText(string? MemberName, EquatableArray<string> Wordings, string? EnumValue);

/// <summary>
/// The extension method that gives an enum value its text.
/// </summary>
internal readonly record struct LocalizedEnumExtension(string Namespace, string EnumName, string HolderName, bool IsPublic);
