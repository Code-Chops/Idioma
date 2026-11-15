# Coming from resx

With resx files and `IStringLocalizer`, a text is a string key in an XML file per language. Here it is a property in a C# class per language, or a method for a text that takes values, so what was a lookup at run time is a name the compiler checks. This page maps one to the other, shows how to move a resource file, and how a resource file can still override a wording.

Other subjects have a page of their own: [how Idioma works](how-it-works.md), [language codes](language-codes.md), [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), and [what the build rejects](build-errors.md).

## What becomes what

| With resx                                                             | With Idioma                                                                    |
|-----------------------------------------------------------------------|--------------------------------------------------------------------------------|
| `Shop.resx`, the default language                                     | `ShopTranslationsEN`, the set of the default language                          |
| `Shop.nl.resx`                                                        | `ShopTranslationsNL`                                                           |
| An entry, `<data name="Checkout">`                                    | A property, `public string Checkout => "Checkout";`                            |
| `localizer["Checkout"]`, or `Shop.Checkout` of the generated class    | `ShopTranslations.Checkout`                                                    |
| `localizer["Greeting", customer.Name]`                                | `ShopTranslations.Greeting.Format(customer.Name)`                              |
| `localizer["ItemsInCart", count, shop.Name]`, with values by position | `ShopTranslations.ItemsInCart(count, shop.Name)`, with values by name and type |
| `AddLocalization()` and an injected `IStringLocalizer<Shop>`          | Nothing to register or inject                                                  |
| A mistyped key shows as the key itself                                | A mistyped name does not compile                                               |
| An entry missing from `Shop.nl.resx` shows in the default language    | A text missing from `ShopTranslationsNL` does not compile                      |
| A satellite assembly per language                                     | Every language compiled into your assembly                                     |

Both follow `CultureInfo.CurrentUICulture`, so the way your app sets the user's language stays as it is.

## Moving a resource file

The two work side by side, so you can move one resource file at a time:

1. Declare your languages, as in the [getting started guide](../README.md#declare-your-languages).
2. Write the set of your default language from the default resource file, with a property per entry. A key that is not a valid C# name, such as `checkout-button`, needs a new name. An entry with placeholders can stay a property, filled in with `Format`, or become a method with a parameter per value ([Write a text that takes values](../README.md#write-a-text-that-takes-values)).
3. Write a set for each other language from its resource file. The build names every text a set lacks.
4. Replace each lookup with the text, and delete the resource files. Once the injected localizer or the generated class is gone, the compiler points at every lookup that is left.

## Keeping a resource file as an override

Each text of a set carries a key, which is its full name: `MyApp.Translations.ShopTranslations.Checkout` for `ShopTranslations.Checkout` in the namespace `MyApp.Translations`. The package itself never reads it. An app that still looks wording up through `IStringLocalizer`, to let a resource file or a database change a wording, asks it for that key first and shows the text for the rest:

```csharp
string Show(ResourceText text)
{
    var found = localizer[text.Key];

    return found.ResourceNotFound ? text : found.Value;
}
```

`Show(ShopTranslations.Checkout)` gives the wording of the entry `MyApp.Translations.ShopTranslations.Checkout` where a resource file has one, and the text's own wording where none has.

A text that takes values carries them as its `Arguments`, in the order of its parameters. Its entry writes them as placeholders, `{0} items in your cart at {1}` for `ItemsInCart(count, shop)`, and the app fills them in:

```csharp
return found.ResourceNotFound ? text : String.Format(found.Value, text.Arguments.AsSpan());
```

Such an entry has one wording for every value, where the method can choose between wordings.
