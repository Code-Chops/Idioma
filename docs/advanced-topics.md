# Advanced topics

The [getting started guide](../README.md#getting-started) covers what most apps write. This page explains what happens behind the texts you write, and covers the tasks the guide leaves out:

- [Fallback order](#fallback-order): which wording a user sees, and [where the user's culture comes from](#the-current-culture)
- [Texts with parameters](#texts-with-parameters): what a call creates, and keeping a public method of a set from becoming a text
- [Equality](#equality): when two texts are equal, and a collection that outlives a language switch
- [What the source generator writes](#what-the-source-generator-writes), and [getting a text by its key](#getting-a-text-by-its-key)
- [Registration](#registration): how sets register, and registering a set by hand
- [Reading sets yourself](#reading-sets-yourself): testing with sets of your own, and [writing a contract yourself](#a-contract-you-write-yourself)
- [Texts in JSON](#texts-in-json): how a text travels, such as to a Blazor component

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## Fallback order

A text follows `CultureInfo.CurrentUICulture` and shows the first wording it finds:

1. The wording for that culture, or else for its parent culture. An `en` wording serves `en-US` and `en-GB` alike, and an `en-GB` wording wins over it in Britain. A culture whose name holds its script, as `zh-Hant-TW` does on Apple platforms, tries the name without it right after itself, so a `zh-TW` wording serves it.
2. The wording for the app's default language, which is the first language it declares. Library texts fall back to it too, so no screen ends up in two languages. A culture that none of the app's languages serves starts here, even when a library has its language. For example, an app declaring `("en", "fr")` shows a Dutch user a library's texts in English, like its own.
3. The wording for the default language of the project that wrote the text.

In an app declaring `("en", "nl", "en-GB")`, whose British set calls the cart "Basket":

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

Idioma compares a culture you pass with `CultureInfo.CurrentCulture` by reference. So a culture gives the user's wording whenever it's that same object, however your code got it. Where code sets the current culture to someone else's, such as an email's recipient's, and leaves the UI culture, call `ToString(culture)` on the text: `$"{ShopLocalization.Total.ToString(dutch)}: {amount:N2}"`.

`CultureInfo.InvariantCulture` has no wording, so it gives the wording of your app's default language.

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

Each call creates a text that keeps the values as its `Arguments`, and fills them into its wording when it's shown. A call allocates the text and its values, and nothing more until the text is shown. Because its wording is made when it's shown, it follows the user's language like any other text. One created for an English user and shown to a Dutch one reads in Dutch, and `ToString(culture)` gives the wording of the culture you pass. Its key is its name, without its parameters. Two such texts are equal when they read the same ([Equality](#equality)).

Write a text without values as a property, because a method without parameters isn't a text. The build rejects such a method in the default-language set (`LOC300`), unless it's an override such as `ToString()` or implements an interface member.

A public method of the default-language set that returns a `string` and takes parameters becomes a text, also when it's a helper. Every language then has to implement it. To keep a helper out:

- Make it `private` or `internal`.
- Make it `static`. A static method is never a text, so it can stay public for other assemblies.
- Mark it `[ExcludeFromLocalizationDiscovery]` where it has to stay public on the instance, such as an override of a public member of a base class. The attribute also keeps a public `string` property from being a text.

The generated class repeats the parameters as the set declares them. So the build rejects a method it can't repeat or keep, such as a generic one or one with a `ref` parameter (`LOC300`). [LOC300](build-errors.md#loc300) lists every case.

Every language names the parameters as the default-language set does (`LOC301`). The compiler checks their types and order, but not their names. Without this check, a set could swap two parameters of the same type and word each value as the other.

Every language also uses the values the default wording shows (`LOC302`). A Dutch `Greeting(string name) => "Hallo!"` next to an English `$"Hello, {name}!"` is a build error, because Dutch users wouldn't see the name. What counts as showing a value:

- A value the default wording only chooses by, such as `salutation` in a `switch`, doesn't count. So it may go unused in a language that doesn't make that distinction.
- A variable that a pattern declares for the whole value counts as the value. So `count switch { 1 => "1 item", var n => $"{n} items" }` shows `count`.

The contract records which values the default wording shows. So the check also holds for a set that an app adds to a library's contract. For a contract you wrote by hand, the build reads them from the default-language set in the same assembly.

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

- **Formatting values.** A value in an interpolated string is formatted for `CultureInfo.CurrentCulture`, as anywhere in C#, and can carry a format, as in `{amount:N2}`. When you pass `ToString` a culture other than the current UI culture, the text shows the wording for a user of that culture. Its values are formatted for that culture too, so a date or a number reads as the recipient of an email writes it.
- **A text as a value.** Give a parameter that takes a text the type `LocalizableText`, as in `Greeting(LocalizableText name)`. The value then shows its wording in the same culture as the text around it. A text passed to a `string` parameter becomes a string at the call, so it keeps the wording of the language in use at that moment. `ShopLocalization.Greeting(ShopLocalization.Checkout).ToString(dutch)` then shows "Hallo, Checkout!" on an English thread, so the build warns about such a call (`LOC303`).
- **Inline texts.** An inline text takes a value as you write it and fills it in at once, in every language: `LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")`. Every language uses the values that the wording of the default language shows in its interpolated strings (`LOC302`). A text among those values is filled in at once too, in the language in use at that moment. For a wording that holds another text and outlives the request, write a text of a set with a `LocalizableText` parameter.
- **Placeholders.** A wording that holds a format-string placeholder, such as `{0}` or `{0:N2}`, gets build warning `LOC502`, in a set and in an inline text. A text shows its wording as written, so nothing would fill the placeholder in. Braces around anything but a number are text. A string passed to a method, such as the format of `string.Format`, gets no warning, because the method may fill the placeholder in.

## Equality

A text equals anything that shows the same wording for the current UI culture: another text or a string. For a Dutch user, each of these is true, and for an English user each is false:

```csharp
string wording = "Afrekenen";
List<LocalizableText> texts = [ShopLocalization.Checkout];

ShopLocalization.Checkout == wording
ShopLocalization.Checkout.Equals(wording)
ShopLocalization.Checkout == LocalizableText.Inline(en: "Pay", nl: "Afrekenen")
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

## What the source generator writes

`LocalizableText.Inline` gets one required parameter per declared language, named after it: `enGb` for `en-GB`. The attribute `[Localized]` gets the same parameters ([Attributes](attributes.md)).

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

The contract takes the texts the default-language set declares itself, overrides included. A text it only inherits from a class of its assembly would be left out, so that's build error `LOC208`. A text that only another language declares isn't in the contract either, so no user sees it. The build warns about it (`LOC209`).

Only a class that implements `ILocalizationSet` is a set, whatever its name. A class that implements only a contract, such as a test double, isn't one, so the build doesn't read a language from its name. A set that derives from another set, such as a regional one, implements `ILocalizationSet` through that set.

Each generated text shows its wording in every language and links to it. Hovering over `ShopLocalization.Checkout` shows `nl: Afrekenen`, and you can jump from there to `ShopLocalizationNl.Checkout`. The generated class is partial, so a helper can sit next to the texts. Besides the texts it holds only the method `GetTextByKey`, and `GetTextByValue` and `GetText` in a [set of an enum](enums.md#a-set-of-the-enum). So a text can take any name except those and the name of the class itself (`LOC205`).

Each generated text is a `LocalizableText` with two keys:

- Its `Key` is its name, `Checkout` here. Two classes can each have a text with that key. `GetTextByKey` takes it ([Getting a text by its key](#getting-a-text-by-its-key)).
- Its `FullKey` is its full name, `MyApp.Localization.ShopLocalization.Checkout` here, so every text has a full key of its own.

The package never reads the full key. It's for an app or library that also looks texts up in resource files, such as through `IStringLocalizer`, so a resource file can override a wording without a rebuild ([an example](coming-from-resx.md#keeping-a-resource-file-as-an-override)).

### Getting a text by its key

`GetTextByKey` gives the text with a key. It's for a key your code only has at run time, such as one from a database. Where you know the text, write `ShopLocalization.Checkout`, which the build checks.

```csharp
LocalizableText cart = ShopLocalization.GetTextByKey("Cart"); // The same text as ShopLocalization.Cart
string dutch = cart.ToString(CultureInfo.GetCultureInfo("nl"));
```

- **The key.** It's the name of the text, in the same letter case. A key no text has throws `KeyNotFoundException`, and so does a full key.
- **The values.** A text with parameters takes its values after the key, in the order of its parameters: `ShopLocalization.GetTextByKey("ItemsInCart", cart.Count, shop.Name)`. Each value has to be of the type of its parameter, because a value isn't converted. A string for a `LocalizableText` parameter is the exception. It becomes a text, as it does in a call of the method itself. An `int` for a `long` throws `ArgumentException`, and so does a missing value or one too many.
- **The result.** It's the text, a `LocalizableText`. For a text without parameters, it's the one the class already holds, so a call allocates nothing. Show it for another culture with `ToString(culture)`, as any text.

## Registration

Every set is registered automatically. When an app starts, it registers the sets of every assembly it references, whether it uses that assembly or not. An assembly the app loads later, such as one a Blazor WebAssembly app loads lazily, registers its sets when its code first runs. Loading it alone doesn't.

The source generator writes the code that does this. It's a class named after your assembly, `LocalizationRegistrar_MyApp` for `MyApp`, in the namespace `CodeChops.Idioma.Generated`. The class is public in an assembly that has sets or texts written as attributes, and in an app that declares languages, so an app that references the assembly can register them at startup. It's hidden from autocomplete, but it's still public API. So a library that tracks its public API, such as with Microsoft's public API analyzers, lists the registrar next to the contract and the generated class of every public set.

The texts of members marked `[Localized]` are recorded the same way, by the same generated code ([Attributes](attributes.md#what-the-source-generator-writes)).

A set is registered by its type, and created when the first text is shown. So a set whose constructor throws doesn't stop your app from starting. The first text shown throws an `InvalidOperationException` that names the set, with the set's own exception inside, and so does every text after it.

A set that can't be registered automatically gets build warning `LOC400`, such as a set without a parameterless constructor. [LOC400](build-errors.md#loc400) lists every case and its fix. A member marked `[Localized]` whose text can't be registered, such as a member of a private nested type, gets warning [LOC505](build-errors.md#loc505).

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
- It gives part of an app, or a test, its own sets: `LocalizationRegistry.Create(sets)`.

```csharp
var dutch = CultureInfo.GetCultureInfo("nl");

// The Dutch set itself, whatever the current UI culture
string? checkout = LocalizationRegistry.Current.Find<IShopLocalization>(dutch)?.Checkout;

// A registry with sets of its own, as a test makes one
var registry = LocalizationRegistry.Create([new ShopLocalizationNl()]);
string wording = ShopLocalization.Checkout.ToString(dutch, registry);
```

A text reads its wording from a registry you pass it, as in `ShopLocalization.Checkout.ToString(dutch, registry)` above. Such a registry doesn't depend on your app's languages. It shows every language it has a set for, unless you pass it languages of its own: `LocalizationRegistry.Create(sets, "en", "nl")`. List each language once, because a repeat throws an `ArgumentException`.

A set of a test project may implement the internal contract of the library it tests, which it sees through `InternalsVisibleTo`. It translates that contract, as a set of a public contract does, so the test project needs no languages of its own.

### A contract you write yourself

An interface that derives from `ILocalizationSet` is a contract too, one you write yourself. Every set lists it next to `ILocalizationSet`, the default-language set too:

```csharp
public interface IGridLocalization : ILocalizationSet
{
    string First { get; }
}

public class GridLocalizationEn : ILocalizationSet, IGridLocalization
{
    public string First => "First";
}
```

The build holds every language to it as to a generated contract. Each language needs a set (`LOC201`), and each set uses the values that the default-language set shows (`LOC302`).

The generator writes no contract and no generated class next to it, so its texts have no `LocalizableText` of their own. Read them through the registry, as in `LocalizationRegistry.Current.Find<IGridLocalization>(culture)?.First`.

An interface that doesn't derive from `ILocalizationSet` isn't a contract. One with the name of the contract a set would get, as `IShopLocalization` next to `ShopLocalizationEn`, is build error `LOC200`.

## Texts in JSON

A text survives JSON, such as a Blazor component parameter that crosses a render mode. It's written with every wording it can show: its own translations and, for a text of a set, the wording of every language your app declares that has a registered set, regions included.

Read back, a text:

- shows that wording for every culture, without needing the sets
- keeps its full key, if it had one, and its `Key` gives that full key too
- no longer belongs to a contract, so a set registered later doesn't reword it
- equals the text it was written from, because a text compares by its wording ([Equality](#equality))

```json
{"key":"MyApp.Localization.ShopLocalization.Checkout","defaultText":"Checkout","translations":{"en":"Checkout","nl":"Afrekenen"}}
```

A text written with `LocalizableText.Inline` travels without a `key`. A text on a member travels with its full key, such as `Shop.Domain.Address.City`. A text without translations, such as a customer's name, travels as its wording alone: `"Ada"`.

A text that takes values is written with its values filled in, in every language: `"nl":"3 artikelen in je winkelwagen bij Idioma"`. A text among its values gets the wording of each language too. A date or a number is formatted as for the user the text is written for, in every language, because a language doesn't say which region its reader lives in. With `CurrentUICultureProvider` set, that's the culture it gives. Read back, it shows that wording and no longer has its `Arguments`. It travels without its key too, because a resource file entry found by the key would have no values to fill in.

A JSON string reads as a text too, with that one wording for every culture. So JSON you write by hand, such as a settings file, can hold `"title": "Orders"`.