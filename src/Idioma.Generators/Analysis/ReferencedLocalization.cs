namespace CodeChops.Idioma.Generators;

/// <summary>
/// What a referenced assembly says about its languages: those it declares, its sets of public contracts, and whether it
/// writes standalone texts. <see cref="References"/> are the names of the assemblies it references, which register
/// before it.
/// </summary>
internal readonly record struct ReferencedLocalization(
    string AssemblyName,
    EquatableArray<string> Languages,
    EquatableArray<ProvidedLocalizationSet> Sets,
    bool HasStandaloneTexts,
    EquatableArray<ReferencedSetOfEnum> SetsOfEnums,
    EquatableArray<string> References);

/// <summary>
/// A referenced public contract of an enum's texts, with the interface its sets list in place of
/// <c>ILocalizationSet</c>, such as <c>ILocalizationSet&lt;Lib.OrderStatus&gt;</c> (<c>LOC206</c>).
/// </summary>
internal readonly record struct ReferencedSetOfEnum(
    string Contract,
    string SetInterface);
