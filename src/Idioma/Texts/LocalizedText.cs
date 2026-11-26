using System.Reflection;

namespace CodeChops.Idioma;

/// <summary>
/// Where you write and read the texts that carry their own wording: <c>LocalizedText.Inline</c> for a text written
/// where it's used, <see cref="GetTextByMember{T}(string)"/> for a text written on a member, and
/// <see cref="GetTextOfType{T}()"/> for a text written on a type. Each gives the wording for
/// <see cref="CultureInfo.CurrentUICulture"/> at the call.
/// </summary>
/// <remarks>
/// <para>
/// Start by declaring your languages, the default one first:
/// <c>[assembly: SupportedLanguages("en", "nl")]</c>. A text that lacks one of
/// them doesn't build. <see cref="SupportedLanguagesAttribute"/> lists the three ways to write a text: a set, an inline
/// text, and an attribute on a type or a member.
/// </para>
/// <para>
/// For wording that differs in a region, list the region next to its language:
/// <c>[assembly: SupportedLanguages("en", "nl", "en-GB")]</c>. Every text then needs British wording too. An inline
/// text takes <c>enGb: "Colour"</c>, and every English set needs a British set next to it. British users see that
/// wording, and other English users keep the <c>en</c> wording.
/// </para>
/// <para>
/// A text gives the first wording it finds, in this fallback order:
/// </para>
/// <list type="number">
/// <item>The wording for the user's UI culture, or else for its parent culture. An <c>en</c> wording serves
/// <c>en-US</c>, and <c>en-GB</c> too when you list no British wording.</item>
/// <item>The wording for your app's default language. A culture that none of your app's languages serves starts here,
/// even for a library's text that has that culture's language. So no screen ends up in two languages.</item>
/// <item>The wording for the text's own default language, that of the project that wrote it.</item>
/// </list>
/// <para>
/// Read a text where you show it. The string holds the wording of the moment it's made, so it doesn't follow a later
/// change of language. On a server, a string kept in a static field shows every user the language of the first one.
/// </para>
/// <para>
/// An app that keeps the user's culture somewhere other than <see cref="CultureInfo.CurrentUICulture"/> sets
/// <see cref="LocalizationRegistry.CurrentUICultureProvider"/>, and every text follows that instead.
/// </para>
/// <para>
/// To put a parameter's value in the wording, write it into every language:
/// <c>LocalizedText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")</c>. In a set, such a text is a method
/// that takes the value, such as <c>ShopLocalization.Greeting(customer.Name)</c>. Every language implements it with
/// the same parameters (see <see cref="ILocalizationSet"/>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [assembly: SupportedLanguages("en", "nl")]
///
/// public static class ShopTexts
/// {
///     public static string Checkout => LocalizedText.Inline(en: "Checkout", nl: "Afrekenen");
///     public static string Greeting(string name) => LocalizedText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!");
/// }
///
/// // For a user whose UI culture is nl-NL: "Afrekenen" and "Hallo, Anna!".
/// string checkout = ShopTexts.Checkout;
/// string greeting = ShopTexts.Greeting(customer.Name);
/// </code>
/// </example>
public static class LocalizedText
{
    /// <summary>
    /// The text of a member marked <c>[Localized]</c>, by the type that declares it and its name, or
    /// <see langword="null"/> for a member without one.
    /// <c>LocalizedText.GetTextByMember&lt;Address&gt;(nameof(Address.City))</c> gives the text of
    /// <c>Address.City</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A member is a property, a field, or a member of an enum. Mark it in the assembly that declares it:
    /// <c>[Localized(en: "City", nl: "Plaats")]</c>. The source generator reads the wording as the assembly builds,
    /// so nothing reads the attribute at run time.
    /// </para>
    /// <para>
    /// A class without a text for the member gets that of its nearest base class. So an override without
    /// <c>[Localized]</c> keeps the text of the property it overrides. An interface's texts aren't inherited.
    /// </para>
    /// <para>
    /// A type with type arguments, such as <c>Page&lt;Order&gt;</c>, gives the text of the member on
    /// <c>Page&lt;T&gt;</c>.
    /// </para>
    /// <para>
    /// The string is the wording for the current UI culture at the call. Read it where you show it.
    /// </para>
    /// <para>
    /// For a value of an enum, <c>status.GetText()</c> gives the text of its member, so you don't name it. For the
    /// text of a type itself, call <see cref="GetTextOfType{T}()"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type that declares the member, or one that derives from it.</typeparam>
    /// <param name="memberName">The name of the member, in the same letter case, such as <c>nameof(Address.City)</c>.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="memberName"/> is <see langword="null"/>.</exception>
    public static string? GetTextByMember<T>(string memberName)
    {
        ArgumentNullException.ThrowIfNull(memberName);

        var text = MemberTexts.Get<T>(memberName);
        return text;
    }

