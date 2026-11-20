# How Idioma works

You don't need to read this page to use Idioma, because the [getting started guide](../README.md#getting-started) covers everything you write. This page is the background, for when you want to know what happens behind it: the fallback order, what the source generator writes, texts with parameters, enums, how sets register, how to read a set yourself, how a text travels through JSON, and when two texts are equal.

Other subjects have a page of their own: [language codes](language-codes.md), [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), [what the build rejects](build-errors.md), [coming from resx](coming-from-resx.md), and [performance](performance.md).

## Fallback order

A text follows `CultureInfo.CurrentUICulture` and shows the first wording it finds:

1. The wording for that culture, or else for its parent culture. An `en` wording serves `en-US` and `en-GB` alike, and an `en-GB` wording wins over it in Britain.
2. The wording for the app's default language, which is the first language it declares. Library texts fall back to it too, so no screen ends up in two languages. A culture that none of the app's languages serves starts here, even when a library has its language. For example, an app declaring `("en", "fr")` shows a Dutch user a library's texts in English, like its own.
3. The wording for the default language of the project that wrote the text.

In an app declaring `("en", "nl", "en-GB")`, whose British set words the cart as "Basket":

| The user's culture | `ShopLocalization.Cart` shows | Because                                            |
|--------------------|-------------------------------|----------------------------------------------------|
| `en-GB`            | Basket                        | It has its own wording                             |
| `en-AU`            | Cart                          | Its parent culture, `en`, has wording              |
| `nl-NL`            | Winkelwagen                   | Its parent culture, `nl`, has wording              |
| `de-DE`            | Cart                          | No app language serves it, so the default language |

`ToString` follows the same order for the culture you pass.

### The current culture

A text takes the user's culture from `CultureInfo.CurrentUICulture`. To take it from somewhere else, such as a session or a user profile, set a function once at startup:

```csharp
LocalizationRegistry.CurrentUICultureProvider = () => UserProfile.Current?.Culture;
```

What to know:

