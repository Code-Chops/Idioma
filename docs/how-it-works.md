# How Idioma works

What happens behind the [getting started guide](../README.md#getting-started): which wording a user sees, what the source generator writes, texts that take values, how sets register, how to read a set yourself, and how a text travels through JSON.

Other subjects have a page of their own: [language codes](language-codes.md), [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), [what the build rejects](build-errors.md), and [coming from resx](coming-from-resx.md).

## Which wording a user sees

A text follows `CultureInfo.CurrentUICulture`, and takes the first wording it finds:

1. The wording for that culture, or else for its parent culture: an `en` wording serves `en-US` and `en-GB` alike, and an `en-GB` wording wins over it in Britain.
2. The wording for the app's default language: the first one it declares. The texts of the libraries the app uses fall back to it as well, so no screen ends up in two languages. A culture none of the app's languages serves starts here, even where a library has its language: an app declaring `("en", "fr")` shows a Dutch user a library's texts in English, like its own.
3. The wording for the default language of the project that wrote the text.

In an app declaring `("en", "nl", "en-GB")`, whose British set words the cart as "Basket":

| The user's culture | `ShopTranslations.Cart` shows | Because                                                                |
|--------------------|-------------------------------|------------------------------------------------------------------------|
| `en-GB`            | Basket                        | It has wording of its own                                              |
| `en-AU`            | Cart                          | Its parent culture, `en`, has wording                                  |
| `nl-NL`            | Winkelwagen                   | Its parent culture, `nl`, has wording                                  |
| `de-DE`            | Cart                          | None of the app's languages serves it, so it gets the default language |

`ToString` follows the same order for the culture you pass. `Format` formats its values for `CultureInfo.CurrentCulture`, or for the culture you pass, so a placeholder can carry a format, as in `{0:N2}` or `{0:d}`. It always reads the wording as a format string, even without values, so write a literal brace as `{{` or `}}`.

## What the source generator writes

`LocalizableText.Inline` gets one required parameter per declared language, named after it: `enGB` for `en-GB`.

From the set of the default language, `ShopTranslationsEN` in the namespace `MyApp.Translations`, it writes the contract `IShopTranslations` and the class `ShopTranslations`. In outline:

```csharp
namespace MyApp.Translations;

public interface IShopTranslations : ITranslationSet
{
    string Cart { get; }
    string Checkout { get; }
}

partial class ShopTranslationsEN : IShopTranslations;

public static partial class ShopTranslations
{
    public static ResourceText Cart { get; } = ...;     // Its key is "MyApp.Translations.ShopTranslations.Cart"
    public static ResourceText Checkout { get; } = ...; // Its key is "MyApp.Translations.ShopTranslations.Checkout"
}
```

Each generated text shows its wording in every language and links to it, so hovering over `ShopTranslations.Checkout` shows `nl: Afrekenen`, and you can jump from there to `ShopTranslationsNL.Checkout`. The generated class is partial, so a helper can sit next to the texts. It holds nothing but the texts, so a text can take any name except that of the class itself.

Each generated text is a `ResourceText`: a `LocalizableText` with a key, which is its full name, `MyApp.Translations.ShopTranslations.Checkout` here, so each text has a key of its own. The package never reads the key: it is for an app or library that also looks texts up in resource files, such as through `IStringLocalizer`, so a resource file can override a wording without a rebuild ([an example](coming-from-resx.md#keeping-a-resource-file-as-an-override)).

The source generator also writes the code that registers your sets (see [Registration](#registration)): a class named after your assembly, `TranslationsRegistrar_MyApp` for `MyApp`, in the namespace `CodeChops.Idioma.Generated`. In an assembly that has sets, or an app that declares languages, that class is public, so that an app referencing the assembly can register them as it starts. It is hidden from autocomplete, but a library that keeps a list of its public API finds it there, next to the contract and the class of every public set.

## Texts that take values

A public method of the default-language set that returns a `string` and takes parameters is a text that takes values ([an example](../README.md#write-a-text-that-takes-values)). The contract repeats the method, so every language implements it, and the generated class gets a method of the same name and parameters:

```csharp
public interface IShopTranslations : ITranslationSet
{
    string ItemsInCart(int count, string shop);
}

public static partial class ShopTranslations
{
    public static ResourceText ItemsInCart(int count, string shop) => ...; // Its key is "MyApp.Translations.ShopTranslations.ItemsInCart"
}
```

Each call creates a text that keeps the values, as its `Arguments`, and words itself with them when it is shown. So it follows the user's language like any other text: one created for an English user and shown to a Dutch one reads in Dutch, and `ToString(culture)` gives the wording of the culture you pass. Its key is its full name, without its parameters. Two such texts are equal when their values are.

A method without parameters is not a text: write a text without values as a property. A public method of the default-language set that is not meant as a text, such as a helper, becomes one all the same, and every language then has to implement it: make a helper `private` or `internal`.

The generated class repeats the parameters as the set declares them, which rules out what it could not repeat or keep (`LOC019`):

- a generic method;
- two methods of one name, as a text is told from the others by its name;
- a parameter passed by reference (`ref`, `in`, `out`), a `params` parameter, or a parameter with a default value;
- a parameter that is `dynamic`, a pointer, or a ref struct such as a span.

Every language names the parameters as the default-language set does (`LOC020`). The compiler checks their types and their order, but not their names, so without that check a set could declare two parameters of one type the other way round and word each value as the other.

What else to know:

- **Plurals.** Idioma has no plural rules: each language chooses its wording for a number in its own set, in C#. `count is 1 ? ... : ...` is right for English and Dutch, and a language with more forms, such as Polish, writes a condition for each.
- **How values are formatted.** A value in an interpolated string is formatted for `CultureInfo.CurrentCulture`, as anywhere in C#, and can carry a format, as in `{amount:N2}`. That also holds for the wording of a culture you pass to `ToString`: it comes from the set of that culture, with the values formatted for the current culture.
- **A text as a value.** A value that is a text itself shows the wording for the current UI culture, also inside the wording of another culture you pass to `ToString`.
- **Placeholders.** A text with placeholders, `"Hello, {0}!"` filled in with `Format`, works as before, next to texts that take values.

## Registration

Every set registers itself: an app registers the sets of every assembly it references when it starts, whether it uses that assembly or not. An assembly the app loads later, such as one a Blazor WebAssembly app loads lazily, registers its sets when its code first runs; loading it alone does not.

A set that cannot register itself gets build warning `LOC003`:

- It has a `required` member, which generated code cannot set, no parameterless constructor, or no language code.
- It rewords another assembly's texts from a nested or file-local class, where generated code cannot reach it.

A nested, generic, or file-local class with your own texts is not recognized, so its language is reported as missing (`LOC004`): make it a top-level class that is not generic.

To register a set by hand, mark it `[ExcludeFromTranslationDiscovery]` and call `TranslationsRegistry.Register`, as for a set that needs a constructor argument:

```csharp
[ExcludeFromTranslationDiscovery]
public sealed class BrandedShopTranslationsEN(string shopName) : ShopTranslationsEN
{
    public override string Checkout => $"Pay at {shopName}"; // Checkout is virtual in ShopTranslationsEN
}

// At startup:
TranslationsRegistry.Register(new BrandedShopTranslationsEN(configuration["ShopName"]!));
```

The build still counts it as a set of its language, and still requires that language to be listed (`LOC005`).

## Reading sets yourself

Besides registering, `TranslationsRegistry` does two things:

- It reads a set directly: `TranslationsRegistry.Current.Find<IShopTranslations>(culture)`.
- It gives part of an app, or a test, sets of its own: `TranslationsRegistry.Create(sets)`. A text reads its wording from such a registry where you pass it one, as in `ShopTranslations.Checkout.ToString(culture, registry)`. The registry stands apart from your app's languages as well: it shows every language it has a set for, unless you pass it languages of its own, `TranslationsRegistry.Create(sets, "en", "nl")`.

```csharp
var dutch = CultureInfo.GetCultureInfo("nl");

// The Dutch set itself, whatever the current UI culture
string? checkout = TranslationsRegistry.Current.Find<IShopTranslations>(dutch)?.Checkout;

// A registry with sets of its own, as a test makes one
var registry = TranslationsRegistry.Create([new ShopTranslationsNL()]);
string wording = ShopTranslations.Checkout.ToString(dutch, registry);
```

## Texts in JSON

A text survives JSON, such as a Blazor component parameter that crosses a render mode. It is written with every wording it can show: its own translations, and for a text of a set, the wording of every language your app declares that has a registered set, regions included. Read back, it shows that wording for every culture without needing the sets, and a resource text keeps its key. It belongs to no contract any more, so a set registered later does not reword it.

```json
{"key":"MyApp.Translations.ShopTranslations.Checkout","defaultText":"Checkout","translations":{"en":"Checkout","nl":"Afrekenen"}}
```

An inline text travels without a `key`, and a text without translations, such as a customer's name, as its wording alone: `"Ada"`.

A text that takes values is written with its values filled in, in every language: `"nl":"3 artikelen in je winkelwagen bij Idioma"`. Read back, it shows that wording, and no longer has its `Arguments`.

A JSON string reads as a text too, with that one wording for every culture, so JSON you write by hand, such as a settings file, can hold `"title": "Orders"`.