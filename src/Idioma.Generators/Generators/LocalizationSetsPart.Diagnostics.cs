namespace CodeChops.Idioma.Generators;

public static partial class LocalizationSetsPart
{
    public const string NoLanguagesDeclaredDiagnosticId = "LOC100";
    public const string CannotExtendDiagnosticId = "LOC200";
    public const string MissingLanguageDiagnosticId = "LOC201";
    public const string UnlistedLanguageDiagnosticId = "LOC202";
    public const string DerivedFromOtherLanguageDiagnosticId = "LOC203";
    public const string MissingContractDiagnosticId = "LOC204";
    public const string RepeatedLanguageDiagnosticId = "LOC209";
    public const string LibraryMissingLanguageDiagnosticId = "LOC700";
    public const string LibraryCannotBeTranslatedDiagnosticId = "LOC701";
    public const string ReservedTextNameDiagnosticId = "LOC300";
    public const string UndeclaredTextDiagnosticId = "LOC303";
    public const string UnsupportedTextDiagnosticId = "LOC400";

    /// <summary>
    /// The property of a diagnostic that holds the name that resolves it, for its code fix to apply: the name the class
    /// should carry (<c>LOC200</c>, when a rename resolves it), the language to list in
    /// <c>[assembly: SupportedLanguages]</c> (<c>LOC202</c>), the contract to add next to <c>ILocalizationSet</c>
    /// (<c>LOC204</c>, when this build generates that contract), or the set to add (<c>LOC201</c> and <c>LOC700</c>).
    /// </summary>
    public const string SuggestedNameProperty = "SuggestedName";

    /// <summary>
    /// The property of <c>LOC201</c> that holds the namespace of the set to add, empty for the global namespace.
    /// <c>LOC700</c> has none, because the set of a library's contract lies in a namespace of the app, which the fix
    /// chooses.
    /// </summary>
    public const string SuggestedNamespaceProperty = "SuggestedNamespace";

    /// <summary>
    /// The property of <c>LOC201</c> and <c>LOC700</c> that holds the declaration of the set to add, without a body:
    /// <c>public class ShopLocalizationDe : ILocalizationSet, IShopLocalization</c>.
    /// </summary>
    public const string SuggestedDeclarationProperty = "SuggestedDeclaration";

    // Shared with LocalizableTextFactoryGenerator and UnnamedLanguageAnalyzer, which report [Localized] and
    // LocalizableText.Inline in an assembly without languages: all say that the attribute is missing.
#pragma warning disable RS1019 // Diagnostic Id is already used by another analyzer
    private static readonly DiagnosticDescriptor NoLanguagesDeclaredRule = Error(
        NoLanguagesDeclaredDiagnosticId,
        "Localization set in an assembly without languages",
        "'{0}' is a localization set, but this assembly declares no languages. Add [assembly: SupportedLanguages(\"en\", \"nl\")] with your languages, the default first.",
        "The generator writes the contract from the default-language set and holds the other sets to it, so it needs to know the languages. A set that rewords a library's set, or adds a language to its contract, needs no declaration.");
#pragma warning restore RS1019

    private static readonly DiagnosticDescriptor CannotExtendRule = Error(
        CannotExtendDiagnosticId,
        "Default-language set the generator can't use",
        "'{0}' is the default-language set of a contract, but {1}. The generator can't use it, so it writes no texts for it. {2}",
        "The generator writes the contract from the default-language set. It names the generated class after the set without its language suffix, and it creates the set for the default wording of every text. So the set must be a class that generated code in another file can name and create.");

    private static readonly DiagnosticDescriptor MissingLanguageRule = Error(
        MissingLanguageDiagnosticId,
        "Contract lacks a language",
        "'{0}' has no set for '{1}', which [assembly: SupportedLanguages] requires. Add {2}. A class without ILocalizationSet doesn't count as a set, and neither does a struct, or a nested, generic, or file-local class.",
        "Every contract needs a set for each language of [assembly: SupportedLanguages], just as every inline text takes wording for each. That includes a listed region, such as en-GB next to en. Its set derives from the set of its language and holds only the texts that differ.");