    /// <summary>
    /// The text of a member marked <c>[Localized]</c>, by a type your code only has at run time and the member's name,
    /// or <see langword="null"/> for a member without one. Where your code knows the type, write
    /// <see cref="GetTextByMember{T}(string)"/>.
    /// </summary>
    /// <remarks>
    /// A type with type arguments, such as <c>Page&lt;Order&gt;</c>, gives the text of the member on
    /// <c>Page&lt;T&gt;</c>.
    /// </remarks>
    /// <param name="type">The type that declares the member, or one that derives from it.</param>
    /// <param name="memberName">The name of the member, in the same letter case.</param>
    /// <exception cref="ArgumentNullException">
    /// When <paramref name="type"/> or <paramref name="memberName"/> is <see langword="null"/>.
    /// </exception>
    public static string? GetTextByMember(Type type, string memberName)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(memberName);

        var text = MemberTexts.Get(type, memberName);
        return text;
    }

    /// <summary>
    /// The text of a member your code holds, such as the <see cref="PropertyInfo"/> of a form field, or
    /// <see langword="null"/> for a member without one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the member's name and the type that declares it are read. A <see cref="Type"/> is a member too, and gives
    /// the text <see cref="GetTextOfType(Type)"/> gives.
    /// </para>
    /// <para>
    /// A member of a type with type arguments, such as <c>Page&lt;Order&gt;</c>, gives the text of that member on
    /// <c>Page&lt;T&gt;</c>.
    /// </para>
    /// </remarks>
    /// <param name="member">A property, a field, a member of an enum, or a type.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="member"/> is <see langword="null"/>.</exception>
    public static string? GetTextByMember(MemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (member is Type type)
            return MemberTexts.GetOwn(type);

        var text = member.DeclaringType is { } declaringType
            ? MemberTexts.Get(declaringType, member.Name)
            : null;

        return text;
    }

    /// <summary>
    /// The text of a type marked <c>[Localized]</c> itself, such as the name of an enum, or <see langword="null"/>
    /// for a type without one. <c>LocalizedText.GetTextOfType&lt;OrderStatus&gt;()</c> gives the text of
    /// <c>OrderStatus</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A type's own text isn't inherited, so a class doesn't get the text of its base class.
    /// </para>
    /// <para>
    /// A type with type arguments, such as <c>Page&lt;Order&gt;</c>, gives the text of <c>Page&lt;T&gt;</c>.
    /// </para>
    /// <para>
    /// The string is the wording for the current UI culture at the call. Read it where you show it.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type that is marked.</typeparam>
    public static string? GetTextOfType<T>()
    {
        var text = MemberTexts.GetOwn<T>();
        return text;
    }

    /// <summary>
    /// The text of a type marked <c>[Localized]</c> itself, by a type your code only has at run time, or
    /// <see langword="null"/> for a type without one. Where your code knows the type, write
    /// <see cref="GetTextOfType{T}()"/>.
    /// </summary>
    /// <remarks>
    /// A type with type arguments, such as <c>Page&lt;Order&gt;</c>, gives the text of <c>Page&lt;T&gt;</c>.
    /// </remarks>
    /// <param name="type">The type that is marked.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="type"/> is <see langword="null"/>.</exception>
    public static string? GetTextOfType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var text = MemberTexts.GetOwn(type);
        return text;
    }
}
