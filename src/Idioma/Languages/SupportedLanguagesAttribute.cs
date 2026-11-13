namespace CodeChops.Idioma;

/// <summary>
/// Declares the languages of your assembly, the default language first:
/// <c>[assembly: SupportedLanguages("en", "nl")]</c>. From then on, the build makes sure that every text in the
/// assembly exists in each of these languages.
/// </summary>
/// <remarks>
/// <para>
/// Put the attribute once in any file of the assembly. It gives you two ways to write a text, and both demand every
/// language:
/// </para>
/// <list type="bullet">
/// <item>An inline text: <c>LocalizableText.Translated(en: "Checkout", nl: "Afrekenen")</c>. The source generator
/// writes this method with one required argument per language, so leaving a language out does not compile.</item>
/// <item>One class per language, for a library or a larger group of texts: see <see cref="ITranslations"/>.</item>
/// </list>
/// <para>
/// Once you list two or more languages, wording you write as a plain string where a text is expected is a build error
/// (<c>LOC011</c>), so a text cannot end up in one language by accident. A value that is not wording of yours, such as
/// a customer's name, still converts to a text.
/// </para>
/// <para>
/// In an application, the first language is also the one every text falls back to, the texts of the libraries it uses
/// included: see <see cref="LocalizableText"/>. A library's own default language applies only in an application that
/// declares no languages.
/// </para>
/// <para>
/// To add a language, add it to the list: the build then reports every text without it, those of the libraries you
/// use included, so you can work through them. Each language is a culture name in any letter case, neutral
/// (<c>en</c>) or specific (<c>en-GB</c>).
/// </para>
/// <para>
/// A specific language is a language of its own to the build, also next to its neutral one: with <c>en</c> and
/// <c>en-GB</c> listed, every inline text takes <c>enGB:</c>, and every class of texts gets a British class beside its
/// English one, which can derive from it and add nothing. Leave <c>en-GB</c> out, and its users see your English.
/// </para>
/// <para>
/// An assembly without this attribute has no texts of its own to translate: its strings convert to texts as they are,
/// and <c>LocalizableText.Translated</c> does not exist there. It can still reword a library's set, or give a library's
/// contract another language.
/// </para>
/// <para>
/// The build reports a mistake as <c>LOC001</c> to <c>LOC016</c>, which
/// <see href="https://github.com/code-chops/Idioma#what-the-build-rejects">the README</see> lists. A test project that
/// compares texts with plain strings can turn off <c>LOC011</c>, <c>LOC012</c>, and <c>LOC013</c> with <c>NoWarn</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In any file of the assembly, after its usings:
/// [assembly: SupportedLanguages("en", "nl")]
///
/// public static class ShopTexts
/// {
///     public static LocalizableText Checkout { get; } = LocalizableText.Translated(en: "Checkout", nl: "Afrekenen");
/// }
/// </code>
/// </example>
// The source generators and the analyzer locate this type by its metadata-name string, and read its constructor
// arguments by position: renamed, moved, or reordered, they would silently stop finding the languages.
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class SupportedLanguagesAttribute : Attribute
{
    /// <summary>
    /// The default language, the first one listed, such as <c>en</c>.
    /// </summary>
    /// <remarks>
    /// In an application, every text falls back to it: see <see cref="LocalizableText"/>.
    /// </remarks>
    public string DefaultLanguage { get; }

    /// <summary>
    /// Every language listed, the default language first, such as <c>en</c> and <c>nl</c>. To offer the user a choice
    /// of your app's languages, read <see cref="TranslationsRegistry.SupportedLanguages"/>.
    /// </summary>
    public IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// Declares the languages of this assembly, the default language first.
    /// </summary>
    /// <param name="defaultLanguage">
    /// The default language, such as <c>en</c>.
    /// </param>
    /// <param name="otherLanguages">
    /// The other languages, such as <c>nl</c>: every text must exist in each of them too.
    /// </param>
    public SupportedLanguagesAttribute(string defaultLanguage, params string[] otherLanguages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLanguage);
        ArgumentNullException.ThrowIfNull(otherLanguages);

        this.DefaultLanguage = defaultLanguage;
        this.Languages = [defaultLanguage, .. otherLanguages];
    }
}
