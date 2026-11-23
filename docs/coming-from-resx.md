# Coming from resx

With resx files and `IStringLocalizer`, a text is a string key in an XML file per language. With Idioma, it's a property in a C# class per language, or a method for a text that takes values. So a lookup at run time becomes a name the compiler checks. This page maps one to the other, shows how to move a resource file, and shows how a resource file can still override a wording.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## What becomes what

| With resx                                                          | With Idioma                                                                     |
|--------------------------------------------------------------------|---------------------------------------------------------------------------------|
| `Shop.resx`, the default language                                  | `ShopLocalizationEn`, the set of the default language                           |
| `Shop.nl.resx`                                                     | `ShopLocalizationNl`                                                            |
| An entry, `<data name="Checkout">`                                 | A property, `public string Checkout => "Checkout";`                             |
| `localizer["Checkout"]`, or `Shop.Checkout` of the generated class | `ShopLocalization.Checkout`                                                     |
| `localizer["Greeting", customer.Name]`, with a `{0}` placeholder   | `ShopLocalization.Greeting(customer.Name)`, a method with a parameter per value |
| `ResourceType = typeof(Shop)` in a validation or display attribute | `ResourceType = typeof(ShopLocalization)`                                        |
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

## Validation and display attributes

An attribute such as `[Required]` or `[Display]` takes only constants, so `[Required(ErrorMessage = ShopLocalization.NameRequired)]` doesn't compile. These attributes do read a public static `string` property by its name, as they read the class resx generates. Each text of a generated class is such a property, so name the generated class:

```csharp
public class Customer
{
    [Display(Name = nameof(ShopLocalization.Name), ResourceType = typeof(ShopLocalization))]
    [Required(ErrorMessageResourceName = nameof(ShopLocalization.NameRequired), ErrorMessageResourceType = typeof(ShopLocalization))]
    public string? Name { get; set; }
}
```

The attribute reads the property each time it needs the wording, so the label and the message follow the user's language. The set has to be public, because these attributes read only a public class.

A message can show the field's label as `{0}`, as in `"Enter {0}"`, because the attribute fills it in. In a set, that wording gets warning `LOC502`. Turn it off around the text with `#pragma warning disable LOC502`.

Code of your own that shows a property's label can keep the label on the property. Mark the property `[Localized(en: "Name", nl: "Naam")]`, and read the label with `LocalizableText.GetTextByMember` ([Write an attribute](../README.md#write-an-attribute)). `[Display]` and the validation attributes don't read that label.

## Keeping a resource file as an override

An app can let a resource file or a database override a wording through `IStringLocalizer`. Use this when a wording has to change without a rebuild, such as when translators edit a resource file. The key of a text is its name. An extension method on the localizer asks it for that key first, and gives the text where there's no entry:

```csharp
public static class LocalizerExtensions
{
    public static string Show(this IStringLocalizer localizer, string key, string text)
    {
        var found = localizer[key];

        return found.ResourceNotFound ? text : found.Value;
    }
}
```

`localizer.Show(nameof(ShopLocalization.Checkout), ShopLocalization.Checkout)` gives the wording of the entry `Checkout` where a resource file has one, and the text's own wording where none has. With `nameof`, the key follows the text when you rename it.

Two classes can each have a text named `Checkout`. So give each class a resource file of its own, as `IStringLocalizer<Shop>` does, or put the class in the key: `"ShopLocalization.Checkout"`.

Call `Show` wherever a resource file may override a wording, also in Razor markup: `@localizer.Show(nameof(ShopLocalization.Checkout), ShopLocalization.Checkout)`. A component gets the localizer with `@inject IStringLocalizer<Shop> localizer`. Markup such as `@ShopLocalization.Checkout` doesn't ask the localizer, so a wording shown that way ignores the resource file.

For a text that takes values, the entry writes them as placeholders: `{0} items in your cart at {1}` for `ItemsInCart(count, shop)`. Fill them in where there's an entry:

```csharp
var found = localizer[nameof(ShopLocalization.ItemsInCart)];

string items = found.ResourceNotFound
    ? ShopLocalization.ItemsInCart(count, shop)
    : string.Format(found.Value, count, shop);
```
Such an entry has one wording for every value. The method, by contrast, can choose between wordings.