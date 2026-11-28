namespace CodeChops.Idioma.Generators;

// Keeps the base types a set lists that bind to nothing yet, such as a contract generated in the same compilation, and
// asks the compiler which generated contract each binds to where the name alone can't tell.
/// <inheritdoc cref="LocalizationDiscovery"/>
internal static partial class LocalizationDiscovery
{
    /// <summary>
    /// The base-list entries of this declaration that bind to no type, as a contract another generator writes does.
    /// Where each is written is added to <paramref name="locations"/>.
    /// </summary>
    /// <param name="typeName">The name of the class, as a site names it.</param>
    private static ImmutableArray<UnresolvedBaseType> GetUnresolvedBaseTypes(INamedTypeSymbol type, string typeName,
        TypeDeclarationSyntax declaration, SemanticModel semanticModel, ImmutableArray<BaseTypeLocation>.Builder locations,
        CancellationToken cancellationToken)
    {
        if (declaration.BaseList is null)
            return ImmutableArray<UnresolvedBaseType>.Empty;

        var types = ImmutableArray.CreateBuilder<UnresolvedBaseType>();
        var declarationIndex = -1;
        var baseTypes = declaration.BaseList.Types;

        for (var index = 0; index < baseTypes.Count; index++)
        {
            var baseType = baseTypes[index].Type;

            if (semanticModel.GetTypeInfo(baseType, cancellationToken).Type is not { TypeKind: TypeKind.Error, Name.Length: > 0 } unresolved)
                continue;

            if (declarationIndex < 0)
                declarationIndex = GetDeclarationIndex(type, declaration);

            var (isQualified, @namespace) = ReadQualifier(baseType, semanticModel, cancellationToken);
            var site = new BaseTypeSite(declaration.SyntaxTree.FilePath, typeName, declarationIndex, index);

            types.Add(new UnresolvedBaseType(unresolved.Name, isQualified, @namespace, site));
            locations.Add(new BaseTypeLocation(site, DiagnosticLocation.From(baseType.GetLocation())));
        }

        return types.ToImmutable();
    }

    /// <summary>
    /// Which declaration of a class in its file a declaration is, counted from 0 in the order they're written.
    /// </summary>
    // Counted within the file, so an edit in another file doesn't change it.
    private static int GetDeclarationIndex(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        var index = 0;

        foreach (var reference in type.DeclaringSyntaxReferences)
            if (reference.SyntaxTree == declaration.SyntaxTree && reference.Span.Start < declaration.SpanStart)
                index++;

        return index;
    }

    /// <summary>
    /// Whether a base type is written with a qualifier, and the namespace that qualifier names. The namespace is
    /// <c>""</c> for <c>global::</c>, and <see langword="null"/> for a qualifier that names no namespace, such as an
    /// extern alias.
    /// </summary>
    private static (bool IsQualified, string? Namespace) ReadQualifier(TypeSyntax type, SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        (bool IsQualified, string? Namespace) qualifier = type switch
        {
            IdentifierNameSyntax => (false, null),
            QualifiedNameSyntax qualified => (true, semanticModel.GetSymbolInfo(qualified.Left, cancellationToken).Symbol is INamespaceSymbol @namespace
                ? GetNamespaceName(@namespace)
                : null),
            AliasQualifiedNameSyntax aliased => (true, aliased.Alias.Identifier.IsKind(SyntaxKind.GlobalKeyword) ? "" : null),

            // A generic name, which no contract has.
            _ => (true, (string?)null),
        };

        return qualifier;
    }

    /// <summary>
    /// Which generated contract each base type will bind to, where two or more contracts generated here share its
    /// simple name. The compilation is given a stand-in for each, and the compiler binds the base type, so its lookup
    /// through namespaces and usings decides. Elsewhere the qualifier or name tells (<see cref="FindGeneratedContract"/>).
    /// </summary>
    // Runs on every edit, but returns at once where no two contracts share a name. The question comes from discovery
    // without locations, so it stays the same when a wording changes.
    public static IncrementalValueProvider<EquatableArray<ContractBinding>> BindContracts(IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<Discovery> discovery, IncrementalValueProvider<Discovery> locations, IncrementalValueProvider<DeclaredLanguages?> languages)
    {
        var question = discovery
            .Combine(languages)
            .Select(static (data, _) => AskForBindings(data.Left, data.Right))
            .WithTrackingName(BindingQuestionTrackingName);

        var bindings = question
            .Combine(context.CompilationProvider)
            .Combine(locations)
            .Select(static (data, cancellationToken) => Bind(data.Left.Left, data.Left.Right, data.Right, cancellationToken));

        return bindings;
    }

