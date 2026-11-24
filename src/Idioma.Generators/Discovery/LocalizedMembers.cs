using System.Globalization;
using System.Runtime.CompilerServices;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads the members marked <c>[Localized]</c>: a property, a field, a member of an enum, or a type, each with the
/// wording its attribute writes per language.
/// </summary>
/// <remarks>
/// <para>
/// The attribute is generated per assembly, from its languages (<see cref="LocalizableTextFactoryGenerator"/>), so it
/// doesn't exist in the compilation a generator sees. An attribute named <c>Localized</c> is taken for it when the
/// build will bind it to the generated one (<see cref="WillBindToGenerated"/>): it binds to nothing here, or to an
/// attribute of that name that only a <see langword="using"/> of the file brings in reach, which the generated one
/// stands before.
/// </para>
/// <para>
/// The wording is read here, as the build runs, and written into the assembly as code
/// (<see cref="LocalizedMembersSource"/>). So nothing reads the attribute at run time, and the attribute itself stays
/// out of the compiled assembly.
/// </para>
/// <para>
/// One part of one generator reads the members (<see cref="LocalizationRegistrationPart"/>), because a generator
/// binds every attribute of that name again on each edit. What it reads holds no location, so that an edit that only moves an
/// attribute leaves every step after it as it was. <c>LocalizedMemberAnalyzer</c> reports a member that gets no text.
/// </para>
/// </remarks>
internal static class LocalizedMembers
{
    /// <summary>
    /// Per enum, the position of each of its members among them, in the order they're written.
    /// </summary>
    // Each marked member of an enum is read by itself and asks for its own position. Counted anew for each, an enum of
    // a thousand members would be walked a thousand times. Held by the enum's symbol, and holding only its own
    // members, so the entry goes when the compilation that made the symbol does.
    private static readonly ConditionalWeakTable<INamedTypeSymbol, Dictionary<ISymbol, int>> EnumOrdinals = new();

    /// <summary>
    /// The name of the generated attribute class.
    /// </summary>
    public const string AttributeTypeName = "LocalizedAttribute";

    /// <summary>
    /// The name the attribute is written by: <c>[Localized(en: "City", nl: "Plaats")]</c>.
    /// </summary>
    public const string AttributeName = "Localized";

    /// <summary>
    /// The name of the extension method that gives a value of an enum its text: <c>status.GetText()</c>. The one a set
    /// of the texts of an enum gets has the same name.
    /// </summary>
    public const string ExtensionName = LocalizationSetsAnalysis.ExtensionName;

    /// <summary>
    /// What the name of the class that holds the extension method of an enum ends in.
    /// </summary>
    public const string ExtensionHolderSuffix = "LocalizedExtensions";

    /// <summary>
    /// The namespace of the class that records an assembly's member texts, which is that of its registrar.
    /// </summary>
    public const string RecorderNamespace = PackageNames.Namespace + ".Generated";

    /// <summary>
    /// The members marked <c>[Localized]</c> in the current compilation, each as its attribute writes it. A member
    /// whose attribute doesn't compile is among them.
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
    /// <param name="context">The generator's context, for the attributes and the assembly's name.</param>
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
    /// The model without the extension method of an enum that a set of the assembly holds the texts of
    /// (<c>ILocalizationSet&lt;TEnum&gt;</c>). The generated class of that set gives the enum its <c>GetText</c>
    /// already, and two would make every call ambiguous (<c>CS0121</c>). The texts of its members are still recorded.
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
    /// The name of the class that records an assembly's member texts: <c>LocalizedMembers_My_App</c> for
    /// <c>My-App</c>. Built from the assembly's name, as its registrar's is, so that a test project that sees the
    /// internals of the library it tests has a class of another name.
    /// </summary>
    public static string GetRecorderName(string? assemblyName)
    {
        var name = $"LocalizedMembers_{CodeEscaping.ToIdentifierPart(assemblyName)}";
        return name;
    }