- It applies to every text of the app, including the texts of the libraries it uses.
- It runs each time a text is shown, so keep it fast.
- When it returns `null`, the text follows `CultureInfo.CurrentUICulture` for that call.
- A [text with parameters](#texts-with-parameters) formats their values for the same culture.
- `ToString(culture)` still shows the wording for the culture you pass.

A text also follows a culture passed as a format provider, so a string built for another culture is in one language:

```csharp
// On an English thread: "Totaal: 1.234,50"
string line = string.Create(dutch, $"{ShopLocalization.Total}: {amount:N2}");
```

`string.Format(dutch, ...)` does the same. Two providers give the user's own wording instead:

- `CultureInfo.CurrentCulture` formats for the current user, whose wording goes by the UI culture.
- A provider that isn't a culture, such as a `NumberFormatInfo`, names no language.

`CultureInfo.InvariantCulture` has no wording, so it gives the wording of your app's default language.

## What the source generator writes

`LocalizableText.Inline` gets one required parameter per declared language, named after it: `enGb` for `en-GB`.

From the default-language set, `ShopLocalizationEn` in the namespace `MyApp.Localization`, the generator writes the contract `IShopLocalization` and the generated class `ShopLocalization`. In outline:

```csharp
namespace MyApp.Localization;

[LocalizationContract]
public interface IShopLocalization
{
    string Cart { get; }
    string Checkout { get; }
}

public static partial class ShopLocalization
{
    public static LocalizableText Cart { get; } = ...;     // Its key is "Cart"
    public static LocalizableText Checkout { get; } = ...; // Its key is "Checkout"

    public static LocalizableText GetTextByKey(string key, params ReadOnlySpan<object?> values) => ...;
}
```

Every set lists `ILocalizationSet` and the contract, the default-language set too: `class ShopLocalizationNl : ILocalizationSet, IShopLocalization`. The contract doesn't derive from `ILocalizationSet`, so neither entry is redundant. The build rejects a set without its contract (`LOC204`), and your IDE adds it.

A text always has wording, so the contract declares it as `string`. A set that returns `string?` for a text gets compiler warning `CS8766`, because it doesn't match the contract. Return a `string`.

Only a class that implements `ILocalizationSet` is a set, whatever its name. A class that implements only a contract, such as a test double, isn't one, so the build doesn't read a language from its name. A set that derives from another set, such as a regional one, implements `ILocalizationSet` through that set.

Each generated text shows its wording in every language and links to it. Hovering over `ShopLocalization.Checkout` shows `nl: Afrekenen`, and you can jump from there to `ShopLocalizationNl.Checkout`. The generated class is partial, so a helper can sit next to the texts. Besides the texts it holds only the method `GetTextByKey`, and `GetTextByValue` in a [set of an enum](#enums). So a text can take any name except those and the name of the class itself (`LOC205`).

Each generated text is a `LocalizableText` with two keys:

- Its `Key` is its name, `Checkout` here. Two classes can each have a text with that key. `GetTextByKey` takes it ([Getting a text by its key](#getting-a-text-by-its-key)).
- Its `FullKey` is its full name, `MyApp.Localization.ShopLocalization.Checkout` here, so every text has a full key of its own.

The package never reads the full key. It's for an app or library that also looks texts up in resource files, such as through `IStringLocalizer`, so a resource file can override a wording without a rebuild ([an example](coming-from-resx.md#keeping-a-resource-file-as-an-override)).

### Getting a text by its key

`GetTextByKey` gives the text with a key ([an example](../README.md#get-a-text-by-its-key)). It's for a key your code only has at run time, such as one from a database. Where you know the text, write `ShopLocalization.Checkout`, which the build checks.

```csharp
public static LocalizableText GetTextByKey(string key, params ReadOnlySpan<object?> values)
```

- **The key.** It's the name of the text, in the same letter case. A key no text has throws `KeyNotFoundException`, and so does a full key.
- **The values.** A text with parameters takes its values after the key, in the order of its parameters: `ShopLocalization.GetTextByKey("ItemsInCart", cart.Count, shop.Name)`. Each value has to be of the type of its parameter, because a value isn't converted. An `int` for a `long` throws `ArgumentException`, and so does a missing value or one too many.
- **The result.** It's the text, a `LocalizableText`. For a text without parameters, it's the one the class already holds, so a call allocates nothing. Show it for another culture with `ToString(culture)`, as any text.

The generator also writes the code that registers your sets (see [Registration](#registration)). It's a class named after your assembly, `LocalizationRegistrar_MyApp` for `MyApp`, in the namespace `CodeChops.Idioma.Generated`. The class is public in an assembly that has sets, and in an app that declares languages, so an app that references the assembly can register them at startup. It's hidden from autocomplete. A library that keeps a list of its public API still finds it there, next to the contract and the generated class of every public set.

## Texts with parameters

A public method of the default-language set that returns a `string` and takes parameters is a text with parameters ([an example](../README.md#texts-with-parameters)). The contract repeats the method, so every language implements it. The generated class gets a method with the same name and parameters:

```csharp
public interface IShopLocalization
{
    string ItemsInCart(int count, string shop);
}

public static partial class ShopLocalization
{
    public static LocalizableText ItemsInCart(int count, string shop) => ...; // Its key is "ItemsInCart"
}
```

Each call creates a text that keeps the values as its `Arguments` and words itself with them when it's shown. A call allocates the text and its values, and nothing more until the text is shown. Because it's worded when shown, it follows the user's language like any other text. One created for an English user and shown to a Dutch one reads in Dutch, and `ToString(culture)` gives the wording of the culture you pass. Its key is its name, without its parameters. Two such texts are equal when they read the same ([Equality](#equality)).

Write a text without values as a property, because a method without parameters isn't a text. The build rejects such a method in the default-language set (`LOC300`), unless it's an override such as `ToString()` or implements an interface member. A public method of the default-language set that isn't meant as a text, such as a helper, still becomes one, and every language then has to implement it. Make a helper `private` or `internal`. A `static` method isn't a text either, so a helper that other assemblies call can stay public as a static method. If it has to stay public on the instance, such as an override of a public member of a base class, mark it `[ExcludeFromLocalizationDiscovery]`. That attribute also keeps a public string property from being a text.

The build rejects what the generated class can't repeat or keep (`LOC300`), because the class repeats the parameters as the set declares them:

- a generic method;
- two methods with the same name, because a text is told apart by its name;
- a method with more than 8 parameters;
- a parameter passed by reference (`ref`, `in`, `out`), a `params` parameter, or a parameter with a default value;
- a `dynamic` parameter, a pointer, or a ref struct such as a span.

Every language names the parameters as the default-language set does (`LOC301`). The compiler checks their types and order, but not their names. Without this check, a set could swap two parameters of the same type and word each value as the other.

Every language also uses the values the default wording shows (`LOC302`). A Dutch `Greeting(string name) => "Hallo!"` next to an English `$"Hello, {name}!"` is a build error, because Dutch users wouldn't see the name. A value the default wording only chooses by, such as `salutation` in a `switch`, may go unused in a language that doesn't make that distinction. The contract records which values the default wording shows, so the check also holds for a set that an app adds to a library's contract.

What else to know:

- **Plurals.** Idioma has no plural rules. Each language chooses its wording for a number in its own set, in C#. `count is 1 ? ... : ...` is right for English and Dutch. A language with more forms writes a condition for each. Polish has three forms for a count:

  ```csharp
  public string ItemsInCart(int count, string shop) => count switch
  {
      1 => $"1 produkt w koszyku w sklepie {shop}",
      _ when count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14) => $"{count} produkty w koszyku w sklepie {shop}",
      _ => $"{count} produktów w koszyku w sklepie {shop}",
  };
  ```

- **Formatting values.** A value in an interpolated string is formatted for `CultureInfo.CurrentCulture`, as anywhere in C#, and can carry a format, as in `{amount:N2}`. When you pass `ToString` a culture other than the current UI culture, the text is worded as for a user of that culture. Its values are formatted for that culture too, so a date or a number reads as the recipient of an email writes it.
- **A text as a value.** Give a parameter that takes a text the type `LocalizableText`, as in `Greeting(LocalizableText name)`. The value then shows its wording in the same culture as the text around it. A text passed to a `string` parameter becomes a string at the call, so it keeps the wording of the language in use at that moment. `ShopLocalization.Greeting(ShopLocalization.Checkout).ToString(dutch)` then shows "Hallo, Checkout!" on an English thread, so the build warns about such a call (`LOC303`).
- **Inline texts.** An inline text takes a value as you write it and fills it in at once, in every language: `LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")`. Every language uses the values that the wording of the default language shows in its interpolated strings (`LOC302`). A text among those values is filled in at once too, in the language in use at that moment. For a wording that holds another text and outlives the request, write a text of a set with a `LocalizableText` parameter.
- **Placeholders.** A wording that holds a format-string placeholder, such as `{0}` or `{0:N2}`, gets build warning `LOC502`, in a set and in an inline text. A text shows its wording as written, so nothing would fill the placeholder in. Braces around anything but a number are text. A string passed to a method, such as the format of `string.Format`, gets no warning, because the method may fill the placeholder in.

## Enums

A set that lists `ILocalizationSet<TEnum>` in place of `ILocalizationSet` holds the texts of an enum ([an example](../README.md#enums)). The generator adds every member of the enum to the contract, as a property named after the member. The generated class gets a text per member, and a method `GetTextByValue` that gives the text of a value:

```csharp
public interface IOrderStatusLocalization
{
    string Pending { get; }
    string Shipped { get; }
}

public static partial class OrderStatusLocalization
{
    public static LocalizableText Pending { get; } = ...; // Its key is "Pending"
    public static LocalizableText Shipped { get; } = ...; // Its key is "Shipped"

    public static LocalizableText GetTextByValue(OrderStatus value) => value switch
    {
        OrderStatus.Pending => Pending,
        OrderStatus.Shipped => Shipped,
        _ => ..., // A text that shows value.ToString()
    };
}
```

The contract takes its members from the enum, and not only from the default-language set. So a member without a text is build error `CS0535` in every language, the default language too. Your IDE's "Implement interface" writes the missing properties.

`GetTextByValue` returns the text the class already holds, so a call allocates nothing. The text follows the user's language like any other text.

What else to know:

- **Other languages.** Only the default-language set needs `ILocalizationSet<TEnum>`, because the contract comes from it. A set of another language may list `ILocalizationSet` instead. The build suggests the generic one for a missing language (`LOC201`).
- **Other texts.** The set can hold texts that aren't members, such as a caption for the enum. A text that takes values works too, as long as it doesn't have the name of a member.
- **A member's text is a property.** It takes no values, because `GetTextByValue` has none to give it. A method with the name of a member is build error `LOC300`.
- **The name `GetTextByValue`.** No text of such a set can be named `GetTextByValue`, because the generated method has that name (`LOC205`).
- **By name.** `GetTextByKey` takes the name of a member as its key: `OrderStatusLocalization.GetTextByKey("Shipped")`.
- **Members with the same value.** `Default = 0` next to `None = 0` each need a text. `GetTextByValue` gives the text of the first one, because it can't tell the two apart.
- **A value that isn't a member.** `GetTextByValue` shows it as `ToString()` writes it, in every language. For a combination of flags, that's the names of the flags, as in `Read, Write`. For any other value, it's the number.
- **Accessibility.** `GetTextByValue` is public for a public enum, and internal for an internal one.
- **More than one enum.** A set that lists `ILocalizationSet<TEnum>` for two enums holds the texts of both, and gets an `GetTextByValue` for each. Two members with the same name share a text.

## Registration

Every set is registered automatically. When an app starts, it registers the sets of every assembly it references, whether it uses that assembly or not. An assembly the app loads later, such as one a Blazor WebAssembly app loads lazily, registers its sets when its code first runs. Loading it alone doesn't.

A set that can't be registered automatically gets build warning `LOC400`. That happens when the set:

- has a `required` member, which generated code can't set;
- has no parameterless constructor;
- has no language code;
- rewords another assembly's texts from a nested or file-local class, which generated code can't reach.

A nested, generic, or file-local class with your own texts isn't recognized, so its language is reported as missing (`LOC201`). Make it a top-level class that isn't generic.

To register a set by hand, such as a set that needs a constructor argument, mark it `[ExcludeFromLocalizationDiscovery]` and call `LocalizationRegistry.Register`:

```csharp
[ExcludeFromLocalizationDiscovery]
public sealed class BrandedShopLocalizationEn(string shopName) : ShopLocalizationEn
{
    public override string Checkout => $"Pay at {shopName}"; // Checkout is virtual in ShopLocalizationEn
}

// At startup:
LocalizationRegistry.Register(new BrandedShopLocalizationEn(configuration["ShopName"]!));
```

The build still counts it as a set of its language, and still requires that language to be listed (`LOC202`).

## Reading sets yourself

Besides registering, `LocalizationRegistry` does two things:

- It reads a set directly: `LocalizationRegistry.Current.Find<IShopLocalization>(culture)`.
- It gives part of an app, or a test, its own sets: `LocalizationRegistry.Create(sets)`. Pass such a registry to a text, as in `ShopLocalization.Checkout.ToString(culture, registry)`, and the text reads its wording from it. The registry is also independent of your app's languages. It shows every language it has a set for, unless you pass it languages of its own: `LocalizationRegistry.Create(sets, "en", "nl")`. Listing a language twice throws an `ArgumentException`, so each language is listed once.

```csharp
var dutch = CultureInfo.GetCultureInfo("nl");

// The Dutch set itself, whatever the current UI culture
string? checkout = LocalizationRegistry.Current.Find<IShopLocalization>(dutch)?.Checkout;

// A registry with sets of its own, as a test makes one
var registry = LocalizationRegistry.Create([new ShopLocalizationNl()]);
string wording = ShopLocalization.Checkout.ToString(dutch, registry);
```

## Texts in JSON

A text survives JSON, such as a Blazor component parameter that crosses a render mode. It's written with every wording it can show: its own translations and, for a text of a set, the wording of every language your app declares that has a registered set, regions included. Read back, it shows that wording for every culture without needing the sets, and a text with a key keeps its full key. That's the one key it has from then on, so its `Key` gives the full key too. It no longer belongs to a contract, so a set registered later doesn't reword it. It equals the text it was written from, because a text compares by its wording ([Equality](#equality)).

```json
{"key":"MyApp.Localization.ShopLocalization.Checkout","defaultText":"Checkout","translations":{"en":"Checkout","nl":"Afrekenen"}}
```

An inline text travels without a `key`. A text without translations, such as a customer's name, travels as its wording alone: `"Ada"`.

A text that takes values is written with its values filled in, in every language: `"nl":"3 artikelen in je winkelwagen bij Idioma"`. A text among its values is worded in each language too. A date or a number is formatted as for the user the text is written for, in every language, because a language doesn't say which region its reader lives in. With `CurrentUICultureProvider` set, that's the culture it gives. Read back, it shows that wording and no longer has its `Arguments`. It travels without its key too, because a resource file entry found by the key would have no values to fill in.

A JSON string reads as a text too, with that one wording for every culture. So JSON you write by hand, such as a settings file, can hold `"title": "Orders"`.

## Equality

A text equals anything that shows the same wording for the current UI culture: another text or a string. For a Dutch user, each of these is true, and for an English user each is false:

```csharp
string wording = "Afrekenen";
List<LocalizableText> texts = [ShopLocalization.Checkout];

ShopLocalization.Checkout == wording
ShopLocalization.Checkout.Equals(wording)
ShopLocalization.Checkout == LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")
texts.Contains(wording)
```

Only the wording counts. The key, the contract, the values, and the wording of other languages don't. So:

- a text read back from JSON equals the text it was written from;
- two texts that take values are equal when they read the same, even with other values, such as `1.5` and `1.499` in a wording with `{amount:N2}`.

The comparison is by character, so letter case counts. To compare without it, or to sort, use the wording as a string:

```csharp
bool same = string.Equals(ShopLocalization.Checkout, "afrekenen", StringComparison.CurrentCultureIgnoreCase);
var sorted = texts.OrderBy(text => (string)text, StringComparer.CurrentCulture);
```

The hash (`GetHashCode`) follows the user's language too, because equal texts must hash alike. A `HashSet<LocalizableText>`, or a `Dictionary` keyed by texts, works while the language stays the same. After a switch, it can't find the texts it holds. So a collection that outlives a switch, or that users of different languages share, needs `ReferenceEqualityComparer.Instance`. It then tells texts apart by object:

```csharp
var cache = new Dictionary<LocalizableText, string>(ReferenceEqualityComparer.Instance);
```

A string only equals another string. So `object.Equals(text, "Afrekenen")` is true for a Dutch user, but `object.Equals("Afrekenen", text)` is false.