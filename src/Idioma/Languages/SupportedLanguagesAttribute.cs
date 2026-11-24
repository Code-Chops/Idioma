namespace CodeChops.Idioma;

/// <summary>
/// Declares the languages of your assembly, the default language first.
/// For example: <c>[assembly: SupportedLanguages("en", "nl")]</c>.
/// The build then makes sure every text in the assembly exists in each of them.
/// </summary>
/// <remarks>
/// <para>
/// Put the attribute once in any file of the assembly. You can then write a text in three ways, and each demands every
/// language:
/// </para>
/// <list type="bullet">
/// <item>A set: one class per language, for a library or a larger group of texts. See
/// <see cref="ILocalizationSet"/>.</item>
/// <item>An inline text: <c>LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")</c>. The source generator
/// writes this method with one required argument per language, so leaving a language out doesn't compile.</item>
/// <item>An attribute on the type or the member it names, for the label of a type, a property or an enum member:
/// <c>[Localized(en: "City", nl: "Plaats")]</c>. The source generator writes the attribute with the same arguments
/// as <c>Inline</c>. <see cref="LocalizableText.GetTextByMember{T}(string)"/> gives the text of a member.</item>
/// </list>
/// <para>
/// Every text is a <see cref="string"/>: the wording for the user's language at the moment you read it. Read a text
/// where you show it, because a string kept in a static field stays in the language of the first user.
/// </para>
/// <para>
/// An application always declares its languages, even without texts of its own (<c>LOC103</c>), because they're the
/// languages its users get. Its first language is the fallback for every text, including the texts of the libraries
/// it uses. See <see cref="LocalizableText"/>. A library's languages only describe the library's own texts.
/// </para>
/// <para>
/// To add a language, add it to the list. The build then reports every text without it, including those of the
/// libraries you use, so you can work through them. Each language is a culture name in any letter case, neutral
/// (<c>en</c>) or specific (<c>en-GB</c>).
/// </para>
/// <para>
/// To the build, a specific language is a language of its own, also next to its neutral one. With <c>en</c> and
/// <c>en-GB</c> listed, every inline text takes <c>enGb:</c>. Every English set needs a British set next to it
/// (<c>LOC201</c>), which can derive from the English set and add nothing. Leave <c>en-GB</c> out, and its users see
/// your English.
/// </para>
/// <para>
/// A library without this attribute has no texts of its own to translate. Neither <c>LocalizableText.Inline</c> nor
/// <c>[Localized]</c> exists there. It can still reword a library's set, or give a library's contract another
/// language.
/// </para>
/// <para>
/// The build reports each mistake with a code, such as <c>LOC201</c>.
/// <see href="https://github.com/Code-Chops/Idioma/blob/main/docs/build-errors.md">What the build rejects</see> lists
/// every code with its fix.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In any file of the assembly, after its usings:
/// [assembly: SupportedLanguages("en", "nl")]
///
/// public static class ShopTexts
/// {
///     public static string Checkout => LocalizableText.Inline(en: "Checkout", nl: "Afrekenen");
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
    /// In an application, every text falls back to it. See <see cref="LocalizableText"/>.
    /// </remarks>
    public string DefaultLanguage { get; }

    /// <summary>
    /// Every language listed, the default language first. To let the user choose one of your app's languages,
    /// read <see cref="LocalizationRegistry.SupportedLanguages"/> instead.
    /// </summary>
    public IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// Declares the languages of this assembly, the default language first.
    /// </summary>
    /// <param name="defaultLanguage">
    /// The default language, such as <c>en</c>.
    /// </param>
    /// <param name="otherLanguages">
    /// The other languages, such as <c>nl</c>. Every text must exist in each of them too.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="defaultLanguage"/> or <paramref name="otherLanguages"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">When <paramref name="defaultLanguage"/> is empty or white space.</exception>
    public SupportedLanguagesAttribute(string defaultLanguage, params string[] otherLanguages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLanguage);
        ArgumentNullException.ThrowIfNull(otherLanguages);

        this.DefaultLanguage = defaultLanguage;
        this.Languages = [defaultLanguage, .. otherLanguages];
    }
}
