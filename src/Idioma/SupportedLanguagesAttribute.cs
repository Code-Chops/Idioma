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
/// <item>One line per text: <c>LocalizableText.Translated(en: "Checkout", nl: "Afrekenen")</c>. The source generator
/// writes this method with one required argument per language, so leaving a language out does not compile.</item>
/// <item>One class per language, for a library or a larger group of texts: see <see cref="ITranslations"/>.</item>
/// </list>
/// <para>
/// Wording you write as a plain string where a text is expected is a build error (<c>LOC011</c>), so a text cannot end
/// up in one language by accident. A value that is no wording of yours, such as a customer's name, converts to a text.
/// An assembly without this attribute has no texts of its own to translate: its strings convert to texts as they are,
/// and <c>LocalizableText.Translated</c> does not exist there. It can still reword a library's set, or give a library's
/// contract another language.
/// </para>
/// <para>
/// In an application, the first language is also the one every text falls back to, the texts of the libraries the
/// application uses included (<see cref="TranslationsRegistry.DefaultLanguage"/>). A culture none of the application's
/// languages serves gets it straight away, even where a library has that culture's language, so no screen shows two
/// languages. A library's own default language applies only in an application that declares no languages.
/// </para>
/// <para>
/// To add a language, add it to the list: the build then reports every text without it, those of the libraries you
/// use included, so you can work through them. Each language is a culture name in any letter case, neutral
/// (<c>nl</c>) or specific (<c>nl-BE</c>). A set of this assembly takes its language from the end of its name, a code
/// listed here that is not two letters in PascalCase: <c>ShopTranslationsNlBE</c> serves <c>nl-BE</c>.
/// </para>
/// <para>
/// The build reports these mistakes:
/// </para>
/// <list type="bullet">
/// <item><c>CS7036</c>: a <c>Translated</c> call without one of the languages.</item>
/// <item><c>CS0535</c>: a set without one of the texts.</item>
/// <item><c>LOC001</c>: a set in an assembly without this attribute.</item>
/// <item><c>LOC002</c>: a default-language set the generator cannot extend: not partial, not named after its language,
/// nested, file-local, generic, or abstract.</item>
/// <item><c>LOC003</c> (warning): a set that cannot register itself, because it has no parameterless constructor or
/// no language code, or because it rewords another assembly's texts from inside another class or a file-local
/// class.</item>
/// <item><c>LOC004</c>: a language without a set.</item>
/// <item><c>LOC005</c>: a set for a language that is not listed, also one you register yourself.</item>
/// <item><c>LOC006</c>: a set deriving from a set of another language.</item>
/// <item><c>LOC007</c>: a set that does not implement its contract.</item>
/// <item><c>LOC008</c>: a language a library you use has no set for; add that set to your assembly.</item>
/// <item><c>LOC009</c> (warning): a language a library's one-line texts lack, which you cannot add from outside.</item>
/// <item><c>LOC010</c>: an invalid or repeated language in this attribute.</item>
/// <item><c>LOC011</c>: wording written in one language, such as <c>"Orders"</c> or <c>$"Order {number}"</c>, in C#
/// and in Razor.</item>
/// <item><c>LOC012</c>: calling by hand what only generated code should call.</item>
/// <item><c>LOC013</c>: a language argument without its name, which could swap two languages unnoticed.</item>
/// <item><c>LOC014</c>: a text named <c>ResourceKeyPrefix</c>, or named like the generated class.</item>
/// <item><c>LOC015</c>: a <c>LanguageCode</c> that implements nothing, on a set deriving from another set without
/// listing <see cref="ITranslations"/> again.</item>
/// <item><c>LOC016</c>: placeholders differing between languages, as in
/// <c>Translated(en: "Hello, {0}!", nl: "Hallo!")</c>, or in a set.</item>
/// </list>
/// <para>
/// <c>LOC003</c>, <c>LOC006</c>, <c>LOC015</c>, and <c>LOC016</c> apply in an assembly without this attribute too. A
/// test project that compares texts with plain strings can turn off <c>LOC011</c>, <c>LOC012</c>, and <c>LOC013</c>
/// with <c>NoWarn</c>.
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
    /// In an application, every text falls back to it, the texts of the libraries the application uses included
    /// (<see cref="TranslationsRegistry.DefaultLanguage"/>): for a culture a text has no wording for, and for every
    /// culture none of the application's languages serves. A library's own default language applies only in an
    /// application that declares no languages.
    /// </remarks>
    public string DefaultLanguage { get; }

    /// <summary>
    /// Every language listed, the default language first, such as <c>en</c> and <c>nl</c>. To offer the user a choice
    /// of your app's languages, read <see cref="TranslationsRegistry.SupportedLanguages"/>, which needs no reflection.
    /// </summary>
    public IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// Declares the languages of this assembly, the default language first.
    /// </summary>
    /// <param name="defaultLanguage">
    /// The default language, such as <c>en</c>: in an application, the language every text falls back to, the texts of
    /// the libraries it uses included.
    /// </param>
    /// <param name="otherLanguages">
    /// The other languages, such as <c>nl</c>: every text must exist in each of them too.
    /// </param>
    public SupportedLanguagesAttribute(string defaultLanguage, params string[] otherLanguages)
    {
        ArgumentNullException.ThrowIfNull(defaultLanguage);
        ArgumentNullException.ThrowIfNull(otherLanguages);

        this.DefaultLanguage = defaultLanguage;
        this.Languages = [defaultLanguage, .. otherLanguages];
    }
}