    /// <summary>
    /// The pipeline step of <see cref="AskForBindings"/>, for a test to read.
    /// </summary>
    internal const string BindingQuestionTrackingName = "BindingQuestion";

    /// <summary>
    /// The generated contracts that share a simple name with another, and the base types written with that name alone.
    /// Empty where no two contracts share a name.
    /// </summary>
    private static ContractBindingQuestion AskForBindings(Discovery discovery, DeclaredLanguages? languages)
    {
        if (languages is not { } declared)
            return default;

        // All the contracts a base type can bind to.
        var contracts = GetContractNames(GetContractStarts(discovery.Sets, declared))
            .GroupBy(GetSimpleName, StringComparer.Ordinal)
            .Where(static group => group.Skip(1).Any())
            .SelectMany(static group => group)
            .ToImmutableArray();

        if (contracts.IsEmpty)
            return default;

        var names = new HashSet<string>(contracts.Select(GetSimpleName), StringComparer.Ordinal);

        var baseTypes = discovery.Sets.SelectMany(static set => set.UnresolvedBaseTypes)
            .Concat(discovery.UnextendableSets.SelectMany(static set => set.UnresolvedBaseTypes))
            .Where(baseType => !baseType.IsQualified && names.Contains(baseType.Name))
            .Distinct()
            .ToImmutableArray();

        return new ContractBindingQuestion(new EquatableArray<string>(contracts), new EquatableArray<UnresolvedBaseType>(baseTypes));
    }

    /// <summary>
    /// Binds each base type of the question in the compilation with a stand-in for each of its contracts.
    /// </summary>
    private static EquatableArray<ContractBinding> Bind(ContractBindingQuestion question, Compilation compilation,
        Discovery locations, CancellationToken cancellationToken)
    {
        if (question.BaseTypes.Length is 0)
            return default;

        var written = new Dictionary<BaseTypeSite, DiagnosticLocation>();

        foreach (var location in locations.Sets.SelectMany(static set => set.Locations.BaseTypes)
                     .Concat(locations.UnextendableSets.SelectMany(static set => set.BaseTypeLocations)))
        {
            if (!written.ContainsKey(location.Site))
                written.Add(location.Site, location.Location);
        }

        var standIns = new StringBuilder();

        foreach (var contract in question.Contracts)
        {
            var separator = contract.LastIndexOf('.');
            var name = CodeEscaping.EscapeIdentifier(contract.Substring(separator + 1));

            standIns.AppendLine(separator < 0
                ? $"interface {name} {{ }}"
                : $"namespace {String.Join(".", contract.Substring(0, separator).Split('.').Select(CodeEscaping.EscapeIdentifier))} {{ interface {name} {{ }} }}");
        }

        var options = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
        var standInTree = CSharpSyntaxTree.ParseText(standIns.ToString(), options, cancellationToken: cancellationToken);
        var withStandIns = compilation.AddSyntaxTrees(standInTree);

        var bindings = ImmutableArray.CreateBuilder<ContractBinding>();
        var models = new Dictionary<SyntaxTree, SemanticModel>();

        foreach (var baseType in question.BaseTypes)
        {
            // An IDE edit can leave a tree of an earlier compilation.
            if (!written.TryGetValue(baseType.Site, out var location) || location.Tree is not { } tree || !withStandIns.ContainsSyntaxTree(tree))
                continue;

            if (!models.TryGetValue(tree, out var model))
                models[tree] = model = withStandIns.GetSemanticModel(tree);

            var node = tree.GetRoot(cancellationToken).FindNode(location.Span, getInnermostNodeForTie: true);

            if (model.GetTypeInfo(node, cancellationToken).Type is INamedTypeSymbol { TypeKind: TypeKind.Interface } bound
                && bound.DeclaringSyntaxReferences.Any(reference => reference.SyntaxTree == standInTree))
            {
                var @namespace = GetNamespaceName(bound.ContainingNamespace);
                bindings.Add(new ContractBinding(baseType.Site, @namespace.Length > 0 ? $"{@namespace}.{bound.Name}" : bound.Name));
            }
        }

        return new EquatableArray<ContractBinding>(bindings.ToImmutable());
    }

    /// <summary>
    /// The name of a type without its namespace: <c>IShopLocalization</c> for <c>Shop.IShopLocalization</c>.
    /// </summary>
    internal static string GetSimpleName(string fullName)
    {
        var simpleName = fullName.Substring(fullName.LastIndexOf('.') + 1);
        return simpleName;
    }

    internal static string GetNamespaceName(INamespaceSymbol @namespace)
    {
        var namespaceName = @namespace.IsGlobalNamespace ? "" : @namespace.ToDisplayString();
        return namespaceName;
    }
}
