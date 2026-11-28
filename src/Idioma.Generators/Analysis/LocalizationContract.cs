namespace CodeChops.Idioma.Generators;

/// <summary>
/// Everything a generated contract and its generated class are built from. The class doesn't hold the texts when the
/// generated code can't create the default-language set (<c>LOC200</c>).
/// </summary>
internal sealed record LocalizationContract(
    string HintName,
    string Namespace,
    string ClassName,
    bool IsPublic,
    string ContractName,
    string TextsName,
    EquatableArray<LocalizationMember> Members,
    EquatableArray<SetLink> Sets,
    bool HasTexts,
    bool DefaultSetListsContract,
    RepeatedUsings Usings,
    EquatableArray<LocalizedEnum> Enums);

/// <summary>
/// A set of a generated contract, for the crefs on its texts. An inherited text is named on the class that declares it.
/// </summary>
internal readonly record struct SetLink(
    string Language,
    string Namespace,
    string Name,
    EquatableArray<string> DeclaredTexts,
    EquatableArray<InheritedText> InheritedTexts,
    EquatableArray<TextWording> Wordings,
    bool IsExcluded);
