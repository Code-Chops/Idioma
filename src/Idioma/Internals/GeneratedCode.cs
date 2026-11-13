namespace CodeChops.Idioma.Internals;

/// <summary>
/// What the code written by the source generator calls. You never need this type: write a text as
/// <c>LocalizableText.Translated</c>, or in a set per language (see <see cref="ITranslations"/>). In an assembly that
/// declares <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>, calling it by hand is a build error
/// (<c>LOC012</c>).
/// </summary>
// Public, and apart from the types it serves: generated code lies in the consumer's assembly, so it can only call what
// is public, and here its entry points stay out of the members of a text and of the registry.
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedCode
{
    /// <summary>
    /// Creates a text with its translations, for the generated <c>LocalizableText.Translated</c>, which demands every
    /// language of <see cref="SupportedLanguagesAttribute">[SupportedLanguages]</see>.
    /// </summary>
    /// <param name="defaultText">The wording shown for every culture without a translation.</param>
    /// <param name="translations">
    /// The translations, each a language (a culture name, see <see cref="LocalizableText.Translations"/>) and its
    /// wording; a language listed twice keeps its last wording.
    /// </param>
    /// <exception cref="ArgumentException">
    /// When a language is empty or white-space; pass a culture name such as <c>en</c> or <c>en-GB</c>.
    /// </exception>
    public static LocalizableText CreateText(string defaultText, params ReadOnlySpan<(string Language, string Text)> translations)
    {
        return LocalizableText.FromGeneratedCode(defaultText, translations);
    }

    /// <summary>
    /// Declares a text of a translations contract, with its default wording taken from a set:
    /// <c>GeneratedCode.CreateResourceText&lt;IShopTranslations&gt;("Shop.Checkout", t => t.Checkout, new ShopTranslationsEN())</c>.
    /// The class the source generator writes declares its texts this way.
    /// </summary>
    /// <typeparam name="TTranslations">
    /// The contract the text belongs to; every registered set implementing it provides the text. Name it: inferred from
    /// <paramref name="defaults"/> it would be that set's own type, and no other language's set would match.
    /// </typeparam>
    /// <param name="key">The name of this text in a resource file.</param>
    /// <param name="selector">Picks the text from a set.</param>
    /// <param name="defaults">The set that supplies the default wording, typically the contract's English set.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="key"/> is empty or white-space; pass the name the text has in the resource file.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When <paramref name="defaults"/> has no language code: its name does not end in one and it does not implement
    /// <see cref="ITranslations.LanguageCode"/>. Name the set after its language (<c>ShopTranslationsEN</c>), or
    /// implement that property.
    /// </exception>
    public static ResourceText CreateResourceText<TTranslations>(string key, Func<TTranslations, string> selector, TTranslations defaults)
        where TTranslations : class, ITranslations
    {
        return ResourceText.Create(key, selector, defaults);
    }

    /// <summary>
    /// Sets the app's <see cref="TranslationsRegistry.SupportedLanguages"/>, when an application that declares
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see> starts. Only the first call counts,
    /// so an application the app loads later, such as the app a test project tests, cannot change them.
    /// </summary>
    /// <param name="defaultLanguage">The default language: a culture name such as <c>en</c> or <c>en-GB</c>.</param>
    /// <param name="otherLanguages">The other languages, each a culture name.</param>
    /// <exception cref="ArgumentException">
    /// When a language is not a culture name, such as an empty string or <c>en_GB</c>; pass a culture name such as
    /// <c>en</c> or <c>en-GB</c>.
    /// </exception>
    public static void RegisterLanguages(string defaultLanguage, params ReadOnlySpan<string> otherLanguages)
    {
        TranslationsRegistry.RegisterLanguages(defaultLanguage, otherLanguages);
    }

    /// <summary>
    /// Records the language a set's name gives it when the last two letters cannot hold it, such as <c>en-GB</c> for
    /// <c>ShopTranslationsEnGB</c>, for a set named after a language its assembly declares, before the set registers.
    /// Only the first call for a set counts.
    /// </summary>
    /// <typeparam name="TTranslations">The set.</typeparam>
    /// <param name="languageCode">The language: a culture name such as <c>en-GB</c>.</param>
    /// <exception cref="ArgumentException">
    /// When <paramref name="languageCode"/> is not a culture name, such as an empty string or <c>en_GB</c>; pass a culture
    /// name such as <c>en-GB</c>.
    /// </exception>
    public static void RegisterLanguageCode<TTranslations>(string languageCode)
        where TTranslations : class, ITranslations
    {
        TranslationsRegistry.RegisterLanguageCode<TTranslations>(languageCode);
    }
}
