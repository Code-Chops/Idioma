namespace CodeChops.Idioma;

/// <summary>
/// A set holds the texts of an app or library in one language. It's a class with one string property per text, or a
/// method for a text that takes values. For example, <c>ShopLocalizationNl</c> holds the Dutch wording of every shop
/// text. The build makes sure every language you support has a set, and every set has every text.
/// </summary>
/// <remarks>
/// <para>
/// Use a set per language for a library or a larger group of texts. A single text is simpler inline, as in
/// <c>LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")</c>. Either way, first declare your languages, the
/// default one first: <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages("en", "nl")]</see>.
/// Then:
/// </para>
/// <list type="number">
/// <item>Write the set for your default language. Make it a class named after that language, with one public string
/// property per text. It lists this interface and the contract the source generator writes from it:
/// <c>public class ShopLocalizationEn : ILocalizationSet, IShopLocalization</c>.</item>
/// <item>Build. The source generator writes the contract <c>IShopLocalization</c>, which lists every text, and the
/// generated class <c>ShopLocalization</c>, which holds them.</item>
/// <item>Write a set for each other language that lists the same two interfaces:
/// <c>public class ShopLocalizationNl : ILocalizationSet, IShopLocalization</c>. A missing set is build error
/// <c>LOC201</c>. A missing text is build error <c>CS0535</c>.</item>
/// <item>Show a text through the generated class. Write <c>@ShopLocalization.Checkout</c> in markup, or
/// <c>ShopLocalization.Checkout</c> wherever a <see cref="LocalizableText"/> or a <see cref="string"/> is
/// expected.</item>
/// </list>
/// <para>
/// Every set lists both interfaces, because the contract doesn't derive from this one. A set that lists only this
/// interface is build error <c>LOC204</c>, and your IDE adds the contract.
/// </para>
/// <para>
/// Only a class that implements this interface is a set, whatever its name. A class that implements only a contract,
/// such as a test double, isn't one. A set that derives from another set implements this interface through it.
/// </para>
/// <para>
/// A set takes its language from the end of its name. See <see cref="LocalizationLanguageCode"/>. A user sees the set for their UI
/// culture, or else a fallback. See <see cref="LocalizableText"/>.
/// </para>
/// <para>
/// There's nothing to register, because every set is registered automatically. For that, a set must be a class that:
/// </para>
/// <list type="bullet">
/// <item>Is top-level.</item>
/// <item>Isn't generic.</item>
/// <item>Isn't file-local.</item>
/// <item>Has a parameterless constructor.</item>
/// </list>
/// <para>
/// The build doesn't recognize a class that breaks one of the first three as a set, and reports its language as
/// missing (<c>LOC201</c>). A set without such a constructor gets build warning <c>LOC400</c>. To register a set
/// yourself, mark it
/// <see cref="ExcludeFromLocalizationDiscoveryAttribute">[ExcludeFromLocalizationDiscovery]</see> and call
/// <see cref="LocalizationRegistry.Register{TSet}">LocalizationRegistry.Register</see>.
/// </para>
/// <para>
/// To reword some of a library's texts, derive from its set in the same language and override those texts. Name your
/// set after that language too: <c>public sealed class MyShopLocalizationEn : ShopLocalizationEn</c>. A library allows
/// this by leaving its sets unsealed and its texts virtual. Your set then:
/// </para>
/// <list type="bullet">
/// <item>Wins over the library's own set, for the texts you override.</item>
/// <item>Reaches the library's regional sets. British users see your wording too, unless the library's British set
/// has wording of its own for that text.</item>
/// <item>Adds up with other rewordings. When a shared library of yours overrides <c>Checkout</c> and your app
/// overrides <c>Greeting</c>, both show. When both override the same text, the set registered last wins.</item>
/// </list>
/// <para>
/// When your app declares a language that a library lacks, the build names the set to add (<c>LOC600</c>). Implement
/// the library's contract in your app:
/// <c>public class ShopLocalizationDe : ILocalizationSet, IShopLocalization</c>.
/// </para>
/// <para>
/// <b>Texts with parameters.</b> Give each property fixed wording, because one instance of each set serves every user.
/// A property with a setter is build error <c>LOC206</c>, and your IDE removes the setter. A text with a value that
/// changes, such as a customer's name, is a public method. It takes the value as a parameter and returns the wording:
/// <c>public string Greeting(string name) => $"Hello, {name}!";</c>. The contract repeats the method, and the generated
/// class gets one with the same name and parameters. So you show the text as
/// <c>ShopLocalization.Greeting(customer.Name)</c>. Its values have a name and a type, and its wording can depend on
/// them, as for a plural: <c>count is 1 ? "1 item" : $"{count} items"</c>. A method without parameters isn't a text.
/// </para>
/// <para>
/// The build checks these methods:
/// </para>
/// <list type="bullet">
/// <item>Every language implements the method with the parameters of the default-language set, named alike
/// (<c>LOC301</c>).</item>
/// <item>Every language uses every value the default wording shows (<c>LOC302</c>).</item>
/// <item>A method the generated class can't repeat, such as a generic one, is build error <c>LOC300</c>.</item>
/// <item>A placeholder such as <c>{0}</c> in a wording is build warning <c>LOC502</c>, because nothing fills it in.
/// Your IDE turns such a text into a method that takes the value.</item>
/// </list>
/// <para>
/// Mark a public member that isn't a text, such as a helper another class calls, with
/// <see cref="ExcludeFromLocalizationDiscoveryAttribute">[ExcludeFromLocalizationDiscovery]</see>.
/// </para>
/// <para>
/// <b>Texts by key.</b> The generated class also gets the method <c>GetTextByKey</c>, for a text your code only knows by its
/// name at run time: <c>ShopLocalization.GetTextByKey("Checkout")</c>. The key is the text's
/// <see cref="LocalizableText.Key"/>. <c>GetTextByKey</c> returns the text, the same one as
/// <c>ShopLocalization.Checkout</c>. A text with parameters takes its values after the key, as in
/// <c>ShopLocalization.GetTextByKey("Greeting", customer.Name)</c>. A key no text has throws
/// <see cref="KeyNotFoundException"/>. No text can be named <c>GetTextByKey</c> (<c>LOC205</c>).
/// </para>
/// <para>
/// <b>Enums.</b> To translate an enum, list <see cref="ILocalizationSet{TEnum}"/> in place of this interface. Every
/// member of the enum then needs a text in every language.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [assembly: SupportedLanguages("en", "nl")]
///
/// public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
/// {
///     public string Cart => "Cart";
///     public string Checkout => "Checkout";
///     public string Greeting(string name) => $"Hello, {name}!";
///     public string ItemsInCart(int count) => count is 1 ? "1 item in your cart" : $"{count} items in your cart";
/// }
///
/// public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
/// {
///     public string Cart => "Winkelwagen";
///     public string Checkout => "Afrekenen";
///     public string Greeting(string name) => $"Hallo, {name}!";
///     public string ItemsInCart(int count) => count is 1 ? "1 artikel in je winkelwagen" : $"{count} artikelen in je winkelwagen";
/// }
///
/// // For a user whose UI culture is nl-NL: "Afrekenen", "Hallo, Anna!", and "3 artikelen in je winkelwagen".
/// string checkout = ShopLocalization.Checkout;
/// string greeting = ShopLocalization.Greeting(customer.Name);
/// string items = ShopLocalization.ItemsInCart(3);
/// </code>
/// </example>
public interface ILocalizationSet
{
    /// <summary>
    /// The language this set serves, such as <c>nl</c>. A set named after its language doesn't implement this, because
    /// the end of its name gives it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The code is a culture name in any letter case. The set serves that culture and every culture under it, so
    /// <c>en</c> serves <c>en-US</c> and <c>en-GB</c>. A set for <c>en-GB</c> serves only British English, and wins
    /// over the <c>en</c> set there. A constant that isn't a culture name, such as <c>en_GB</c>, is build
    /// error <c>LOC402</c>.
    /// </para>
    /// <para>
    /// An assembly that lists <c>en-GB</c> in <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see>
    /// needs such a set in every contract (<c>LOC201</c>), because each of its inline texts takes a British wording. A
    /// set with nothing British of its own derives from the English one and adds nothing.
    /// </para>
    /// <para>
    /// A name ends in the code in PascalCase, and gives it in one of two ways:
    /// </para>
    /// <list type="bullet">
    /// <item>Its last two letters: <c>ShopLocalizationNl</c> serves <c>nl</c>.</item>
    /// <item>A longer code that the assembly's
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see> lists:
    /// <c>ShopLocalizationEnGb</c> serves <c>en-GB</c>, and <c>ShopLocalizationZhHans</c> serves <c>zh-Hans</c>. So
    /// does a set that derives from such a set.</item>
    /// </list>
    /// <para>
    /// The code starts with a capital. After it, the code matches in any letter case, so <c>ShopLocalizationNL</c>
    /// and <c>ShopLocalizationEnGB</c> work too. The capital marks where the code begins, so an Arabic set named
    /// <c>MessagesAr</c> serves <c>ar</c>, not <c>es-AR</c>. A name without it has no code: <c>LocalizationSet</c>
    /// doesn't serve Estonian (<c>et</c>).
    /// </para>
    /// <para>
    /// A longer code counts only when listed, because a name alone can't tell <c>ShopLocalizationEnGb</c> (British
    /// English) from <c>AdminUiNl</c> (Dutch). When it isn't listed, the last two letters count. Then
    /// <c>ShopLocalizationEnGb</c> serves <c>gb</c>, which isn't a language, and the build reports <c>LOC202</c>.
    /// </para>
    /// <para>
    /// Implement this property in two cases: for such a code in an assembly that doesn't list it, or for a set whose
    /// name doesn't end in its language. For example: <c>string ILocalizationSet.LocalizationLanguageCode => "en-GB";</c>. A set
    /// that derives from another set lists <see cref="ILocalizationSet"/> again for that, as in the example below.
    /// Without it, the property implements nothing, and the build reports <c>LOC401</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In an assembly declaring ("en", "nl"): en-GB is left out, so inline texts need no British wording.
    /// // Cart is virtual in ShopLocalizationEn, so that this set can override it.
    /// public class ShopLocalizationEnGb : ShopLocalizationEn, ILocalizationSet
    /// {
    ///     string ILocalizationSet.LocalizationLanguageCode => "en-GB";
    ///
    ///     public override string Cart => "Basket";
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// When this property isn't implemented and the set's name doesn't end in a language, such as a name ending in a
    /// digit. The build warns about such a set with <c>LOC400</c>. Name the set after its language
    /// (<c>ShopLocalizationDe</c>), or implement this property.
    /// </exception>
    string LocalizationLanguageCode => GetLanguageCode(this.GetType());

