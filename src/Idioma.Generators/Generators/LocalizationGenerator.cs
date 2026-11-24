namespace CodeChops.Idioma.Generators;

/// <summary>
/// Writes what the localization sets of an assembly and its members marked <c>[Localized]</c> need, in two parts: the
/// contract and the generated class of each default-language set (<see cref="LocalizationSetsPart"/>), and the
/// registrar that adds the sets to <c>LocalizationRegistry</c> (<see cref="LocalizationRegistrationPart"/>). One
/// generator for both, so that the classes of the compilation are read once per edit, and the languages, the contracts
/// the sets list, and the contracts the compilation generates are found once.
/// </summary>
/// <remarks>
/// <para>
/// Each part reads only what it uses of a set (<see cref="DiscoveredTexts"/>), so an edit that changes only what one
/// part uses leaves the other's pipeline as it was. <c>LocalizableText.Inline</c> and <c>[Localized]</c> come from a
/// generator of their own (<see cref="LocalizableTextFactoryGenerator"/>).
/// </para>
/// <para>
/// An edit of a wording changes the share the contracts are written from, which holds the wordings and every source
/// location. It leaves the registrar's share as it was, which holds neither. So the steps that read the registrar's
/// share alone don't run again: the contracts the compilation generates, and the registrar. Both parts report their
/// diagnostics from the share with the locations.
/// </para>
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
/// <param name="RecordLanguages">The same languages without where the attribute is written, for a step that reports
/// nothing there. An edit in the attribute's file that leaves the languages as they were leaves this as it was.</param>
/// <param name="Compiles">Whether the code the generator writes compiles with this project's version of C#
/// (<see cref="LanguageVersionCheck"/>).</param>
/// <param name="ContractDiscovery">What discovery found, as the part that writes the contracts reads it
/// (<see cref="DiscoveredTexts.OfContract"/>). It holds the wordings and every source location, so both parts report
/// from it.</param>
/// <param name="RecordDiscovery">What discovery found, as the part that writes the registrar reads it
/// (<see cref="DiscoveredTexts.OfRecord"/>). It holds no wording and no location.</param>
/// <param name="Contracts">The contracts the compilation generates, worked out once for both parts
/// (<see cref="LocalizationDiscovery.ReadGeneratedContracts"/>).</param>
internal sealed record LocalizationInputs(
    IncrementalValueProvider<DeclaredLanguages?> Languages,
    IncrementalValueProvider<DeclaredLanguages?> RecordLanguages,
    IncrementalValueProvider<bool> Compiles,
    IncrementalValueProvider<Discovery> ContractDiscovery,
    IncrementalValueProvider<Discovery> RecordDiscovery,
    IncrementalValueProvider<GeneratedContracts> Contracts)
{
    public static LocalizationInputs Read(IncrementalGeneratorInitializationContext context)
    {
        var languages = SupportedLanguages.Read(context);
        var (contractDiscovery, recordDiscovery) = LocalizationDiscovery.Read(context);

        // The location of the attribute holds the syntax tree of its file, which every edit in that file renews.
        var recordLanguages = languages.Select(static (languages, _) =>
            languages is { } declared ? declared with { Location = default } : (DeclaredLanguages?)null);

        // From what the registrar reads, which a wording leaves as it was. What binds a contract, and where one comes
        // from, is in both.
        var bindings = LocalizationDiscovery.BindContracts(context, recordDiscovery, contractDiscovery, recordLanguages);
        var contracts = LocalizationDiscovery.ReadGeneratedContracts(recordDiscovery, recordLanguages, bindings);

        var inputs = new LocalizationInputs(languages, recordLanguages, LanguageVersionCheck.Read(context, languages), contractDiscovery, recordDiscovery, contracts);
        return inputs;
    }
}
