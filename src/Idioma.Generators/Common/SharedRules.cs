namespace CodeChops.Idioma.Generators;

/// <summary>
/// The descriptors of codes that more than one generator or analyzer reports, one per code so an IDE lists each once.
/// Each reporter passes what differs as a message argument.
/// </summary>
internal static class SharedRules
{
    /// <summary>
    /// <c>LOC100</c>: a set or a standalone text in an assembly that declares no languages. The generators report it
    /// for a set and <c>[Localized]</c>, and <c>UnnamedLanguageAnalyzer</c> for <c>LocalizedText.Inline</c>.
    /// </summary>
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
    /// <c>LOC402</c>: a language leaves out a value the default wording shows. <c>TextParametersAnalyzer</c> reports it
    /// for a method of a set, and <c>InlineTextValuesAnalyzer</c> for <c>LocalizedText.Inline</c>.
    /// </summary>
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