    /// <summary>
    /// Whether a type is the generated attribute, as an analyzer sees it in the compilation that holds the generated
    /// code: a class of that name in the global namespace, declared in this assembly.
    /// </summary>
    public static bool IsGeneratedAttribute(INamedTypeSymbol? type, IAssemblySymbol assembly)
    {
        var isGeneratedAttribute = type is { Name: AttributeTypeName, Arity: 0, ContainingType: null, ContainingNamespace.IsGlobalNamespace: true }
                                   && SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, assembly);

        return isGeneratedAttribute;
    }

    /// <summary>
    /// Whether a node is an attribute written as <c>Localized</c> or <c>LocalizedAttribute</c>, also behind
    /// <c>global::</c>. All the syntax can tell.
    /// </summary>
    public static bool IsNamedLocalized(SyntaxNode node)
    {
        if (node is not AttributeSyntax attribute)
            return false;

        var name = attribute.Name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            AliasQualifiedNameSyntax { Alias.Identifier.ValueText: "global" } alias => alias.Name.Identifier.ValueText,
            _ => null,
        };

        var isNamedLocalized = name is AttributeName or AttributeTypeName;
        return isNamedLocalized;
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
    /// Whether the build will bind an attribute named <c>Localized</c> to the generated one, as seen from the
    /// compilation a generator sees, which doesn't hold it yet.
    /// </summary>
    /// <remarks>
    /// The generated attribute lies in the global namespace. C# looks for a name in the namespaces and types around
    /// it first, each with the usings written inside it, then in the global namespace, and only then in the usings at
    /// the top of the file. So the generated attribute wins over an attribute of that name that only a using of the
    /// file, or a global using, brings in reach. It loses to one in a namespace or type around the member, and to one
    /// behind a using written inside a namespace. That one is the code's own, and its members are left alone.
    /// </remarks>
    public static bool WillBindToGenerated(AttributeSyntax attribute, SemanticModel model, CancellationToken cancellationToken)
    {
        var symbol = model.GetSymbolInfo(attribute, cancellationToken);

        if (symbol.Symbol is null && symbol.CandidateSymbols.IsEmpty)
            return true;

        if ((symbol.Symbol ?? symbol.CandidateSymbols[0]).ContainingType is not { ContainingType: null, ContainingNamespace: { IsGlobalNamespace: false } otherNamespace })
            return false;

        for (var node = attribute.Parent; node is not null; node = node.Parent)
        {
            if (node is not BaseNamespaceDeclarationSyntax declaration)
                continue;

            // The namespace itself and those it lies in: namespace Shop.Domain looks in Shop.Domain and in Shop.
            for (var around = model.GetDeclaredSymbol(declaration, cancellationToken) as INamespaceSymbol; around is { IsGlobalNamespace: false }; around = around.ContainingNamespace)
                if (SymbolEqualityComparer.Default.Equals(around, otherNamespace))
                    return false;

            foreach (var directive in declaration.Usings)
            {
                if (directive.Alias is { Name.Identifier.ValueText: AttributeName or AttributeTypeName })
                    return false;

                if (directive.Alias is null
                    && directive.Name is { } name
                    && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, cancellationToken).Symbol, otherNamespace))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// The members an attribute marks: a type, a property, a member of an enum, each field of a declaration of
    /// fields, or the property a record's parameter declares. Empty for an attribute on anything else, such as an
    /// indexer, or the field behind a property.
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
    /// Why generated code can't name the type of a marked member, or the marked type itself, with what to do about
    /// it, or <see langword="null"/> when it can. The code lies in a file of its own at the top level of the assembly.
    /// </summary>
    public static UnnameableType? DescribeUnnameable(ISymbol marked)
    {
        var type = marked as INamedTypeSymbol ?? marked.ContainingType;

        var unnameable = type is null ? null : DescribeUnnameable(GetChain(type));
        return unnameable;
    }

    /// <summary>
    /// The members an attribute marks, as the generator works with them.
    /// </summary>
    private static EquatableArray<LocalizedMember> ReadMembers(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var attribute = (AttributeSyntax)context.Node;

        if (!WillBindToGenerated(attribute, context.SemanticModel, cancellationToken))
            return default;

        var symbols = FindMarked(attribute, context.SemanticModel, cancellationToken);

        if (symbols.Length is 0)
            return default;

        var wordings = ReadWordings(attribute, context.SemanticModel, cancellationToken);
        var members = ImmutableArray.CreateBuilder<LocalizedMember>(symbols.Length);

        foreach (var symbol in symbols)
            if (ReadMember(symbol, wordings) is { } member)
                members.Add(member);

        return new EquatableArray<LocalizedMember>(members.ToImmutable());
    }

    /// <summary>
    /// The arguments of the attribute, each with the language parameter it names and the constant it holds.
    /// </summary>
    private static EquatableArray<LocalizedArgument> ReadWordings(AttributeSyntax attribute, SemanticModel model, CancellationToken cancellationToken)
    {
        if (attribute.ArgumentList is not { } arguments)
            return default;

        var wordings = ImmutableArray.CreateBuilder<LocalizedArgument>(arguments.Arguments.Count);

        foreach (var argument in arguments.Arguments)
        {
            // The attribute has no properties, so an argument written as "name = value" compiles to nothing.
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

        // Without the @ of an escaped keyword (namespace Shop.@event), as a set's key: a key is a name, not code.
        var keyNamespace = type.ContainingNamespace is { IsGlobalNamespace: false } ? type.ContainingNamespace.ToDisplayString().Replace("@", "") + "." : "";
        var prefix = @namespace.Length is 0 ? "" : @namespace + ".";
        var isInGenericType = chain.Any(static part => part.Arity > 0);

        // As code names the type: Shop.Domain.Page<> for a typeof, where a type with type parameters has no other name.
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
                IsPublic: chain.All(static part => part.DeclaredAccessibility is Accessibility.Public),
                CanExtend: !isInGenericType,
                Ordinal: field is null ? -1 : GetOrdinal(type, field),
                Value: field is { HasConstantValue: true } ? Convert.ToString(field.ConstantValue, CultureInfo.InvariantCulture) : null,
                IsObsoleteAsError: field is not null && LocalizationDiscovery.IsObsoleteAsError(field));
        }

        var member = new LocalizedMember(
            TypeReference: reference,
            TypeKey: keyNamespace + String.Join(".", chain.Select(static part => part.MetadataName)),
            TypeName: type.Name,
            MemberName: isMember ? symbol.Name : null,
            Wordings: wordings,
            IsNameable: DescribeUnnameable(chain) is null,
            SuppressedDiagnostics: new EquatableArray<string>(GetSuppressedDiagnostics(symbol, chain)),
            Enum: enumMember);

        return member;
    }

    /// <summary>
    /// The position of a member of an enum among its members, in the order they're written, or -1 for a field that
    /// isn't one of them.
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

    /// <summary>
    /// The diagnostics that the generated code of a member reports by naming what its author marked
    /// <c>[Experimental]</c>, or <c>[Obsolete]</c> with an id of its own: the member's type, a type around it, or the
    /// member itself where it's a member of an enum, which <c>GetText()</c> names. The generated file turns them off.
    /// </summary>
    private static ImmutableArray<string> GetSuppressedDiagnostics(ISymbol symbol, List<INamedTypeSymbol> chain)
    {
        var suppressed = chain.SelectMany(static part => LocalizationDiscovery.GetSuppressedDiagnostics(part));

        if (symbol is IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum })
            suppressed = suppressed.Concat(LocalizationDiscovery.GetSuppressedDiagnostics(symbol));

        return [.. suppressed.Distinct(StringComparer.Ordinal)];
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

            // Naming it is an error (CS0619), which no pragma turns off.
            if (LocalizationDiscovery.IsObsoleteAsError(part))
                return new UnnameableType(part.Name, "marked [Obsolete] as an error", $"Make the [Obsolete] of '{part.Name}' a warning, or remove [Localized]");
        }

        return null;
    }

    private static LocalizedMembersModel BuildModel(EquatableArray<LocalizedMember> members, EquatableArray<string> codes, string? assemblyName)
    {
        var recorderName = GetRecorderName(assemblyName);

        if (codes.Length is 0 || members.Length is 0)
            return new LocalizedMembersModel(recorderName, codes, default, default);

        var types = new SortedDictionary<string, List<(LocalizedMember Member, EquatableArray<string> Wordings)>>(StringComparer.Ordinal);
        var seen = new HashSet<(string Type, string? Member)>();
        var suppressed = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var member in members)
        {
            if (!member.IsNameable || ResolveWordings(member, codes) is not { } wordings || !seen.Add((member.TypeKey, member.MemberName)))
                continue;

            suppressed.UnionWith(member.SuppressedDiagnostics);

            if (!types.TryGetValue(member.TypeKey, out var texts))
                types.Add(member.TypeKey, texts = []);

            texts.Add((member, wordings));
        }

        var model = ImmutableArray.CreateBuilder<LocalizedType>(types.Count);

        foreach (var type in types.Values)
        {
            var first = type[0].Member;

            // The type's own text first, then the members of an enum in the order it declares them, and the members of
            // any other type by name: an order that doesn't depend on which file of a partial type comes first.
            var texts = type
                .OrderBy(static text => text.Member.MemberName is null ? 0 : 1)
                .ThenBy(static text => text.Member.Enum?.Ordinal ?? 0)
                .ThenBy(static text => text.Member.MemberName, StringComparer.Ordinal)
                .Select(static text => new LocalizedText(text.Member.MemberName, text.Wordings, text.Member.Enum?.Value, text.Member.Enum?.IsObsoleteAsError ?? false))
                .ToImmutableArray();

            var extension = first.Enum is { CanExtend: true } @enum && texts.Any(static text => text.MemberName is not null)
                ? new LocalizedEnumExtension(@enum.Namespace, @enum.Name, @enum.HolderName, @enum.IsPublic)
                : (LocalizedEnumExtension?)null;

            model.Add(new LocalizedType(first.TypeReference, first.TypeKey, first.TypeName, new EquatableArray<LocalizedText>(texts), extension));
        }

        return new LocalizedMembersModel(recorderName, codes, new EquatableArray<LocalizedType>(model.ToImmutable()), new EquatableArray<string>([.. suppressed]));
    }

    /// <summary>
    /// The wording of a member for each language, in the order of <paramref name="codes"/>, or <see langword="null"/>
    /// when its attribute doesn't give every language a wording. That's an attribute that doesn't compile, as one
    /// that leaves a language out, or one that passes <see langword="null"/>.
    /// </summary>
    private static EquatableArray<string>? ResolveWordings(LocalizedMember member, EquatableArray<string> codes)
    {
        var wordings = new string?[codes.Length];

        for (var i = 0; i < member.Wordings.Length; i++)
        {
            var argument = member.Wordings[i];

            // An argument without a name takes the parameter of its place, as the compiler has it (LOC500 reports it).
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
    /// The language a parameter of the generated attribute stands for, as its place among the languages, or -1 for a
    /// name that is no parameter.
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
/// A member marked <c>[Localized]</c>, as its attribute writes it: the type that declares it, its name, or
/// <see langword="null"/> for the type's own text, and its arguments.
/// </summary>
/// <param name="TypeReference">The type as code at the top level of the assembly names it, such as <c>Shop.Domain.Page&lt;&gt;</c>.</param>
/// <param name="TypeKey">The full name of the type as the key of a text writes it, such as <c>Shop.Domain.Page`1</c>.</param>
/// <param name="TypeName">The name of the type, such as <c>Page</c>.</param>
/// <param name="MemberName">The name of the member, or <see langword="null"/> for the type's own text.</param>
/// <param name="Wordings">The arguments of the attribute, in the order they're written.</param>
/// <param name="IsNameable">Whether generated code can name the type.</param>
/// <param name="SuppressedDiagnostics">
/// What naming the type, or a member of an enum, reports besides <c>CS0612</c> and <c>CS0618</c>: the id of
/// <c>[Experimental]</c>, and the <c>DiagnosticId</c> of <c>[Obsolete]</c>.
/// </param>
/// <param name="Enum">What the extension method of an enum needs, for a member of an enum or an enum's own text.</param>
internal readonly record struct LocalizedMember(
    string TypeReference,
    string TypeKey,
    string TypeName,
    string? MemberName,
    EquatableArray<LocalizedArgument> Wordings,
    bool IsNameable,
    EquatableArray<string> SuppressedDiagnostics,
    LocalizedMemberEnum? Enum);

/// <summary>
/// An argument of <c>[Localized]</c>: the language parameter it names, or <see langword="null"/> for one without a
/// name, and the wording it holds. The wording is <see langword="null"/> for an argument that is no constant, and for
/// the constant <see langword="null"/>.
/// </summary>
internal readonly record struct LocalizedArgument(string? Parameter, string? Wording);

/// <summary>
/// A type generated code can't name, what kind of type that makes it, such as <c>private</c>, and what to do about it.
/// </summary>
internal readonly record struct UnnameableType(string Name, string Kind, string Advice);

/// <summary>
/// What the extension method of an enum needs to know about a member of it, or about the enum's own text.
/// </summary>
/// <param name="Namespace">The namespace of the enum, or empty for the global namespace.</param>
/// <param name="Name">The enum as code in its namespace names it, such as <c>Order.Status</c>.</param>
/// <param name="HolderName">The name of the class that holds the extension method.</param>
/// <param name="IsPublic">Whether code outside the assembly can name the enum.</param>
/// <param name="CanExtend">Whether an extension method can take the enum: not one inside a type with type parameters.</param>
/// <param name="Ordinal">The place of the member among the members of its enum, or -1 for the enum's own text.</param>
/// <param name="Value">The number of the member, or <see langword="null"/> where it has none to read.</param>
/// <param name="IsObsoleteAsError">
/// Whether the member is marked <c>[Obsolete]</c> as an error. Generated code can't name such a member, so
/// <c>GetText()</c> finds it by its <paramref name="Value"/>.
/// </param>
internal readonly record struct LocalizedMemberEnum(
    string Namespace,
    string Name,
    string HolderName,
    bool IsPublic,
    bool CanExtend,
    int Ordinal,
    string? Value,
    bool IsObsoleteAsError);

/// <summary>
/// The texts to generate for the members of an assembly: the name of the class that records them, the languages, the
/// types with their texts, in the order of their full names, and what naming those types and members reports besides
/// <c>CS0612</c> and <c>CS0618</c>, in the order of the ids (<see cref="LocalizedMember.SuppressedDiagnostics"/>).
/// </summary>
internal readonly record struct LocalizedMembersModel(
    string RecorderName,
    EquatableArray<string> Codes,
    EquatableArray<LocalizedType> Types,
    EquatableArray<string> SuppressedDiagnostics)
{
    /// <summary>
    /// Whether the assembly has a text to record, and so gets the class that records them.
    /// </summary>
    public bool HasTexts => this.Types.Length > 0;
}

/// <summary>
/// A type with texts: how code names it, its full name for the keys, its own name, its texts, and the extension
/// method to write for an enum.
/// </summary>
internal readonly record struct LocalizedType(
    string Reference,
    string Key,
    string Name,
    EquatableArray<LocalizedText> Texts,
    LocalizedEnumExtension? Extension);

/// <summary>
/// A text of a member: the member's name, or <see langword="null"/> for the type's own text, its wording per language
/// in the order of the assembly's languages, the number of a member of an enum, and whether that member is marked
/// <c>[Obsolete]</c> as an error, so that generated code names its number instead of the member.
/// </summary>
internal readonly record struct LocalizedText(string? MemberName, EquatableArray<string> Wordings, string? EnumValue, bool IsObsoleteAsError);

/// <summary>
/// The extension method that gives a value of an enum its text: where to write it, how to name the enum there, the
/// class that holds it, and whether it's public.
/// </summary>
internal readonly record struct LocalizedEnumExtension(string Namespace, string EnumName, string HolderName, bool IsPublic);