    private static readonly DiagnosticDescriptor UnlistedLanguageRule = Error(
        UnlistedLanguageDiagnosticId,
        "Localization set for an unlisted language",
        "'{0}' is a set for '{1}', which [assembly: SupportedLanguages] doesn't list. {2}",
        "[assembly: SupportedLanguages] is the only list of languages the assembly supports, and only sets of those languages are checked for completeness. This also holds for a set marked [ExcludeFromLocalizationDiscovery]. An app shows a culture outside its list in its default language, so registering such a set by hand wouldn't make it show.");

    private static readonly DiagnosticDescriptor DerivedFromOtherLanguageRule = Error(
        DerivedFromOtherLanguageDiagnosticId,
        "Localization set derives from a set of another language",
        "'{0}' is a set for '{1}' but derives from '{2}', so every text it doesn't override stays in '{3}'. To translate, implement the contract instead, which requires every text. To reword '{2}', give '{0}' the language '{3}': end its name in it, or implement LocalizationLanguageCode.",
        "Derive from a set only to reword some of its texts in the same language. In another language, it compiles with texts left untranslated. A regional set may derive from the set of its language, as en-GB from en. Reported in every assembly, with or without [assembly: SupportedLanguages].");

    private static readonly DiagnosticDescriptor MissingContractRule = Error(
        MissingContractDiagnosticId,
        "Localization set doesn't implement its contract",
        "'{0}' is a set for '{1}' but doesn't implement '{2}', so no text reads it. Add '{2}', the contract generated from '{3}', next to ILocalizationSet.",
        "Every set lists ILocalizationSet and its contract, the default-language set included. The contract is what requires every text in every language, and what a text reads its wording through.");

    private static readonly DiagnosticDescriptor RepeatedLanguageRule = Error(
        RepeatedLanguageDiagnosticId,
        "Second set of a contract for one language",
        "'{0}' is a second set of '{1}' for '{2}', next to '{3}'. Users of '{2}' see only one of them. Remove one of the two, or derive '{0}' from '{3}' to reword some of its texts.",
        "A contract has one set per language. Of two sets of one language, the one whose name sorts last would show, and the other would be left out without a word. A set that derives from a set of the same language rewords it, and always wins over it.");

    private static readonly DiagnosticDescriptor LibraryMissingLanguageRule = Error(
        LibraryMissingLanguageDiagnosticId,
        "Referenced contract lacks a language",
        "No set serves '{0}' in '{1}', which this assembly supports. Add {2}.",
        "An assembly that supports a language must show the texts of the libraries it references in that language too. A set added here is registered automatically and serves the library's texts.");

    private static readonly DiagnosticDescriptor LibraryCannotBeTranslatedRule = new(
        id: LibraryCannotBeTranslatedDiagnosticId,
        title: "Referenced library's inline texts can't be translated",
        messageFormat: "'{0}' has inline texts without '{1}', which this assembly supports. No set added here can translate them, so users of '{1}' will see them in '{2}'.",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nothing outside a library can add a language to its inline texts, which are those it writes with LocalizableText.Inline and with [Localized] on a member. A library that wants to be translatable writes its texts in sets.",
        helpLinkUri: PackageNames.HelpLink(LibraryCannotBeTranslatedDiagnosticId));

    private static readonly DiagnosticDescriptor ReservedTextNameRule = Error(
        ReservedTextNameDiagnosticId,
        "Text named like the generated class or its method",
        "'{0}' has a text named '{1}', like {2}. Rename the text.",
        "In C#, a member can't have the name of its own class, so no text can have the name of the generated class. No text can be named GetTextByKey either, because the generated class gets a method of that name, which gives a text by its key. In a set of the texts of an enum, the same holds for GetTextByValue, the method that gives the text of an enum value, and for GetText, the extension method that gives a value its text.");

