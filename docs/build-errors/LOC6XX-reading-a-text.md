# Build errors about reading a text

Each code has an example and the fix. The [list of every code](../build-errors.md#all-codes) shows which ones your IDE can fix.

## LOC600

A call names an enum member whose value an earlier member has. This is a warning. A value can't tell the two members apart, so the call gets the text of the earlier member:

```csharp
public enum OrderStatus { Default, None = Default }

OrderStatus.None.GetText()                               // LOC600: shows the text of Default
OrderStatusLocalization.GetTextByValue(OrderStatus.None) // LOC600
```

```text
warning LOC600: GetText() gives the text of 'OrderStatus.Default' for 'OrderStatus.None', because the two have the same value. Use 'OrderStatusLocalization.None' for the text of 'None'.
```

Use the member's own text:

- In a [set of the enum](../enums.md#a-set-of-the-enum), that's `OrderStatusLocalization.None`. The IDE fix writes it.
- For a member marked `[Localized]`, that's `LocalizedText.GetTextByMember<OrderStatus>(nameof(OrderStatus.None))`. It gives a `string?`, so it gets no fix.

The build checks a call that names the member in the code. A value your code only has at run time, as in `order.Status.GetText()`, gets no warning, because no member is named there. Such a value gives the text of the first member.

A member without `[Localized]` next to marked members, such as `Default = Active`, gets no warning either, because it has no text of its own.

## LOC601

A call asks for the text of a member or a type that has none, so it always gives `null`:

```csharp
LocalizedText.GetTextByMember<Address>(nameof(Address.Street)) // LOC601: Street isn't marked [Localized]
LocalizedText.GetTextOfType<Customer>()                        // LOC601: Customer has no text of its own
```

Mark the member or the type `[Localized(en: "...", nl: "...")]`. An override has the text of the property it overrides, so a marked property in a base class is enough. A type's own text isn't inherited.

The build checks a call that names its type and its member in the code: by a type argument or by `typeof`, and by a member name that's a constant, such as `nameof(Address.Street)`. It doesn't check a call that takes a `Type` or a `PropertyInfo` your code only has at run time. There, `null` is the answer for a member without a text, as a form gets it for a field without a label.

A type of another assembly is checked only when that assembly doesn't use Idioma, such as `DayOfWeek` of .NET itself. The attribute isn't kept in a compiled assembly, so the build can't see which members of a library have a text.

A member your class inherits from such an assembly has no text either, such as `Message` of a class that derives from `Exception`. The message says where the member is declared. To give it a text, override it in your class and mark the override, where the member is virtual.

To ask for a text that isn't there yet on purpose, turn the error off around the call: `#pragma warning disable LOC601`.

## LOC602

A text is read in the initializer of a static field or a static property. This is a warning:

```csharp
private static readonly string Label = ShopLocalization.Checkout; // LOC602
public static string Title { get; } = LocalizedText.Inline(en: "Orders", nl: "Bestellingen"); // LOC602
```

A text is the wording for the user's language at the moment it's read. A static initializer runs once, so it keeps the wording of whoever used the class first. On a server, every user then sees that language. Read the text where you show it:

```csharp
private static string Label => ShopLocalization.Checkout;
public static string Title => LocalizedText.Inline(en: "Orders", nl: "Bestellingen");
```

```text
warning LOC602: 'ShopLocalization.Checkout' is read once, when 'Label' is initialized, so every user gets the wording of the first user's language. Read the text where you show it, such as in a property written with '=>'.
```

What counts as a text:

- a text of a generated class, such as `ShopLocalization.Checkout` or `ShopLocalization.GetTextByKey("Checkout")`, and not a helper you wrote next to its texts
- `LocalizedText.Inline`, `LocalizedText.GetTextByMember` and `LocalizedText.GetTextOfType`
- `GetText()` on a value of an enum

A text inside a lambda gets no warning, because it's read when the lambda runs: `static readonly Func<string> Label = () => ShopLocalization.Checkout;`. A lambda that runs as the initializer does is the exception, so these get the warning too:

- A lambda of a LINQ query that the initializer enumerates, as `ToList()`, `First()` and `[.. query]` do: `static readonly List<string> Labels = statuses.Select(status => status.GetText()).ToList();`. A query stored as it is runs later, each time it's enumerated, so it gets no warning.
- A lambda given to `Lazy<T>`, which keeps the wording it reads first.

A text an instance field keeps gets no warning, because it lives as long as its object. So a text kept in a singleton, a cache or a local variable that outlives the request still compiles ([What the build misses](../build-errors.md#what-the-build-misses)).
