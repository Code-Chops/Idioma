namespace CodeChops.Idioma.Generators;

/// <summary>
/// The rules <see cref="LocalizationSetsPart"/> reports.
/// </summary>
internal enum LocalizationSetsRule
{
    /// <summary>
    /// <c>LOC100</c>: a set lists <c>ILocalizationSet</c> in an assembly that declares no languages.
    /// </summary>
    NoLanguagesDeclared,

    /// <summary>
    /// <c>LOC200</c>: a default-language set the generator can't use.
    /// </summary>
    CannotExtend,

    /// <summary>
    /// <c>LOC201</c>: a contract of this assembly, generated or written by hand, has no set for a supported language.
    /// </summary>
    MissingLanguage,

    /// <summary>
    /// <c>LOC202</c>: a set serves a language the assembly does not list, a set marked
    /// <c>[ExcludeFromLocalizationDiscovery]</c> included.
    /// </summary>
    UnlistedLanguage,

    /// <summary>
    /// <c>LOC203</c>: a set derives from a set of another language, in any assembly.
    /// </summary>
    DerivedFromOtherLanguage,

    /// <summary>
    /// <c>LOC204</c>: a set lists <c>ILocalizationSet</c> without implementing its contract.
    /// </summary>
    MissingContract,

    /// <summary>
    /// <c>LOC209</c>: a set of a contract for a language that another set of that contract serves already, where
    /// neither derives from the other.
    /// </summary>
    RepeatedLanguage,

    /// <summary>
    /// <c>LOC700</c>: a referenced contract has no set for a supported language.
    /// </summary>
    LibraryMissingLanguage,

    /// <summary>
    /// <c>LOC701</c>: a referenced library writes texts inline, and lacks a supported language.
    /// </summary>
    LibraryCannotBeTranslated,

    /// <summary>
    /// <c>LOC300</c>: a text of a default-language set is named like the generated texts class, or like a method of
    /// that class: the one that gives a text by its key, or one of the two that give the text of an enum
    /// value.
    /// </summary>
    ReservedTextName,

    /// <summary>
    /// <c>LOC400</c>: a method of a default-language set has the shape of a text that takes values, but can't be one.
    /// </summary>
    UnsupportedText,

    /// <summary>
    /// <c>LOC303</c>: a default-language set inherits a text from a class of its assembly without declaring it, so its
    /// contract leaves the text out.
    /// </summary>
    UndeclaredText,

    /// <summary>
    /// <c>LOC305</c>: a text of the default-language set of an enum isn't a member of its enums. The analyzer reports
    /// the same in every other set.
    /// </summary>
    TextOutsideEnum,
}
