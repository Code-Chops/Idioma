namespace CodeChops.Idioma.Generators;

/// <summary>
/// The names of this package's own types and namespaces, as the generators, the analyzers, and the code fixes find them
/// in a compilation, and as generated code writes them.
/// </summary>
// One place for each name: a type that is renamed in the package is then renamed here once, and the tests of that type
// pin the name it is found by (Metadata_ShouldMatchWhatGeneratorReads).
internal static class PackageNames
{
    public const string Assembly = "CodeChops.Idioma";
    public const string Namespace = "CodeChops.Idioma";
    public const string InternalsNamespace = Namespace + ".Internals";

    public const string LocalizationSetName = "ILocalizationSet";
    public const string LocalizationSet = Namespace + "." + LocalizationSetName;
    public const string LocalizedTextName = "LocalizedText";
    public const string LocalizedText = Namespace + "." + LocalizedTextName;
    public const string LocalizationRegistry = Namespace + ".LocalizationRegistry";
    public const string SupportedLanguagesAttribute = Namespace + ".SupportedLanguagesAttribute";
    public const string ExcludeFromLocalizationDiscoveryAttribute = Namespace + ".ExcludeFromLocalizationDiscoveryAttribute";

    public const string GeneratedCode = InternalsNamespace + ".GeneratedCode";
    public const string LocalizationSetAttribute = InternalsNamespace + ".LocalizationSetAttribute";
    public const string InlineTextsAttribute = InternalsNamespace + ".InlineTextsAttribute";
    public const string ShownValueAttribute = InternalsNamespace + ".ShownValueAttribute";

    /// <summary>
    /// The folder of the pages of build errors and warnings. Each page holds the codes of one subject, with an example
    /// and the fix of each.
    /// </summary>
    private const string BuildErrorsFolder = "https://github.com/Code-Chops/Idioma/blob/main/docs/build-errors/";

    /// <summary>
    /// The help link of a diagnostic: the section of its code on the page of its subject, such as
    /// <c>LOC2XX-sets.md#loc201</c> for <c>LOC201</c>. The first digit of a code names its subject. An IDE opens the
    /// link from the code in its error list.
    /// </summary>
    // On main, and not at the tag of a version as the README of the package links: this assembly doesn't know the
    // version of the package it ships in. So a page or a section that a released version links to stays where it is.
    // GitHub tells the case of a path apart, so each page is named here as its file is.
    public static string HelpLink(string diagnosticId)
    {
        var page = diagnosticId[3] switch
        {
            '1' => "LOC1XX-languages",
            '2' => "LOC2XX-sets",
            '3' => "LOC3XX-texts-of-a-set",
            '4' => "LOC4XX-texts-with-parameters",
            '5' => "LOC5XX-inline-texts-and-attributes",
            '6' => "LOC6XX-reading-a-text",
            '7' => "LOC7XX-libraries",
            _ => throw new ArgumentOutOfRangeException(nameof(diagnosticId), diagnosticId, "The code has no page of build errors. Add its subject to HelpLink, and its page to docs/build-errors/."),
        };

        var helpLink = $"{BuildErrorsFolder}{page}.md#{diagnosticId.ToLowerInvariant()}";
        return helpLink;
    }

    /// <summary>
    /// Whether a type is <c>ILocalizationSet</c> itself. Told by its name and namespaces, part by part, which builds no
    /// string, because discovery asks this of every interface of every class with a base list, on every edit.
    /// </summary>
    public static bool IsLocalizationSet(INamedTypeSymbol type)
    {
        return IsLocalizationSet(type, arity: 0);
    }

    /// <summary>
    /// Whether a type is <c>ILocalizationSet&lt;TEnum&gt;</c>, which a set of the texts of an enum lists in place of
    /// <c>ILocalizationSet</c>. Told part by part as <see cref="IsLocalizationSet(INamedTypeSymbol)"/> is.
    /// </summary>
    public static bool IsLocalizationSetOfEnum(INamedTypeSymbol type)
    {
        return IsLocalizationSet(type, arity: 1);
    }

    private static bool IsLocalizationSet(INamedTypeSymbol type, int arity)
    {
        return type is
        {
            Name: "ILocalizationSet",
            ContainingType: null,
            ContainingNamespace:
            {
                Name: "Idioma",
                ContainingNamespace: { Name: "CodeChops", ContainingNamespace.IsGlobalNamespace: true },
            },
        } && type.Arity == arity;
    }

    /// <summary>
    /// Whether a type is the contract of a set, such as <c>IShopLocalization</c>: an interface the source generator
    /// marks <c>[LocalizationContract]</c>, or one written by hand, which derives from <c>ILocalizationSet</c>.
    /// <c>ILocalizationSet&lt;TEnum&gt;</c> derives from it too, and is no contract.
    /// </summary>
    public static bool IsContract(INamedTypeSymbol type)
    {
        if (type.TypeKind is not TypeKind.Interface || IsLocalizationSet(type) || IsLocalizationSetOfEnum(type))
            return false;

        if (IsGeneratedContract(type))
            return true;

        foreach (var @interface in type.AllInterfaces)
            if (IsLocalizationSet(@interface))
                return true;

        return false;
    }

    /// <summary>
    /// Whether a type is a contract the source generator writes, which it marks <c>[LocalizationContract]</c>. The
    /// generator writes it from the default-language set on every build, so its texts change with that set. A contract
    /// written by hand doesn't.
    /// </summary>
    public static bool IsGeneratedContract(INamedTypeSymbol type)
    {
        if (type.TypeKind is not TypeKind.Interface)
            return false;

        foreach (var attribute in type.GetAttributes())
            if (attribute.AttributeClass is { } attributeClass && IsLocalizationContractAttribute(attributeClass))
                return true;

        return false;
    }

    /// <summary>
    /// The enums a generated contract names, as in <c>[LocalizationContract(typeof(OrderStatus))]</c>: those whose
    /// members it has a text for, which each of its sets lists as <c>ILocalizationSet&lt;TEnum&gt;</c>. Empty for any
    /// other contract.
    /// </summary>
    public static ImmutableArray<INamedTypeSymbol> GetContractEnums(INamedTypeSymbol contract)
    {
        foreach (var attribute in contract.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass || !IsLocalizationContractAttribute(attributeClass))
                continue;

            // The enums as the one argument of the params constructor, an array.
            if (attribute.ConstructorArguments is not [{ Kind: TypedConstantKind.Array } enums])
                return [];

            return [.. enums.Values.Select(static value => value.Value).OfType<INamedTypeSymbol>().Where(static type => type.TypeKind is TypeKind.Enum)];
        }

        return [];
    }

    /// <summary>
    /// Whether a type is <c>LocalizationContractAttribute</c>, told part by part as
    /// <see cref="IsLocalizationSet(INamedTypeSymbol)"/> is.
    /// </summary>
    private static bool IsLocalizationContractAttribute(INamedTypeSymbol type)
    {
        return type is
        {
            Name: "LocalizationContractAttribute",
            Arity: 0,
            ContainingType: null,
            ContainingNamespace:
            {
                Name: "Internals",
                ContainingNamespace:
                {
                    Name: "Idioma",
                    ContainingNamespace: { Name: "CodeChops", ContainingNamespace.IsGlobalNamespace: true },
                },
            },
        };
    }
}
