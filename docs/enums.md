# Enums

Give an enum its texts in one of two ways:

- **[A set of the enum](#a-set-of-the-enum)** makes a member without a text a build error. It also works for an enum you can't change, such as `DayOfWeek` or one from a library.
- **[An attribute on each member](#an-attribute-on-each-member)** keeps the wording next to the member, and a member without one is a build warning. It needs the enum's source, because the attribute goes on each member.

Either way, `order.Status.GetText()` gives the text of a value.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## A set of the enum

A set of the enum lists `ILocalizationSet<TEnum>` in place of `ILocalizationSet`. Each member of the enum is a text, named after the member:

```csharp
public enum OrderStatus { Pending, Shipped }

public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public string Pending => "Pending";
    public string Shipped => "Shipped";
}

public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public string Pending => "In behandeling";
    public string Shipped => "Verzonden";
}
```

The generator adds every member of the enum to the contract, as a property named after the member. The generated class gets a text per member, a method `GetTextByValue` that gives the text of a value, and an extension method `GetText` that you call on the value:

```csharp
public interface IOrderStatusLocalization
{
    string Pending { get; }
    string Shipped { get; }
}

public static partial class OrderStatusLocalization
{
    public static string Pending => ...; // The wording of the set for the user's language
    public static string Shipped => ...;

    public static string GetTextByValue(OrderStatus value) => value switch
    {
        OrderStatus.Pending => Pending,
        OrderStatus.Shipped => Shipped,
        _ => value.ToString(),
    };

    extension(OrderStatus value)
    {
        public string GetText() => GetTextByValue(value); // order.Status.GetText()
    }
}
```

The contract takes its members from the enum, and not only from the default-language set. So a member without a text is build error `CS0535` in every language, the default language too. Your IDE's "Implement interface" writes the missing properties.

`GetTextByValue` gives the wording for the user's language, as the text itself does, and allocates nothing. `order.Status.GetText()` gives the same, and is in reach wherever the namespace of the set is.

What else to know:

- **Every language.** Each set of the enum lists `ILocalizationSet<TEnum>`, in every language, so each set shows which enum it words. A set that lists `ILocalizationSet` in its place is build warning `LOC211`, and so is one that lists an enum the contract has no texts of. The fix for a missing language (`LOC201`) writes the generic one.
- **Only texts of members.** A text that isn't a member of the enum is build warning `LOC210`, because `GetText()` never gives it. It's usually left over from a member that was removed. Write a caption for the enum, or a text that takes values, in a set of its own.
- **A member's text is a property.** It takes no values, because `GetTextByValue` has none to give it. A method with the name of a member is build error `LOC300`.
- **The names `GetTextByValue` and `GetText`.** No text of such a set can have either name, because the generated methods have them (`LOC205`).
- **By name.** `GetTextByKey` takes the name of a member as its key: `OrderStatusLocalization.GetTextByKey("Shipped")`.
- **Members with the same value.** `Default = 0` next to `None = 0` each need a text. `GetTextByValue` gives the text of the first one, because it can't tell the two apart.
- **A value that isn't a member.** `GetTextByValue` shows it as `ToString()` writes it, in every language. For a combination of flags, that's the names of the flags, as in `Read, Write`. For any other value, it's the number.
- **Accessibility.** `GetTextByValue` and `GetText` are public for a public enum, and internal for an internal one.
- **More than one enum.** A set that lists `ILocalizationSet<TEnum>` for two enums holds the texts of both, and gets a `GetTextByValue` and a `GetText` for each. Two members with the same name share a text.
- **Two sets of one enum.** Each set gives the enum a `GetText`. Where the namespaces of both are in reach, the call is ambiguous (`CS0121`). Call `GetTextByValue` of the class you mean.
- **A set and `[Localized]` members.** An enum that has a set in its own assembly gets its `GetText` from the set alone, also while its members are still marked. `GetTextByMember` still finds the texts of the marked members.

For a library's enum, a set has two more advantages over attributes. An app can add a language to it, and reword a text the library made `virtual`. Neither is possible for a text written as an attribute.

## An attribute on each member

To keep the wording next to each member, mark each one `[Localized]`. The attribute on the enum itself gives the enum a text of its own, which `LocalizableText.GetTextOfType<ParcelStatus>()` shows:

```csharp
[Localized(en: "Parcel status", nl: "Pakketstatus")]
public enum ParcelStatus
{
    [Localized(en: "Picked", nl: "Gepickt")]
    Picked,

    [Localized(en: "Delivered", nl: "Bezorgd")]
    Delivered,
}
```

The source generator writes `GetText` for such an enum, in the enum's namespace. In outline:

```csharp
public static class ParcelStatusLocalizedExtensions
{
    extension(ParcelStatus value)
    {
        public string GetText() => value switch
        {
            ParcelStatus.Picked => ...,    // en: Picked, nl: Gepickt
            ParcelStatus.Delivered => ..., // en: Delivered, nl: Bezorgd
            _ => value.ToString(),
        };
    }
}
```

`LocalizableText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Picked))` finds the same text, as it does for any member marked `[Localized]` ([Attributes](attributes.md)).

What else to know:

- **Members with the same value.** Of two members with the same value, the first gives the text. A member with the value of a marked member, such as `Default = Active`, shows that member's text.
- **A member without `[Localized]`.** It shows its name, in every language. The build warns about it when other members of the enum are marked (`LOC503`), because that's usually a member added later. An enum with a text of its own and no marked member gets no warning, because its values may never be shown.
- **An enum inside a generic type.** An enum inside a type with type parameters gets no `GetText()`, and `GetTextByMember` still finds its texts.