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

    public const string TranslationSet = Namespace + ".ITranslationSet";
    public const string LocalizableText = Namespace + ".LocalizableText";
    public const string ResourceText = Namespace + ".ResourceText";
    public const string TranslationsRegistry = Namespace + ".TranslationsRegistry";
    public const string SupportedLanguagesAttribute = Namespace + ".SupportedLanguagesAttribute";
    public const string ExcludeFromTranslationDiscoveryAttribute = Namespace + ".ExcludeFromTranslationDiscoveryAttribute";

    public const string GeneratedCode = InternalsNamespace + ".GeneratedCode";
    public const string InlineText = InternalsNamespace + ".IInlineText";
    public const string TranslationSetAttribute = InternalsNamespace + ".TranslationSetAttribute";
    public const string InlineTextsAttribute = InternalsNamespace + ".InlineTextsAttribute";
    public const string FormatPlaceholdersAttribute = InternalsNamespace + ".FormatPlaceholdersAttribute";

    /// <summary>
    /// Whether a type is <c>ITranslationSet</c> itself. Told by its name and namespaces, part by part, which builds no
    /// string: discovery asks this of every interface of every class with a base list, on every edit.
    /// </summary>
    public static bool IsTranslationSet(INamedTypeSymbol type)
    {
        return type is
        {
            Name: "ITranslationSet",
            Arity: 0,
            ContainingType: null,
            ContainingNamespace:
            {
                Name: "Idioma",
                ContainingNamespace: { Name: "CodeChops", ContainingNamespace.IsGlobalNamespace: true },
            },
        };
    }
}
