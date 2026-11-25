namespace CodeChops.Idioma.Generators;

/// <summary>
/// Everything a generated contract is built from: the name of its file, the default-language set, whether the generated
/// types are public like it or internal, the derived names, its text members in declaration order, the sets translating
/// them, whether the generated class holds the texts, the using directives its file repeats
/// (<see cref="RepeatedUsings"/>), and the enums whose members it has a text for, each of which the generated class
/// gets a method for that gives the text of a value. The class doesn't hold the texts when the generated code can't
/// create the set for their default wording (<c>LOC200</c>). Where the default-language set doesn't list the contract
/// (<c>LOC204</c>), the texts take their default wording from it through a class of the file that does. The file turns
/// off what naming the default-language set, its enums and the types of its parameters reports
/// (<see cref="LocalizationSetsAnalysis.GetContractSuppressedDiagnostics"/>).
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
    EquatableArray<LocalizedEnum> Enums,
    EquatableArray<string> SuppressedDiagnostics);

/// <summary>
/// A set translating a generated contract, for the crefs on its texts: its language, a culture name, its namespace and
/// name, by which the contract's file names it, the texts a cref can name on it, the texts it inherits, which
/// a cref names on the class declaring them, the wording it gives each text that returns a constant or takes values,
/// which the contract's file shows next to the link, and whether it is marked
/// <c>[ExcludeFromLocalizationDiscovery]</c>.
/// </summary>
internal readonly record struct SetLink(
    string Language,
    string Namespace,
    string Name,
    EquatableArray<string> DeclaredTexts,
    EquatableArray<InheritedText> InheritedTexts,
    EquatableArray<TextWording> Wordings,
    bool IsExcluded);
