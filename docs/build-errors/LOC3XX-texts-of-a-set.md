# Build errors about texts of a set

Each code has an example and the fix. The [list of every code](../build-errors.md#all-codes) shows which ones your IDE can fix.

## LOC300

A text has a name the generated class can't give it, so that text isn't generated. Rename the text. Four names are taken:

- The name of the generated class, as in `ShopLocalization.ShopLocalization`. In C#, a member can't have the name of its own class.
- `GetTextByKey`. The generated class has a method of that name, which gives a [text by its key](../advanced-topics.md#getting-a-text-by-its-key).
- `GetTextByValue`, in a [set of an enum](../enums.md#a-set-of-the-enum). The generated class has a method of that name, which gives the text of an enum value.
- `GetText`, in a set of an enum. The generated class has an extension method of that name, which gives a value its text: `order.Status.GetText()`.

## LOC301

A text has a setter, so its wording can change after the set is created:

```csharp
public string Greeting { get; set; } = "Hallo!"; // LOC301
```

Remove the setter. One instance of each set serves every user, so a change of wording reaches all of them. Any setter counts, also a `private` one.

- To set the wording when the set is created, assign it in the constructor or use `init`.
- To change the wording at run time, read it in the getter, for example from a store.

The IDE fix removes the setter, along with its body. An auto-property can still be assigned in the constructor. Any other assignment stops compiling, which shows you the code that changed the wording.

A member that isn't a text may have a setter, such as a helper marked `[ExcludeFromLocalizationDiscovery]`. The build doesn't catch a getter that returns a field which a method changes.

## LOC302

A set that derives from another hides one of its texts instead of overriding it, so users never see its wording:

```csharp
public class ShopLocalizationEnGb : ShopLocalizationEn
{
    public new string Color => "Colour"; // LOC302: British users still see "Color"
}
```

Make the text `virtual` in the set it derives from, and override it. The set implements the contract through the set it derives from, so the contract calls that set's member. A member of the same name only hides it. Without `new`, the compiler warns about the hiding (`CS0108`), and the IDE's fix for that warning adds `new`.

Where the text can't be virtual, such as a text of a library, list the contract again: `class MyShopLocalizationEn : ShopLocalizationEn, IShopLocalization`. The contract then calls the set's own member.

The IDE offers both fixes:

- Override the text. The fix puts `override` in place of `new`. Where the text isn't virtual yet, it makes it virtual in the set that declares it, so the fix can change two files. A library's text that isn't virtual gets no such fix, and neither does a text implemented explicitly.
- List the contract again. This fix is always offered.

"Fix all" changes each text once, however many sets hide it. A set that hides texts of two contracts lists both.

## LOC303

The default-language set inherits a text from a class of its assembly without declaring it, so its contract leaves the text out:

```csharp
public abstract class ShopBase
{
    public string Brand => "Idioma"; // LOC303: no other language has to translate it
}

public class ShopLocalizationEn : ShopBase, ILocalizationSet, IShopLocalization
{
    public string Cart => "Cart";
}
```

Declare the text in the default-language set, by overriding it or by moving it there. The contract is written from the texts that set declares itself, so a text it only inherits isn't in it. No other language then has to word it, and the generated class has no text for it.

A member of the base that isn't a text needs `[ExcludeFromLocalizationDiscovery]`. A class of another assembly isn't read, so a property such as `Exception.Message` isn't reported.

## LOC304

A set has a text that the default-language set doesn't have, so no user sees it:

```csharp
public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
{
    public string Checkout => "Afrekenen";
    public string Basket => "Mandje"; // LOC304: ShopLocalizationEn has no Basket
}
```

```text
warning LOC304: 'ShopLocalizationNl.Basket' isn't a text of 'IShopLocalization', so users of 'nl' won't see it. Add 'Basket' to the default-language set, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.
```

Add the text to the default-language set, and write its wording there. The contract is written from the texts that set declares, so a text that only another language has isn't in it. No generated text reads it then.

- A set of a library's contract can't add a text to it, so remove the text there.
- Mark a member that isn't a text `[ExcludeFromLocalizationDiscovery]`.
- To keep a text you'll add to the default-language set later, turn the warning off around it: `#pragma warning disable LOC304`.

A member with the name of a text isn't reported, because another code says what's wrong with it, such as [LOC302](#loc302) for a hidden text.

## LOC305

A set of an enum has a text that isn't a member of the enum, so `GetText()` never gives it:

```csharp
public enum OrderStatus { Pending, Shipped }

public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public string Pending => "In behandeling";
    public string Shipped => "Verzonden";
    public string Delivered => "Bezorgd"; // LOC305: OrderStatus has no Delivered
}
```

```text
warning LOC305: 'OrderStatusLocalizationNl.Delivered' isn't a member of 'OrderStatus', so GetText() never gives it. Add 'Delivered' to 'OrderStatus', move it to a set of its own, or mark it [ExcludeFromLocalizationDiscovery] if it isn't a text.
```

Add the member to the enum if it's missing there. Every language then needs its text, so the build reports `CS0535` for each set that lacks it. Otherwise the text is usually left over from a member that was removed, so remove it from each set.

- Write a caption for the enum, or a text that takes values, in a set of its own that lists `ILocalizationSet`.
- An enum of another assembly, such as `DayOfWeek`, can't get a member, so move the text.
- Mark a member that isn't a text `[ExcludeFromLocalizationDiscovery]`.
- To keep the text in the set of the enum, turn the warning off around it: `#pragma warning disable LOC305`.

Each set that lists `ILocalizationSet<TEnum>` is reported, the default-language set too. In another language, LOC305 takes the place of [LOC304](#loc304), because adding the text to the default-language set wouldn't help. A text of a library's contract isn't reported, because the set has to implement it.
