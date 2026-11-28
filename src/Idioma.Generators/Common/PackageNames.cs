using System.Reflection;

namespace CodeChops.Idioma.Generators;

/// <summary>
/// The names of this package's types and namespaces, as the build finds them and generated code writes them.
/// </summary>
// The tests pin each name to its type (Metadata_ShouldMatchWhatGeneratorReads).
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
    public const string StandaloneTextsAttribute = InternalsNamespace + ".StandaloneTextsAttribute";
    public const string ShownValueAttribute = InternalsNamespace + ".ShownValueAttribute";

    /// <summary>
    /// The docs of the release this assembly ships in, under the tag the publish workflow requires, such as
    /// <c>v0.17.0-preview</c>. A build without a package version links to <c>main</c>.
    /// </summary>
    // Pinned to the release, because below 1.0 a code can move or change its meaning in the next one. So a link from an
    // IDE that runs an older package still lands on the section of its own code. The informational version is the
    // package version (Directory.Build.props), with the commit after a '+'.
    private static string BuildErrorsFolder { get; } = $"https://github.com/Code-Chops/Idioma/blob/{ReadReleaseTag()}/docs/build-errors/";

    /// <summary>
    /// The tag of this assembly's package version, such as <c>v0.17.0-preview</c>, or <c>main</c> without a version.
    /// </summary>
    internal static string ReadReleaseTag()
    {
        var version = typeof(PackageNames).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (String.IsNullOrWhiteSpace(version))
            return "main";

        var commit = version!.IndexOf('+');
        var tag = $"v{(commit < 0 ? version : version.Substring(0, commit))}";

        return tag;
    }

    /// <summary>
    /// The help link of a diagnostic, such as <c>LOC2XX-sets.md#loc201</c> for <c>LOC201</c>, in the docs of this
    /// release. The first digit of a code names its page.
    /// </summary>
    // Paths are case-sensitive on GitHub.
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
    /// Whether a type is <c>ILocalizationSet</c> itself. Compared part by part without building a string, because
    /// discovery asks it of every interface on every edit.
    /// </summary>
    public static bool IsLocalizationSet(INamedTypeSymbol type)
    {
        return IsLocalizationSet(type, arity: 0);
    }

    /// <summary>
    /// Whether a type is <c>ILocalizationSet&lt;TEnum&gt;</c>, which a set of an enum's texts lists.
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
    /// Whether a type is a contract: generated with <c>[LocalizationContract]</c>, or written by hand deriving from
    /// <c>ILocalizationSet</c>. <c>ILocalizationSet&lt;TEnum&gt;</c> is no contract.
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
    /// Whether a type is a generated contract, whose texts follow the default-language set.
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
    /// The enums a generated contract names, as in <c>[LocalizationContract(typeof(OrderStatus))]</c>. Empty for any
    /// other contract.
    /// </summary>
    public static ImmutableArray<INamedTypeSymbol> GetContractEnums(INamedTypeSymbol contract)
    {
        foreach (var attribute in contract.GetAttributes())
        {
            if (attribute.AttributeClass is not { } attributeClass || !IsLocalizationContractAttribute(attributeClass))
                continue;

            if (attribute.ConstructorArguments is not [{ Kind: TypedConstantKind.Array } enums])
                return [];

            return [.. enums.Values.Select(static value => value.Value).OfType<INamedTypeSymbol>().Where(static type => type.TypeKind is TypeKind.Enum)];
        }

        return [];
    }

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
