namespace CodeChops.Idioma.Generators;

/// <summary>
/// The descriptors of the codes that more than one generator or analyzer reports. One per code, so that an IDE's list
/// of rules shows each code once, with one title and one description. What differs between the reporters is a part of
/// the message, which each one passes as an argument.
/// </summary>
internal static class SharedRules
{
    /// <summary>
    /// <c>LOC100</c>: a set, <c>LocalizedText.Inline</c>, or <c>[Localized]</c> in an assembly that declares no
    /// languages. The first argument says what was found, as in <c>'ShopLocalizationEn' is a localization set</c>.
    /// </summary>
    /// <remarks>
    /// The generators report it for a set and for <c>[Localized]</c>, and <c>UnnamedLanguageAnalyzer</c> for a call
    /// of <c>LocalizedText.Inline</c>, in Razor markup too.
    /// </remarks>
    public static readonly DiagnosticDescriptor NoLanguagesDeclared = new(
        id: LocalizationSetsPart.NoLanguagesDeclaredDiagnosticId,
        title: "Text in an assembly without languages",
        messageFormat: "{0}, but this assembly declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source generator needs the languages of [assembly: SupportedLanguages]. It writes the contract from the default-language set and holds the other sets to it. It writes LocalizedText.Inline and [Localized] with one parameter per language, so without the attribute the compiler finds neither (CS0117, CS0246). A set that rewords a library's set, or adds a language to its contract, needs no declaration.",
        helpLinkUri: PackageNames.HelpLink(LocalizationSetsPart.NoLanguagesDeclaredDiagnosticId));

    /// <summary>
    /// <c>LOC402</c>: a language leaves out a value the default wording shows, in a text of a set or in an inline
    /// text. The arguments are what leaves it out, the value, what shows it, the value's name, and the language whose
    /// users won't see it.
    /// </summary>
    /// <remarks>
    /// <c>TextParametersAnalyzer</c> reports it for a method of a set, and <c>InlineTextValuesAnalyzer</c> for a call
    /// of <c>LocalizedText.Inline</c>. Both have the same fix: use the value in every language.
    /// </remarks>
    public static readonly DiagnosticDescriptor ValueLeftOut = new(
        id: TextParametersAnalyzer.ValueLeftOutDiagnosticId,
        title: "Value left out of a wording",
        messageFormat: "{0} doesn't use {1}, but {2} shows it. Use '{3}' in every language, or users of {4} won't see it.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The compiler allows wording that leaves a value out: a parameter a method never uses, or a parameter of LocalizedText.Inline that doesn't show what another language shows. Every language must use a value that the default wording shows, as a parameter of a text of a set, or in an interpolated string of an inline text. A value the default wording only uses to choose between wordings, such as a form of address, may go unused in a language that makes no such distinction.",
        helpLinkUri: PackageNames.HelpLink(TextParametersAnalyzer.ValueLeftOutDiagnosticId));
}
