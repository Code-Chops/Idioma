namespace CodeChops.Idioma;

/// <summary>
/// The texts of an app or library in one language: a class with one string property per text, such as
/// <c>ShopTranslationsNL</c> with the Dutch wording of every shop text. The build makes sure that every language you
/// support has a set, and that every set has every text.
/// </summary>
/// <remarks>
/// <para>
/// Use a set per language for a library or a larger group of texts. A single text is simpler in one line:
/// <c>LocalizableText.Translated(en: "Checkout", nl: "Afrekenen")</c>. Either way, start by declaring your languages,
/// the default one first: <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages("en", "nl")]</see>.
/// Then:
/// </para>
/// <list type="number">
/// <item>Write the set for your default language as a partial class named after that language, with one public
/// string property per text: <c>public partial class ShopTranslationsEN : ITranslations</c>.</item>
/// <item>Build. The source generator writes the contract <c>IShopTranslations</c>, which lists every text, and the
/// class <c>ShopTranslations</c>, which holds them.</item>
/// <item>Write a set for each other language that implements the contract:
/// <c>public class ShopTranslationsNL : IShopTranslations</c>. A missing set is build error <c>LOC004</c>, and a
/// missing text is build error <c>CS0535</c>.</item>
/// <item>Show a text through the generated class: <c>@ShopTranslations.Checkout</c> in markup, or
/// <c>ShopTranslations.Checkout</c> wherever a <see cref="LocalizableText"/> or a <see cref="string"/> is
/// expected.</item>
/// </list>
/// <para>
/// A user sees the set whose language matches <see cref="CultureInfo.CurrentUICulture"/>, or else its parent culture:
/// <c>ShopTranslationsNL</c> serves <c>nl-NL</c> and <c>nl-BE</c> alike. A culture without a set, or one your app does
/// not declare, gets your app's default language, the sets of the libraries it uses included, or else the default
/// language of the contract. A set takes its language from the end of its name: the last two letters
/// (<c>ShopTranslationsNL</c>), or a longer code your assembly declares (<c>ShopTranslationsNlBE</c> for
/// <c>nl-BE</c>); see <see cref="LanguageCode"/>.
/// </para>
/// <para>
/// There is nothing to register: every set registers itself. An app registers the sets of every assembly it
/// references when it starts, and an assembly it loads later registers its sets when its code first runs. That takes a
/// top-level class that is not generic, with a parameterless constructor; the build warns with <c>LOC003</c> when a set
/// cannot register itself. To decide yourself when a set applies, mark it
/// <see cref="ExcludeFromTranslationDiscoveryAttribute">[ExcludeFromTranslationDiscovery]</see> and register it with
/// <see cref="TranslationsRegistry.Register{TTranslations}">TranslationsRegistry.Register</see>.
/// </para>
/// <para>
/// To reword some of a library's texts, derive from its set in the same language, name your set after that language
/// too, and override those texts: <c>public sealed class MyShopTranslationsEN : ShopTranslationsEN</c>. Your set wins
/// over the library's own, and reaches its regional sets too: British users see your wording unless the library's
/// British set gives that text other wording than its English set. Rewordings add up: a text shows the last rewording
/// that changes it, so a shared library of yours and your app can each reword other texts of one set. A library makes
/// this possible by leaving its sets unsealed and its texts virtual. When your app declares a language that a library
/// lacks, the build names the set to add (<c>LOC008</c>): implement the library's contract in your app,
/// <c>public class ShopTranslationsDE : IShopTranslations</c>.
/// </para>
/// <para>
/// One instance of each set serves every user, so give each text fixed wording. Put a value that changes, such as a
/// customer's name, in a placeholder (<c>"Hello, {0}!"</c>) and fill it in with
/// <see cref="LocalizableText.Format(ReadOnlySpan{object})">Format</see>. Give each text the placeholders of its
/// default-language wording: the build checks it (<c>LOC016</c>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [assembly: SupportedLanguages("en", "nl")]
///
/// public partial class ShopTranslationsEN : ITranslations
/// {
///     public virtual string Checkout => "Checkout";
///     public virtual string Greeting => "Hello, {0}!";
/// }
///
/// public class ShopTranslationsNL : IShopTranslations
/// {
///     public virtual string Checkout => "Afrekenen";
///     public virtual string Greeting => "Hallo, {0}!";
/// }
///
/// // For a user whose UI culture is nl-NL: "Afrekenen" and "Hallo, Anna!".
/// string checkout = ShopTranslations.Checkout;
/// string greeting = ShopTranslations.Greeting.Format(customer.Name);
/// </code>
/// </example>
public interface ITranslations
{
    /// <summary>
    /// The language this set serves, such as <c>nl</c>. A set named after its language does not implement this: the
    /// end of its name gives it, so <c>ShopTranslationsNL</c> serves <c>nl</c>, and <c>ShopTranslationsNlBE</c> serves
    /// <c>nl-BE</c> in an assembly whose <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see>
    /// lists <c>nl-BE</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The code is a culture name in any letter case, and the set serves that culture and every culture under it:
    /// <c>nl</c> serves <c>nl-NL</c> and <c>nl-BE</c>. A set for <c>nl-BE</c> serves only Belgian Dutch, and wins
    /// over the <c>nl</c> set there.
    /// </para>
    /// <para>
    /// A code that is not two letters, such as <c>nl-BE</c>, <c>zh-Hans</c>, or <c>fil</c>, comes from the name only
    /// when the assembly declares it, as a name alone cannot tell <c>ShopTranslationsNlBE</c> (Belgian Dutch) from
    /// <c>AdminUiNL</c> (Dutch). The build then records the code for the set, and a set deriving from it serves the
    /// same language. Without the declaration the last two letters count: <c>ShopTranslationsNlBE</c> serves
    /// <c>be</c>, and the build reports <c>LOC005</c>.
    /// </para>
    /// <para>
    /// Implement this property for such a code in an assembly that does not declare it, or for a set whose name does
    /// not end in its language: <c>string ITranslations.LanguageCode => "nl-BE";</c>. A set deriving from another set
    /// lists <see cref="ITranslations"/> again for that, as in the example below. Without it, the property implements
    /// nothing, and the build reports <c>LOC015</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In an assembly declaring ("en", "nl"): nl-BE is left out, so one-line texts need no Belgian wording.
    /// public class ShopTranslationsNlBE : ShopTranslationsNL, ITranslations
    /// {
    ///     string ITranslations.LanguageCode => "nl-BE";
    ///
    ///     public override string Checkout => "Betalen";
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// When this property is not implemented and the set's name ends neither in two letters from A to Z nor in a longer
    /// code its assembly declares, such as a name ending in a digit. The build warns about such a set with
    /// <c>LOC003</c>. Name the set after its language (<c>ShopTranslationsDE</c>), or implement this property.
    /// </exception>
    string LanguageCode => GetLanguageCode(this.GetType());

