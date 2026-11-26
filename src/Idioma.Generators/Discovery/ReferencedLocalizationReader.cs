namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads what the referenced assemblies say about their languages (<see cref="ReferencedLocalization"/>), for
/// <see cref="LocalizationSetsAnalysis.FindLibraryGaps"/> to hold them to the languages of this assembly.
/// </summary>
internal static class ReferencedLocalizationReader
{
    /// <summary>
    /// What the referenced assemblies say about their languages, in a fixed order. Only assemblies that reference this
    /// package can hold its attributes, which keeps the framework's assemblies out of this.
    /// </summary>
    public static EquatableArray<ReferencedLocalization> Read(Compilation compilation)
    {
        var references = ImmutableArray.CreateBuilder<ReferencedLocalization>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (!LocalizationDiscovery.ReferencesPackage(assembly))
                continue;

            var sets = ImmutableArray.CreateBuilder<ProvidedLocalizationSet>();
            var setsOfEnums = ImmutableArray.CreateBuilder<ReferencedSetOfEnum>();
            var read = new HashSet<string>(StringComparer.Ordinal);
            var hasStandaloneTexts = false;

            foreach (var attribute in assembly.GetAttributes())
            {
                var name = attribute.AttributeClass?.ToDisplayString();

                if (String.Equals(name, PackageNames.LocalizationSetAttribute, StringComparison.Ordinal)
                    && attribute.ConstructorArguments.Length is 2
                    && attribute.ConstructorArguments[0].Value is string contract
                    && attribute.ConstructorArguments[1].Value is string language)
                {
                    sets.Add(new ProvidedLocalizationSet(contract, language));

                    if (read.Add(contract) && ReadSetOfEnum(assembly, contract, compilation) is { } setOfEnum)
                        setsOfEnums.Add(setOfEnum);
                }
                else if (String.Equals(name, PackageNames.StandaloneTextsAttribute, StringComparison.Ordinal))
                {
                    hasStandaloneTexts = true;
                }
            }

            var languages = SupportedLanguages.ReadCodes(assembly);

            if (sets.Count > 0 || languages.Length > 0)
            {
                references.Add(new ReferencedLocalization(
                    assembly.Identity.Name,
                    new EquatableArray<string>(languages),
                    new EquatableArray<ProvidedLocalizationSet>(sets.ToImmutable()),
                    hasStandaloneTexts,
                    new EquatableArray<ReferencedSetOfEnum>(setsOfEnums.ToImmutable())));
            }
        }

        var referencedLocalization = new EquatableArray<ReferencedLocalization>(
            references.OrderBy(static reference => reference.AssemblyName, StringComparer.Ordinal)
                .ToImmutableArray());

        return referencedLocalization;
    }

    /// <summary>
    /// A referenced contract of an enum, with what a set of it lists in place of <c>ILocalizationSet</c>:
    /// <c>ILocalizationSet&lt;Lib.OrderStatus&gt;</c>, by the enum's full name. <see langword="null"/> for a contract of
    /// no enum, and for one whose enums this assembly can't name, as an internal enum of the library.
    /// </summary>
    private static ReferencedSetOfEnum? ReadSetOfEnum(IAssemblySymbol assembly, string contract, Compilation compilation)
    {
        if (assembly.GetTypeByMetadataName(contract) is not { } type)
            return null;

        var enums = PackageNames.GetContractEnums(type)
            .Where(@enum => compilation.IsSymbolAccessibleWithin(@enum, compilation.Assembly))
            .ToArray();

        if (enums.Length is 0)
            return null;

        var setInterface = String.Join(", ", enums.Select(static @enum =>
            $"{SupportedLanguages.SetInterface}<{@enum.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted))}>"));

        return new ReferencedSetOfEnum(contract, setInterface);
    }
}
