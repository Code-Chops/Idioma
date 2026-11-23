namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes what the localization sets of an assembly and its members marked <c>[Localized]</c> need, in two parts: the
/// contract and the generated class of each default-language set (<see cref="LocalizationSetsPart"/>), and the
/// registrar that adds the sets to <c>LocalizationRegistry</c> (<see cref="LocalizationRegistrationPart"/>). One
/// generator for both, so that the classes of the compilation are read once per edit, and the languages and the
/// contracts the sets list are found once.
/// </summary>
/// <remarks>
/// Each part reads only what it uses of a set (<see cref="DiscoveredTexts"/>), so an edit that changes only what one
/// part uses leaves the other's pipeline as it was. <c>LocalizableText.Inline</c> and <c>[Localized]</c> come from a
/// generator of their own (<see cref="LocalizableTextFactoryGenerator"/>).
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
/// <param name="Languages">The languages of <c>[assembly: SupportedLanguages]</c>, or <see langword="null"/> when the
/// assembly declares none.</param>
/// <param name="Compiles">Whether the code the generator writes compiles with this project's version of C#
/// (<see cref="LanguageVersionCheck"/>).</param>
/// <param name="ContractDiscovery">What discovery found, as the part that writes the contracts reads it
/// (<see cref="DiscoveredTexts.OfContract"/>).</param>
/// <param name="RecordDiscovery">What discovery found, as the part that writes the registrar reads it
/// (<see cref="DiscoveredTexts.OfRecord"/>).</param>
/// <param name="Bindings">Which generated contract a base type names where two share its name
/// (<see cref="LocalizationDiscovery.BindContracts"/>).</param>
internal sealed record LocalizationInputs(
    IncrementalValueProvider<DeclaredLanguages?> Languages,
    IncrementalValueProvider<bool> Compiles,
    IncrementalValueProvider<Discovery> ContractDiscovery,
    IncrementalValueProvider<Discovery> RecordDiscovery,
    IncrementalValueProvider<EquatableArray<ContractBinding>> Bindings)
{
    public static LocalizationInputs Read(IncrementalGeneratorInitializationContext context)
    {
        var languages = SupportedLanguages.Read(context);
        var (contractDiscovery, recordDiscovery) = LocalizationDiscovery.Read(context);

        // From what the registrar reads, which a wording leaves as it was. What binds a contract is in both.
        var bindings = LocalizationDiscovery.BindContracts(context, recordDiscovery, languages);

        var inputs = new LocalizationInputs(languages, LanguageVersionCheck.Read(context, languages), contractDiscovery, recordDiscovery, bindings);
        return inputs;
    }
}