    /// <summary>
    /// The language a set's name gives it, such as <c>de</c> for <c>ShopLocalizationDe</c>. This is what
    /// <see cref="LocalizationLanguageCode"/> returns for a set that doesn't implement it.
    /// </summary>
    /// <remarks>
    /// To get the language a set actually serves, read <see cref="LocalizationLanguageCode"/> on the set. Its docs also explain how
    /// a name gives a language.
    /// </remarks>
    /// <param name="setType">The type of the set.</param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="setType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When the name doesn't end in a language. Name the set after its language, or implement
    /// <see cref="LocalizationLanguageCode"/>.
    /// </exception>
    static string GetLanguageCode(Type setType)
    {
        ArgumentNullException.ThrowIfNull(setType);

        // Recorded by the generated registrar of the set's assembly, which runs before any of its code. It holds the
        // language the build read from the name of every set it found, so the name below is read only for a set the
        // build didn't see, such as a nested one that derives from no set it saw.
        if (LocalizationRegistry.FindLanguageCode(setType) is { } recorded)
            return recorded;

        // ASCII letters only: culture names are ASCII, so a suffix of other letters would file the set under a language
        // no UI culture resolves through, without a word of warning. The capital marks where the code starts, as the
        // build reads it, so LocalizationSet has no language, where et would make it Estonian.
        var name = setType.Name;
        if (name.Length < 2 || !Char.IsAsciiLetter(name[^1]) || !Char.IsAsciiLetterUpper(name[^2]))
            throw new InvalidOperationException(
                $"'{setType}' has no language, because its name doesn't end in a language code that starts with a capital. End its name in one, such as "
                + "ShopLocalizationDe for German, or ShopLocalizationEnGb when its assembly declares en-GB. You can also implement "
                + $"{nameof(ILocalizationSet)}.{nameof(LocalizationLanguageCode)}.");

        var languageCode = name[^2..].ToLowerInvariant();
        return languageCode;
    }
}

