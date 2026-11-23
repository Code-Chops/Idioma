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
| `ResourceType = typeof(Shop)` in a validation or display attribute | A class of `string` properties that reads your texts                            |
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

Name your texts in an attribute such as `[Required]` or `[Display]` through a class of static `string` properties. An attribute takes only constants, so `[Required(ErrorMessage = ShopLocalization.NameRequired)]` doesn't compile. These attributes do read a static `string` property by its name, as they read the class resx generates:

```csharp
public static class FormTexts
{
    public static string Name => ShopLocalization.Name;
    public static string NameRequired => ShopLocalization.NameRequired;
}

public class Customer
{
    [Display(Name = nameof(FormTexts.Name), ResourceType = typeof(FormTexts))]
    [Required(ErrorMessageResourceName = nameof(FormTexts.NameRequired), ErrorMessageResourceType = typeof(FormTexts))]
    public string? Name { get; set; }
}
```

Each property converts its text when the attribute reads it, so the label and the message follow the user's language. The attribute can't name `ShopLocalization` itself, because its texts are `LocalizableText` and the attribute asks for a `string`.

A message can show the field's label as `{0}`, as in `"Enter {0}"`, because the attribute fills it in. In a set, that wording gets warning `LOC502`. Turn it off around the text with `#pragma warning disable LOC502`.

Code of your own that shows a property's label needs no such class. Mark the property `[Localized(en: "Name", nl: "Naam")]`, and read the label with `LocalizableText.GetTextByMember` ([Write an attribute](../README.md#write-an-attribute)). `[Display]` and the validation attributes don't read that label.

## Keeping a resource file as an override

Each text of a set carries a full key, which is its full name: `MyApp.Localization.ShopLocalization.Checkout` for `ShopLocalization.Checkout` in the namespace `MyApp.Localization`. The package itself never reads it. An app can still let a resource file or a database override a wording through `IStringLocalizer`. Use this when a wording has to change without a rebuild, such as when translators edit a resource file. An extension method on the localizer asks it for the key first, and shows the text where there's no entry:

```csharp
public static class LocalizerExtensions
{
    public static string Show(this IStringLocalizer localizer, LocalizableText text)
    {
        if (text.FullKey is null)
            return text;

        var found = localizer[text.FullKey];

        return found.ResourceNotFound ? text : found.Value;
    }
}
```

An inline text has no key, so `Show` gives its wording. To give a value of your own a key, create it with `LocalizableText.Keyed("Customer.Name", customer.Name)`. It shows `customer.Name` where no resource file has the entry.

`localizer.Show(ShopLocalization.Checkout)` gives the wording of the entry `MyApp.Localization.ShopLocalization.Checkout` where a resource file has one, and the text's own wording where none has.

A text also has a `Key`, which is its name alone: `Checkout`. Use it in place of `FullKey` where each class has a resource file of its own, as with `IStringLocalizer<Shop>`, whose entries are named `Checkout` and `Cart`. Two classes can each have a text with that key, so it doesn't suit a file they share.

Call `Show` wherever a resource file may override a wording, also in Razor markup: `@localizer.Show(ShopLocalization.Checkout)`. A component gets the localizer with `@inject IStringLocalizer<Shop> localizer`. Markup such as `@ShopLocalization.Checkout` and string interpolation show a text through `ToString()`, which doesn't ask the localizer. So a wording shown that way ignores the resource file.

A text that takes values carries them as its `Arguments`, in the order of its parameters. Its entry writes them as placeholders, `{0} items in your cart at {1}` for `ItemsInCart(count, shop)`, and the last line of `Show` fills them in:

```csharp
return found.ResourceNotFound ? text : string.Format(found.Value, text.Arguments.AsSpan());
```

Such a text loses its key when it travels through JSON, as a Blazor component parameter can. It's read back with its values filled in and without its `Arguments`, so `Show` gives that wording.

Such an entry has one wording for every value. The method, by contrast, can choose between wordings.