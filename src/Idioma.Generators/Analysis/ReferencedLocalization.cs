namespace CodeChops.Idioma.Generators;

/// <summary>
/// What a referenced assembly says about its languages: those it declares, its sets of public contracts, and whether it
/// writes texts inline.
/// </summary>
internal readonly record struct ReferencedLocalization(
    string AssemblyName,
    EquatableArray<string> Languages,
    EquatableArray<ProvidedLocalizationSet> Sets,
    bool HasInlineTexts,
    EquatableArray<ReferencedSetOfEnum> SetsOfEnums);

/// <summary>
/// A public contract of a referenced assembly that holds the texts of an enum, with what each of its sets lists in place
/// of <c>ILocalizationSet</c>, such as <c>ILocalizationSet&lt;Lib.OrderStatus&gt;</c> (<c>LOC205</c>). The set an app
/// adds for it (<c>LOC700</c>) lists that too.
/// </summary>
internal readonly record struct ReferencedSetOfEnum(
    string Contract,
    string SetInterface);