/// <summary>
/// A set that holds the text of every member of an enum, in one language. Each text is a string property named after
/// its member. For example, <c>OrderStatusLocalizationNl</c> holds the Dutch wording of <c>OrderStatus.Pending</c>
/// and <c>OrderStatus.Shipped</c>. The build makes sure every set has a text for every member.
/// </summary>
/// <remarks>
/// <para>
/// Write the set as any other set (see <see cref="ILocalizationSet"/>), and list this interface in place of
/// <see cref="ILocalizationSet"/>: <c>public class OrderStatusLocalizationEn : ILocalizationSet&lt;OrderStatus&gt;,
/// IOrderStatusLocalization</c>. The source generator then adds every member of the enum to the contract. So a set
/// without a text for a member is build error <c>CS0535</c>, in every language. A member you add to the enum later
/// doesn't build until every language has its text.
/// </para>
/// <para>
/// The generated class holds a text per member, such as <c>OrderStatusLocalization.Shipped</c>. Its key is the name
/// of the member. The class also gets the method <c>GetTextByValue</c>, which gives the text of
/// a value: <c>OrderStatusLocalization.GetTextByValue(order.Status)</c>.
/// </para>
/// <para>
/// The rules for these texts:
/// </para>
/// <list type="bullet">
/// <item>The text of a member is a property, because it takes no values. A method with the name of a member is build
/// error <c>LOC300</c>.</item>
/// <item>No text can be named <c>GetTextByValue</c>, because the generated method has that name (<c>LOC205</c>).</item>
/// <item>Two members with the same value, such as <c>Default = 0</c> next to <c>None = 0</c>, each need a text.
/// <c>GetTextByValue</c> gives the text of the first one.</item>
/// <item><c>GetTextByValue</c> shows a value that isn't a member, such as a combination of flags, as
/// <see cref="Enum.ToString()"/> writes it.</item>
/// <item>The set can hold other texts too, such as a caption for the enum itself.</item>
/// </list>
/// <para>
/// Only the default-language set needs this interface, because the contract comes from it. A set of another language
/// may list <see cref="ILocalizationSet"/> instead.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public enum OrderStatus { Pending, Shipped }
///
/// public class OrderStatusLocalizationEn : ILocalizationSet&lt;OrderStatus&gt;, IOrderStatusLocalization
/// {
///     public string Pending => "Pending";
///     public string Shipped => "Shipped";
/// }
///
/// public class OrderStatusLocalizationNl : ILocalizationSet&lt;OrderStatus&gt;, IOrderStatusLocalization
/// {
///     public string Pending => "In behandeling";
///     public string Shipped => "Verzonden";
/// }
///
/// // For a user whose UI culture is nl-NL: "Verzonden".
/// string status = OrderStatusLocalization.GetTextByValue(OrderStatus.Shipped);
/// </code>
/// </example>
/// <typeparam name="TEnum">The enum whose members the set has a text for.</typeparam>
public interface ILocalizationSet<TEnum> : ILocalizationSet
    where TEnum : struct, Enum;