    /// <summary>
    /// The language a set's name gives it: a longer code its assembly declares that the name ends in, which the build
    /// records for the set and for every set deriving from it (<c>ShopTranslationsNlBE</c> gives <c>nl-BE</c>), else
    /// the last two letters, in lower case (<c>ShopTranslationsDE</c> gives <c>de</c>). This is what
    /// <see cref="LanguageCode"/> returns for a set that does not implement it. To get the language a set actually
    /// serves, read <see cref="LanguageCode"/> on the set.
    /// </summary>
    /// <param name="translationsType">The type of the set.</param>
    /// <returns>
    /// The language code.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// When the name ends neither in two letters from A to Z nor in a longer code its assembly declares. Name the set
    /// after its language, or implement <see cref="LanguageCode"/>.
    /// </exception>
    static string GetLanguageCode(Type translationsType)
    {
        ArgumentNullException.ThrowIfNull(translationsType);

        // Recorded by the generated registrar of the set's assembly, which runs before any of its code: a name ending in
        // a longer code that assembly declares, on the set or a set it derives from.
        if (TranslationsRegistry.FindLanguageCode(translationsType) is { } recorded)
            return recorded;

        // ASCII letters only: culture names are ASCII, so a suffix of other letters would file the set under a language
        // no UI culture resolves through, without a word of warning.
        var name = translationsType.Name;
        if (name.Length < 2 || !Char.IsAsciiLetter(name[^1]) || !Char.IsAsciiLetter(name[^2]))
        {
            throw new InvalidOperationException(
                $"Cannot derive a language code from the name of {translationsType}: name the type with the language code as suffix "
                + $"(e.g., ShopTranslationsDE for German, or ShopTranslationsNlBE for a declared nl-BE) or implement {nameof(ITranslations)}.{nameof(LanguageCode)}.");
        }

        return name[^2..].ToLowerInvariant();
    }
}
