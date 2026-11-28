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
    /// <c>LOC201</c>: a contract of this assembly has no set for a supported language.
    /// </summary>
    MissingLanguage,

    /// <summary>
    /// <c>LOC202</c>: a set serves a language the assembly doesn't list, an excluded set included.
    /// </summary>
    UnlistedLanguage,

    /// <summary>
    /// <c>LOC204</c>: a set derives from a set of another language.
    /// </summary>
    DerivedFromOtherLanguage,

    /// <summary>
    /// <c>LOC205</c>: a set lists <c>ILocalizationSet</c> without implementing its contract.
    /// </summary>
    MissingContract,

    /// <summary>
    /// <c>LOC203</c>: two sets of a contract serve the same language, and neither derives from the other.
    /// </summary>
    RepeatedLanguage,

    /// <summary>
    /// <c>LOC208</c>: a rewording doesn't reach a regional set of the assembly of the set it rewords.
    /// </summary>
    UnreachedRegionalSet,

    /// <summary>
    /// <c>LOC209</c>: two rewordings of one set for one language, neither deriving from the other.
    /// </summary>
    RewordingsDontAddUp,

    /// <summary>
    /// <c>LOC700</c>: a referenced contract has no set for a supported language.
    /// </summary>
    LibraryMissingLanguage,

    /// <summary>
    /// <c>LOC701</c>: a referenced library writes standalone texts and lacks a supported language.
    /// </summary>
    LibraryCannotBeTranslated,

    /// <summary>
    /// <c>LOC300</c>: a text of a default-language set is named like the generated class or one of its methods.
    /// </summary>
    ReservedTextName,

    /// <summary>
    /// <c>LOC400</c>: a method of a default-language set has the shape of a text that takes values, but can't be one.
    /// </summary>
    UnsupportedText,

    /// <summary>
    /// <c>LOC303</c>: a default-language set inherits a text from its assembly without declaring it, so the contract
    /// leaves it out.
    /// </summary>
    UndeclaredText,

    /// <summary>
    /// <c>LOC305</c>: a text of an enum's default-language set isn't a member of its enums. The analyzer reports the
    /// other sets.
    /// </summary>
    TextOutsideEnum,
}
