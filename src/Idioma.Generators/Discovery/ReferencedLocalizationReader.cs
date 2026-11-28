namespace CodeChops.Idioma.Generators;

/// <summary>
/// Reads what the referenced assemblies say about their languages (<see cref="ReferencedLocalization"/>), for
/// <see cref="LocalizationSetsAnalysis.FindLibraryGaps"/> to hold them to the languages of this assembly, and for
/// <see cref="LocalizationSetsAnalysis.FindRewordingGaps"/> to hold this assembly's rewordings to their sets.
/// </summary>
internal static class ReferencedLocalizationReader
{
    /// <summary>
    /// What the referenced assemblies say about their languages, ordered by assembly name. Only assemblies that
    /// reference this package are read.
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
                    && attribute.ConstructorArguments is [{ Value: string contract }, { Value: string language }, { Value: string set }, var reworded, { Kind: TypedConstantKind.Array } bases])
                {
                    var baseNames = bases.Values.Select(static name => name.Value).OfType<string>().ToImmutableArray();
                    sets.Add(new ProvidedLocalizationSet(contract, language, set, reworded.Value as string, new EquatableArray<string>(baseNames)));

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
                    new EquatableArray<ReferencedSetOfEnum>(setsOfEnums.ToImmutable()),
                    new EquatableArray<string>([.. assembly.Modules.First().ReferencedAssemblies.Select(static identity => identity.Name).OrderBy(static name => name, StringComparer.Ordinal)])));
            }
        }

        var referencedLocalization = new EquatableArray<ReferencedLocalization>(
            references.OrderBy(static reference => reference.AssemblyName, StringComparer.Ordinal)
                .ToImmutableArray());

        return referencedLocalization;
    }

    /// <summary>
    /// A referenced contract of an enum, with the interface a set of it lists, such as
    /// <c>ILocalizationSet&lt;Lib.OrderStatus&gt;</c>. <see langword="null"/> when no enum of it is accessible here.
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
