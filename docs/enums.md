# Enums

Give an enum its texts in one of two ways:

- **[A set of the enum](#a-set-of-the-enum)** makes a member without a text a build error. It also works for an enum you can't change, such as `DayOfWeek` or one from a library.
- **[An attribute on each member](#an-attribute-on-each-member)** keeps the wording next to the member, and a member without one is a build warning. It needs the enum's source, because the attribute goes on each member.

Either way, `order.Status.GetText()` gives the text of a value.

[`EnumDemo.cs`](../demos/EnumDemo.cs) is a complete app that gives two enums their texts, one in each way. It's one file, which `dotnet run` runs without a project.

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

The generated class gets a text per member of the enum, a method `GetTextByValue` that gives the text of a value, and an extension method `GetText` that you call on the value. The generator also adds every member to the contract, as a property named after the member:

```csharp
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

public interface IOrderStatusLocalization
{
    string Pending { get; }
    string Shipped { get; }
}
```

The contract takes its members from the enum, and not only from the default-language set. So a member without a text is build error `CS0535` in every language, the default language too. Your IDE's "Implement interface" writes the missing properties.

`GetTextByValue` gives the wording for the user's language, as the text itself does, and allocates nothing. `order.Status.GetText()` gives the same, and is in reach wherever the namespace of the set is.

## An attribute on each member

To keep the wording next to each member, mark each one `[Localized]`. The attribute on the enum itself gives the enum a text of its own, which `LocalizedText.GetTextOfType<ParcelStatus>()` shows:

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

`LocalizedText.GetTextByMember<ParcelStatus>(nameof(ParcelStatus.Picked))` finds the same text, as it does for any member marked `[Localized]` ([Attributes](attributes.md)).

## What else to know

Either way:

- **Members with the same value.** The first of them gives the text. So with `Default = 0` and `None = 0`, `OrderStatus.None.GetText()` shows the text of `Default`. The build warns about such a call where `None` has a text of its own ([LOC600](build-errors/LOC6XX-reading-a-text.md#loc600)). To show that text, write `OrderStatusLocalization.None` with a set, or call `GetTextByMember` with attributes.
- **A value that isn't a member.** It shows as `ToString()` writes it, in every language, such as `Read, Write` for flags.

With a set of the enum:

- **Every language.** Each set lists `ILocalizationSet<TEnum>`, not only the default language's (`LOC206`).
- **Only members, as properties.** A set of an enum holds a property per member and nothing else (`LOC305`, `LOC400`). Write a caption for the enum, or a text that takes values, in a set of its own.
- **By name.** `GetTextByKey` takes the member's name: `OrderStatusLocalization.GetTextByKey("Shipped")`.
- **More than one enum.** A set that lists two enums holds the texts of both, and gets a `GetText` for each. Two members with the same name share a text.
- **Two sets of one enum.** Each gives the enum a `GetText`, so a call that sees both is ambiguous (`CS0121`). Call `GetTextByValue` of the class you mean.
- **An enum of another assembly.** A set of an enum you can't change, such as `DayOfWeek`, gives it an internal `GetText`, also when the set is public. So two libraries that each translate `DayOfWeek` don't make your calls ambiguous. Other assemblies call `GetTextByValue`, as in `DayLocalization.GetTextByValue(DayOfWeek.Monday)`.
- **A set and `[Localized]` members.** The set's texts win, in every language. `GetTextByMember` still finds the attributes' texts. A member the set has no text of keeps the text of its `[Localized]`, such as a member that a newer version of a library adds to its enum.
- **A library's enum.** An app can add a language to a library's set, and reword a text the library made `virtual`. It adds a language to an enum with `[Localized]` members by writing a set of its own. That set's wording then shows wherever `GetText()` is called, also in the library.

With an attribute on each member:

- **A member without `[Localized]`.** It shows its name, in every language. The build warns about it when other members of the enum are marked (`LOC501`), because that's usually a member added later. An enum with a text of its own and no marked member gets no warning, because its values may never be shown.
- **An enum inside a generic type.** It gets no `GetText()`, and `GetTextByMember` still finds its texts.
