# Coming from resx

With resx files and `IStringLocalizer`, a text is a string key in an XML file per language. With Idioma, it's a property in a C# class per language, or a method for a text that takes values. So a lookup at run time becomes a name the compiler checks. This page maps one to the other.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## What becomes what

| With resx                                                          | With Idioma                                                                     |
|--------------------------------------------------------------------|---------------------------------------------------------------------------------|
| `Shop.resx`, the default language                                  | `ShopLocalizationEn`, the set of the default language                           |
| `Shop.nl.resx`                                                     | `ShopLocalizationNl`                                                            |
| An entry, `<data name="Checkout">`                                 | A property, `public string Checkout => "Checkout";`                             |
| `localizer["Checkout"]`, or `Shop.Checkout` of the generated class | `ShopLocalization.Checkout`                                                     |
| `localizer[key]`, with a key known only at run time                | `ShopLocalization.GetTextByKey(key)`, which gives `null` for an unknown key     |
| `localizer["Greeting", customer.Name]`, with a `{0}` placeholder   | `ShopLocalization.Greeting(customer.Name)`, a method with a parameter per value |
| `ResourceType = typeof(Shop)` in a validation or display attribute | `ResourceType = typeof(ShopLocalization)`                                        |
| `AddLocalization()` and an injected `IStringLocalizer<Shop>`       | Nothing to register or inject                                                   |
| A mistyped key shows as the key itself                             | A mistyped name doesn't compile                                                 |
| An entry missing from `Shop.nl.resx` falls back to the default     | A text missing from `ShopLocalizationNl` doesn't compile                        |
| A satellite assembly per language                                  | Every language compiled into your assembly                                      |

Both follow `CultureInfo.CurrentUICulture`, so the way your app sets the user's language stays as it is. The two work side by side, so you can move one resource file at a time.

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

A message can show the field's label as `{0}`, as in `"Enter {0}"`, because the attribute fills it in. The build sees that the attribute names the text as its error message, so the placeholder gets no warning (`LOC403`). Where the attribute is in another project than the texts, mark the text `[StringSyntax(StringSyntaxAttribute.CompositeFormat)]` instead ([details](build-errors/LOC4XX-texts-with-parameters.md#loc403)). A display name is shown as written, so a `{0}` there is still reported.

Keep such a text a property. The attribute finds it by its name at run time, and a method of that name would make it throw. The IDE fix for `LOC403` leaves a text alone that an attribute or a `nameof` names.

Code of your own that shows a property's label can keep the label on the property. Mark the property `[Localized(en: "Name", nl: "Naam")]`, and read the label with `LocalizedText.GetTextByMember` ([Write an attribute](../README.md#write-an-attribute)). `[Display]` and the validation attributes don't read that label.