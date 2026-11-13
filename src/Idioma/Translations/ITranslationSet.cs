namespace CodeChops.Idioma;

/// <summary>
/// The texts of an app or library in one language, called a set: a class with one string property per text, such as
/// <c>ShopTranslationsNL</c> with the Dutch wording of every shop text. The build makes sure that every language you
/// support has a set, and that every set has every text.
/// </summary>
/// <remarks>
/// <para>
/// Use a set per language for a library or a larger group of texts. A single text is simpler inline:
/// <c>LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")</c>. Either way, start by declaring your languages,
/// the default one first: <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages("en", "nl")]</see>.
/// Then:
/// </para>
/// <list type="number">
/// <item>Write the set for your default language as a partial class named after that language, with one public
/// string property per text: <c>public partial class ShopTranslationsEN : ITranslationSet</c>.</item>
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
/// A set takes its language from the end of its name (see <see cref="LanguageCode"/>), and a user sees the set for
/// their UI culture, or else a fallback: see <see cref="LocalizableText"/>.
/// </para>
/// <para>
/// There is nothing to register: every set registers itself. That takes a top-level class that is not generic, with a
/// parameterless constructor. A set without such a constructor gets build warning <c>LOC003</c>. A nested, generic, or
/// file-local class is no set to the build, which reports its language as missing (<c>LOC004</c>). To register a set
/// yourself, mark it
/// <see cref="ExcludeFromTranslationDiscoveryAttribute">[ExcludeFromTranslationDiscovery]</see> and call
/// <see cref="TranslationsRegistry.Register{TSet}">TranslationsRegistry.Register</see>.
/// </para>
/// <para>
/// To reword some of a library's texts, derive from its set in the same language, name your set after that language
/// too, and override those texts: <c>public sealed class MyShopTranslationsEN : ShopTranslationsEN</c>. A library
/// allows this by leaving its sets unsealed and its texts virtual. Your set then:
/// </para>
/// <list type="bullet">
/// <item>Wins over the library's own set, for the texts you override.</item>
/// <item>Reaches the library's regional sets: British users see your wording too, unless the library's British set
/// has wording of its own for that text.</item>
/// <item>Adds up with other rewordings: when a shared library of yours overrides <c>Checkout</c> and your app
/// overrides <c>Greeting</c>, both show. When both override the same text, the set registered last wins.</item>
/// </list>
/// <para>
/// When your app declares a language that a library lacks, the build names the set to add (<c>LOC008</c>): implement
/// the library's contract in your app, <c>public class ShopTranslationsDE : IShopTranslations</c>.
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
/// public partial class ShopTranslationsEN : ITranslationSet
/// {
///     public virtual string Cart => "Cart";
///     public virtual string Checkout => "Checkout";
///     public virtual string Greeting => "Hello, {0}!";
/// }
///
/// public class ShopTranslationsNL : IShopTranslations
/// {
///     public virtual string Cart => "Winkelwagen";
///     public virtual string Checkout => "Afrekenen";
///     public virtual string Greeting => "Hallo, {0}!";
/// }
///
/// // For a user whose UI culture is nl-NL: "Afrekenen" and "Hallo, Anna!".
/// string checkout = ShopTranslations.Checkout;
/// string greeting = ShopTranslations.Greeting.Format(customer.Name);
/// </code>
/// </example>
public interface ITranslationSet
{
    /// <summary>
    /// The language this set serves, such as <c>nl</c>. A set named after its language does not implement this: the
    /// end of its name gives it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The code is a culture name in any letter case, and the set serves that culture and every culture under it:
    /// <c>en</c> serves <c>en-US</c> and <c>en-GB</c>. A set for <c>en-GB</c> serves only British English, and wins
    /// over the <c>en</c> set there.
    /// </para>
    /// <para>
    /// An assembly that lists <c>en-GB</c> in <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see>
    /// needs such a set in every contract (<c>LOC004</c>), as each of its inline texts takes a British wording. A set
    /// with nothing British of its own derives from the English one and adds nothing.
    /// </para>
    /// <para>
    /// A name gives the code in one of two ways:
    /// </para>
    /// <list type="bullet">
    /// <item>Its last two letters: <c>ShopTranslationsNL</c> serves <c>nl</c>.</item>
    /// <item>A longer code that the assembly's
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see> lists, written in PascalCase:
    /// <c>ShopTranslationsEnGB</c> serves <c>en-GB</c>, and <c>ShopTranslationsZhHans</c> serves <c>zh-Hans</c>. So
    /// does a set deriving from such a set.</item>
    /// </list>
    /// <para>
    /// A longer code counts only when listed, as a name alone cannot tell <c>ShopTranslationsEnGB</c> (British English)
    /// from <c>AdminUiNL</c> (Dutch). When it is not listed, the last two letters count: <c>ShopTranslationsEnGB</c>
    /// serves <c>gb</c>, which is no language, and the build reports <c>LOC005</c>.
    /// </para>
    /// <para>
    /// Implement this property for such a code in an assembly that does not list it, or for a set whose name does
    /// not end in its language: <c>string ITranslationSet.LanguageCode => "en-GB";</c>. A set deriving from another set
    /// lists <see cref="ITranslationSet"/> again for that, as in the example below. Without it, the property implements
    /// nothing, and the build reports <c>LOC015</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In an assembly declaring ("en", "nl"): en-GB is left out, so inline texts need no British wording.
    /// public class ShopTranslationsEnGB : ShopTranslationsEN, ITranslationSet
    /// {
    ///     string ITranslationSet.LanguageCode => "en-GB";
    ///
    ///     public override string Cart => "Basket";
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// When this property is not implemented and the set's name does not end in a language, such as a name ending in a
    /// digit. The build warns about such a set with <c>LOC003</c>. Name the set after its language
    /// (<c>ShopTranslationsDE</c>), or implement this property.
    /// </exception>
    string LanguageCode => GetLanguageCode(this.GetType());

    /// <summary>
    /// The language a set's name gives it, such as <c>de</c> for <c>ShopTranslationsDE</c>: what
    /// <see cref="LanguageCode"/> returns for a set that does not implement it.
    /// </summary>
    /// <remarks>
    /// To get the language a set actually serves, read <see cref="LanguageCode"/> on the set, which also explains how
    /// a name gives a language.
    /// </remarks>
    /// <param name="setType">The type of the set.</param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="setType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When the name does not end in a language. Name the set after its language, or implement
    /// <see cref="LanguageCode"/>.
    /// </exception>
    static string GetLanguageCode(Type setType)
    {
        ArgumentNullException.ThrowIfNull(setType);

        // Recorded by the generated registrar of the set's assembly, which runs before any of its code: a name ending in
        // a longer code that assembly declares, on the set or a set it derives from.
        if (TranslationsRegistry.FindLanguageCode(setType) is { } recorded)
            return recorded;

        // ASCII letters only: culture names are ASCII, so a suffix of other letters would file the set under a language
        // no UI culture resolves through, without a word of warning.
        var name = setType.Name;
        if (name.Length < 2 || !Char.IsAsciiLetter(name[^1]) || !Char.IsAsciiLetter(name[^2]))
            throw new InvalidOperationException(
                $"Cannot derive a language code from the name of {setType}: name the type with the language code as suffix "
                + $"(e.g., ShopTranslationsDE for German, or ShopTranslationsEnGB for a declared en-GB) or implement {nameof(ITranslationSet)}.{nameof(LanguageCode)}.");

        return name[^2..].ToLowerInvariant();
    }
}
