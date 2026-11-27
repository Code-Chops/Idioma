namespace CodeChops.Idioma;

/// <summary>
/// A set holds the texts of an app or library in one language. For example, <c>ShopLocalizationNl</c> holds the Dutch
/// wording of every shop text. The build makes sure every language has a set, and every set has every text.
/// </summary>
/// <remarks>
/// <para>
/// Declare your languages, the default one first:
/// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages("en", "nl")]</see>. Then:
/// </para>
/// <list type="number">
/// <item>Write the set for your default language, with one public string property per text. It lists this interface and
/// the contract: <c>public class ShopLocalizationEn : ILocalizationSet, IShopLocalization</c>.</item>
/// <item>Build. The source generator writes the contract <c>IShopLocalization</c> and the generated class
/// <c>ShopLocalization</c>.</item>
/// <item>Write a set for each other language that lists the same two interfaces. A missing set is build error
/// <c>LOC201</c>, and a missing text is <c>CS0535</c>.</item>
/// <item>Show a text through the generated class: <c>ShopLocalization.Checkout</c>. It gives the wording for the user's
/// current language, so read it where you show it.</item>
/// </list>
/// <para>
/// A single text is simpler inline: <c>LocalizedText.Inline(en: "Checkout", nl: "Afrekenen")</c>.
/// </para>
/// <para>
/// Every set lists both interfaces, because the contract doesn't derive from this one (<c>LOC204</c>). Only a class
/// that implements this interface is a set, so a test double of the contract isn't one.
/// </para>
/// <para>
/// A set takes its language from the end of its name (see <see cref="LocalizationLanguageCode"/>). A user sees the set
/// for their UI culture, or else a fallback (see <see cref="LocalizedText"/>).
/// </para>
/// <para>
/// Every set is registered automatically. For that, a set must be a class that:
/// </para>
/// <list type="bullet">
/// <item>Is top-level, not generic and not file-local. Otherwise the build doesn't discover it, and reports its
/// language as missing (<c>LOC201</c>).</item>
/// <item>Has a parameterless constructor (<c>LOC208</c>).</item>
/// </list>
/// <para>
/// To register a set yourself, mark it
/// <see cref="ExcludeFromLocalizationDiscoveryAttribute">[ExcludeFromLocalizationDiscovery]</see> and call
/// <see cref="LocalizationRegistry.Register{TSet}">LocalizationRegistry.Register</see>.
/// </para>
/// <para>
/// <b>Rewording a library.</b> Derive from the library's set in the same language and override the texts you reword:
/// <c>public sealed class MyShopLocalizationEn : ShopLocalizationEn</c>. This works when the library leaves its sets
/// unsealed and its texts virtual. British users see your wording too, unless the library's British set words that text
/// itself. Rewordings from several assemblies add up. When two override the same text, the set registered last wins.
/// </para>
/// <para>
/// When your app declares a language that a library lacks, add the library's set in your app (<c>LOC700</c>):
/// <c>public class ShopLocalizationDe : ILocalizationSet, IShopLocalization</c>.
/// </para>
/// <para>
/// <b>Texts with parameters.</b> Give each property fixed wording and no setter (<c>LOC301</c>), because one instance of
/// each set serves every user. A text with a value that changes is a public method that takes the value:
/// <c>public string Greeting(string name) => $"Hello, {name}!";</c>. The generated class repeats it, so you write
/// <c>ShopLocalization.Greeting(customer.Name)</c>. A method without parameters isn't a text. The build checks that:
/// </para>
/// <list type="bullet">
/// <item>Every language takes the parameters of the default-language set, with the same names (<c>LOC401</c>).</item>
/// <item>Every language uses every value the default wording shows (<c>LOC402</c>).</item>
/// <item>The generated class can repeat the method, so it isn't generic (<c>LOC400</c>).</item>
/// <item>No wording has a placeholder such as <c>{0}</c>, because nothing fills it in (<c>LOC403</c>).</item>
/// </list>
/// <para>
/// Mark a public member that isn't a text, such as a helper, with
/// <see cref="ExcludeFromLocalizationDiscoveryAttribute">[ExcludeFromLocalizationDiscovery]</see>.
/// </para>
/// <para>
/// <b>Texts by key.</b> Call <c>GetTextByKey</c> for a text your code knows only by its name at run time:
/// <c>ShopLocalization.GetTextByKey("Checkout")</c>. A text with parameters takes its values after the key:
/// <c>ShopLocalization.GetTextByKey("Greeting", customer.Name)</c>. The key is the text's name, in the same letter case.
/// An unknown key, such as one from a database whose text was renamed since, gives <see langword="null"/>. A
/// <see langword="null"/> key throws. <c>ShopLocalization.TextKeys</c> lists every key. No text can be named
/// <c>GetTextByKey</c> or <c>TextKeys</c> (<c>LOC300</c>).
/// </para>
/// <para>
/// <b>Enums.</b> To translate an enum, list <see cref="ILocalizationSet{TEnum}"/> in place of this interface.
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
///     public string ItemsInCart(int count) => count is 1
///         ? "1 item in your cart"
///         : $"{count} items in your cart";
/// }
///
/// public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
/// {
///     public string Cart => "Winkelwagen";
///     public string Checkout => "Afrekenen";
///     public string Greeting(string name) => $"Hallo, {name}!";
///     public string ItemsInCart(int count) => count is 1
///         ? "1 artikel in je winkelwagen"
///         : $"{count} artikelen in je winkelwagen";
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
    /// The language this set serves, such as <c>nl</c>. A set named after its language doesn't implement this.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The code is a culture name in any letter case. A code that isn't one, such as <c>en_GB</c>, is build error
    /// <c>LOC207</c>. A set for <c>en</c> serves <c>en-US</c> and <c>en-GB</c> too. A set for <c>en-GB</c> wins over it
    /// for British users.
    /// </para>
    /// <para>
    /// When an assembly lists <c>en-GB</c> in
    /// <see cref="SupportedLanguagesAttribute">[assembly: SupportedLanguages]</see>, every contract needs a British set
    /// (<c>LOC201</c>). A set with no British wording of its own derives from the
    /// English one and adds nothing.
    /// </para>
    /// <para>
    /// A set's name ends in its code in PascalCase:
    /// </para>
    /// <list type="bullet">
    /// <item>The last two letters give the code: <c>ShopLocalizationNl</c> serves <c>nl</c>.</item>
    /// <item>A longer code counts only when <c>[SupportedLanguages]</c> lists it: <c>ShopLocalizationEnGb</c> serves
    /// <c>en-GB</c>. Otherwise it serves <c>gb</c>, which isn't a language (<c>LOC202</c>).</item>
    /// <item>The code starts with a capital, so <c>LocalizationSet</c> doesn't serve Estonian (<c>et</c>). After that,
    /// letter case doesn't matter: <c>ShopLocalizationNL</c> works too.</item>
    /// </list>
    /// <para>
    /// Implement this property when the name can't give the code, as in the example. A set that derives from another
    /// set lists <see cref="ILocalizationSet"/> again for that, or the property implements nothing (<c>LOC206</c>).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In an assembly declaring ("en", "nl"), where en-GB isn't listed. Cart is virtual in ShopLocalizationEn.
    /// public class ShopLocalizationEnGb : ShopLocalizationEn, ILocalizationSet
    /// {
    ///     string ILocalizationSet.LocalizationLanguageCode => "en-GB";
    ///
    ///     public override string Cart => "Basket";
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// When the set doesn't implement this property and its name doesn't end in a language (<c>LOC208</c>).
    /// </exception>
    string LocalizationLanguageCode => GetLanguageCode(this.GetType());

    /// <summary>
    /// The language the build gave a set's class, such as <c>de</c> for <c>ShopLocalizationDe</c>, or else the language
    /// its name gives. <see cref="LocalizationLanguageCode"/> gives this, unless the set implements it.
    /// </summary>
    /// <remarks>
    /// The build gives each set it finds the language of its name, or the constant its
    /// <see cref="LocalizationLanguageCode"/> returns. So <c>ShopLocalizationEnGb</c> gets <c>en-GB</c> in an assembly
    /// that declares <c>en-GB</c>. A class the build didn't see, such as a nested one, gets the language of the nearest
    /// class it derives from that the build saw. Only a class without such a class takes the last two letters of its
    /// name.
    /// </remarks>
    /// <param name="setType">The type of the set.</param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="setType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// When neither the build nor the name gives the set a language.
    /// </exception>
    static string GetLanguageCode(Type setType)
    {
        ArgumentNullException.ThrowIfNull(setType);

        // Described by the generated registrar of the set's assembly, which runs before any of its code. It holds the
        // language the build read from the name of every set it found, so the name below is read only for a set the
        // build didn't see, such as a nested one that derives from no set it saw.
        if (LocalizationRegistry.GetDescribedLanguageCode(setType) is { } described)
            return described;

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
/// A set that holds a text for every member of an enum, in one language. For example,
/// <c>OrderStatusLocalizationNl</c> holds the Dutch wording of <c>OrderStatus.Pending</c> and
/// <c>OrderStatus.Shipped</c>.
/// </summary>
/// <remarks>
/// <para>
/// Write it as any other set, and list this interface in place of <see cref="ILocalizationSet"/> in every language
/// (<c>LOC205</c>). The contract then gets every member of the enum, so a missing text is build error <c>CS0535</c>.
/// </para>
/// <para>
/// The generated class gets a text per member, keyed by the member's name, and the method <c>GetTextByValue</c>:
/// <c>OrderStatusLocalization.GetTextByValue(order.Status)</c>. The rules:
/// </para>
/// <list type="bullet">
/// <item>A member's text is a property. A method with a member's name is build error <c>LOC400</c>.</item>
/// <item>No text can be named <c>GetTextByValue</c> (<c>LOC300</c>).</item>
/// <item>Members that share a value each need a text, and <c>GetTextByValue</c> gives the first one's. Naming another,
/// as in <c>OrderStatus.None.GetText()</c>, is build warning <c>LOC600</c>.</item>
/// <item>A value that isn't a member, such as combined flags, shows as <see cref="Enum.ToString()"/> writes it.</item>
/// <item>A text that isn't a member is build warning <c>LOC305</c>, because <c>GetTextByValue</c> never gives it.</item>
/// </list>
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
