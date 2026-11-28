namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes the contracts and generated classes (<see cref="LocalizationSetsPart"/>) and the registrar
/// (<see cref="LocalizationRegistrationPart"/>). One generator for both, so the compilation's classes are read once
/// per edit.
/// </summary>
/// <remarks>
/// Each part reads only what it uses of a set (<see cref="DiscoveredTexts"/>). The registrar's share holds no wording
/// and no location, so editing a wording doesn't rerun the registrar. Both parts report from the contracts' share.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class LocalizationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var inputs = LocalizationInputs.Read(context);

        LocalizationSetsPart.Initialize(context, inputs);
        LocalizationRegistrationPart.Initialize(context, inputs);
    }
}

/// <summary>
/// What both parts of <see cref="LocalizationGenerator"/> read, each read once.
/// </summary>
/// <param name="Languages"><see langword="null"/> when the assembly declares none.</param>
/// <param name="RegistrarLanguages">
/// The languages without the attribute's location, so an edit in its file doesn't rerun the registrar.
/// </param>
/// <param name="Compiles">
/// Whether the generated code compiles with this project's C# version (<see cref="LanguageVersionCheck"/>).
/// </param>
/// <param name="ContractDiscovery">
/// The contracts' share (<see cref="DiscoveredTexts.OfContract"/>), with wordings and locations.
/// </param>
/// <param name="RegistrarDiscovery">
/// The registrar's share (<see cref="DiscoveredTexts.OfRegistrar"/>), without wordings and locations.
/// </param>
/// <param name="Contracts">See <see cref="LocalizationDiscovery.ReadGeneratedContracts"/>.</param>
internal sealed record LocalizationInputs(
    IncrementalValueProvider<DeclaredLanguages?> Languages,
    IncrementalValueProvider<DeclaredLanguages?> RegistrarLanguages,
    IncrementalValueProvider<bool> Compiles,
    IncrementalValueProvider<Discovery> ContractDiscovery,
    IncrementalValueProvider<Discovery> RegistrarDiscovery,
    IncrementalValueProvider<GeneratedContracts> Contracts)
{
    public static LocalizationInputs Read(IncrementalGeneratorInitializationContext context)
    {
        var languages = SupportedLanguages.Read(context);
        var (contractDiscovery, registrarDiscovery) = LocalizationDiscovery.Read(context);

        // A location holds its syntax tree, which every edit in that file renews.
        var registrarLanguages = languages.Select(static (languages, _) =>
            languages is { } declared ? declared with { Location = default } : (DeclaredLanguages?)null);

        // From the registrar's share, which editing a wording leaves as it was.
        var bindings = LocalizationDiscovery.BindContracts(context, registrarDiscovery, contractDiscovery, registrarLanguages);
        var contracts = LocalizationDiscovery.ReadGeneratedContracts(registrarDiscovery, registrarLanguages, bindings);

        var inputs = new LocalizationInputs(languages, registrarLanguages, LanguageVersionCheck.Read(context, languages), contractDiscovery, registrarDiscovery, contracts);
        return inputs;
    }
}
