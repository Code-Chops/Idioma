# Coming from resx

With resx files and `IStringLocalizer`, a text is a string key in an XML file per language. With Idioma, it's a property in a C# class per language, or a method for a text that takes values. So a lookup at runtime becomes a name the compiler checks. This page maps one to the other, shows how to move a resource file, and shows how a resource file can still override a wording.

Other subjects have a page of their own: [language codes](language-codes.md), [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), [what the build rejects](build-errors.md), [performance](performance.md), and [how Idioma works](how-it-works.md).

## What becomes what

| With resx                                                          | With Idioma                                                                     |
|--------------------------------------------------------------------|---------------------------------------------------------------------------------|
| `Shop.resx`, the default language                                  | `ShopLocalizationEn`, the set of the default language                           |
| `Shop.nl.resx`                                                     | `ShopLocalizationNl`                                                            |
| An entry, `<data name="Checkout">`                                 | A property, `public string Checkout => "Checkout";`                             |
| `localizer["Checkout"]`, or `Shop.Checkout` of the generated class | `ShopLocalization.Checkout`                                                     |
| `localizer["Greeting", customer.Name]`, with a `{0}` placeholder   | `ShopLocalization.Greeting(customer.Name)`, a method with a parameter per value |
| `AddLocalization()` and an injected `IStringLocalizer<Shop>`       | Nothing to register or inject                                                   |
| A mistyped key shows as the key itself                             | A mistyped name doesn't compile                                                 |
| An entry missing from `Shop.nl.resx` falls back to the default     | A text missing from `ShopLocalizationNl` doesn't compile                        |
| A satellite assembly per language                                  | Every language compiled into your assembly                                      |

Both follow `CultureInfo.CurrentUICulture`, so the way your app sets the user's language stays as it is.

## Moving a resource file

You can move one resource file at a time, because the two work side by side:

1. Declare your languages, as in the [getting started guide](../README.md#declare-your-languages).
2. Write the default-language set from the default resource file, with a property per entry. A key that isn't a valid C# name, such as `checkout-button`, needs a new name. An entry with placeholders becomes a method with a parameter per value ([Texts with parameters](../README.md#texts-with-parameters)).
3. Write a set for each other language from its resource file. The build names every text a set lacks.
4. Replace each lookup with the text, and delete the resource files. Once the injected localizer or the class resx generated is gone, the compiler points at every lookup that's left.

## Keeping a resource file as an override

Each text of a set carries a key, which is its full name: `MyApp.Localization.ShopLocalization.Checkout` for `ShopLocalization.Checkout` in the namespace `MyApp.Localization`. The package itself never reads it. An app can still let a resource file or a database override a wording through `IStringLocalizer`. An extension method on the localizer asks it for the key first, and shows the text where there's no entry:

```csharp
public static class LocalizerExtensions
{
    public static string Show(this IStringLocalizer localizer, LocalizableText text)
    {
        if (text.Key is null)
            return text;

        var found = localizer[text.Key];

        return found.ResourceNotFound ? text : found.Value;
    }
}
```

An inline text has no key, so `Show` gives its wording. To give a value of your own a key, create it with `LocalizableText.Keyed("Customer.Name", customer.Name)`. It shows `customer.Name` where no resource file has the entry.

`localizer.Show(ShopLocalization.Checkout)` gives the wording of the entry `MyApp.Localization.ShopLocalization.Checkout` where a resource file has one, and the text's own wording where none has.

Call `Show` wherever a resource file may override a wording, also in Razor markup: `@localizer.Show(ShopLocalization.Checkout)`. A component gets the localizer with `@inject IStringLocalizer<Shop> localizer`. Markup such as `@ShopLocalization.Checkout` and string interpolation show a text through `ToString()`, which doesn't ask the localizer. So a wording shown that way ignores the resource file.

A text that takes values carries them as its `Arguments`, in the order of its parameters. Its entry writes them as placeholders, `{0} items in your cart at {1}` for `ItemsInCart(count, shop)`, and the last line of `Show` fills them in:

```csharp
return found.ResourceNotFound ? text : String.Format(found.Value, text.Arguments.AsSpan());
```

Such a text loses its key when it travels through JSON, as a Blazor component parameter can. It's read back with its values filled in and without its `Arguments`, so `Show` gives that wording.

Such an entry has one wording for every value. The method, by contrast, can choose between wordings.