    private static readonly DiagnosticDescriptor UndeclaredTextRule = Error(
        UndeclaredTextDiagnosticId,
        "Text the default-language set inherits without declaring it",
        "'{0}' inherits the text '{1}' from '{2}', so its contract leaves '{1}' out, and no other language has to word it. Declare '{1}' in '{0}', or mark it [ExcludeFromLocalizationDiscovery] in '{2}' if it isn't a text.",
        "The contract is written from the texts the default-language set declares itself. A public string property or a method of the shape of a text that it only inherits from a class of its assembly isn't part of the contract, so no other language has to word it, and the generated class has no text for it. Declare the text in the set, by overriding it or by moving it there. Mark a member that isn't a text [ExcludeFromLocalizationDiscovery].");

    private static readonly DiagnosticDescriptor UnsupportedTextRule = Error(
        UnsupportedTextDiagnosticId,
        "Method that can't be a text",
        "'{0}.{1}' is a public method that returns a string, which makes it a text with parameters, but {2}. No text is generated for it. Fix the method. If it isn't a text, make it internal or private, or mark it [ExcludeFromLocalizationDiscovery].",
        "A public method of a default-language set that returns a string and takes parameters is a text with parameters. The generated class gets a method with the same name and parameters. It passes the values to the set of the user's language. That only works for a method that isn't generic, has a name no other method in its set has, and takes at most 8 parameters, each by value and without a default. A method without parameters is no text, because a text without parameters is a property.");

    private static DiagnosticDescriptor Error(string id, string title, string message, string description)
    {
        var error = new DiagnosticDescriptor(id, title, message, "Design", DiagnosticSeverity.Error, isEnabledByDefault: true, description, PackageNames.HelpLink(id));
        return error;
    }

    private static void Report(SourceProductionContext context, EquatableArray<LocalizationSetsFinding> findings)
    {
        foreach (var finding in findings)
        {
            var rule = finding.Rule switch
            {
                LocalizationSetsRule.NoLanguagesDeclared => NoLanguagesDeclaredRule,
                LocalizationSetsRule.CannotExtend => CannotExtendRule,
                LocalizationSetsRule.MissingLanguage => MissingLanguageRule,
                LocalizationSetsRule.UnlistedLanguage => UnlistedLanguageRule,
                LocalizationSetsRule.DerivedFromOtherLanguage => DerivedFromOtherLanguageRule,
                LocalizationSetsRule.MissingContract => MissingContractRule,
                LocalizationSetsRule.RepeatedLanguage => RepeatedLanguageRule,
                LocalizationSetsRule.LibraryMissingLanguage => LibraryMissingLanguageRule,
                LocalizationSetsRule.LibraryCannotBeTranslated => LibraryCannotBeTranslatedRule,
                LocalizationSetsRule.UnsupportedText => UnsupportedTextRule,
                LocalizationSetsRule.ReservedTextName => ReservedTextNameRule,
                LocalizationSetsRule.UndeclaredText => UndeclaredTextRule,
                LocalizationSetsRule.TextOutsideEnum => TextOutsideContractAnalyzer.TextOutsideEnumRule,
                // A rule added without a descriptor here. The generator fails with CS8785, and a test of the new rule
                // shows it, where reporting it under another code would go unnoticed.
                _ => throw new ArgumentOutOfRangeException(nameof(findings), finding.Rule, "A rule without a descriptor."),
            };

            var properties = ImmutableDictionary<string, string?>.Empty;

            if (finding.SuggestedName is { } suggestedName)
                properties = properties.Add(SuggestedNameProperty, suggestedName);

            if (finding.NewSet is { } newSet)
            {
                properties = properties.Add(SuggestedDeclarationProperty, newSet.Declaration);

                if (newSet.Namespace is { } newSetNamespace)
                    properties = properties.Add(SuggestedNamespaceProperty, newSetNamespace);
            }

            context.ReportDiagnostic(Diagnostic.Create(rule, finding.Location.ToLocation(), properties.IsEmpty ? null : properties,
                finding.Arguments.Cast<object>().ToArray()));
        }
    }
}
