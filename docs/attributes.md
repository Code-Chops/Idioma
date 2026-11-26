# Attributes

`[Localized]` is a different way to write a text, next to a set and an inline text ([an example](../README.md#write-an-attribute)). It puts a text on the member it names, which is a property, a field, a member of an enum, or a type. That suits a label, which a form, a table or an export shows. Such a text carries its own wording in every language, as an inline text does, and it belongs to no set.

[`AttributeDemo.cs`](../demos/AttributeDemo.cs) is a complete app that writes its texts as attributes on a class and its properties. It's one file, which `dotnet run` runs without a project.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## Finding a text by its member

`LocalizedText.GetTextByMember` gives the text of a member:

- `GetTextByMember<Address>(nameof(Address.City))` is for a type your code knows.
- `GetTextByMember(type, "City")` is for a type your code only has at run time.
- `GetTextByMember(property)` is for a member your code holds, such as the `PropertyInfo` of a form field. Only its name and the type that declares it are read. A form can fall back to the property's name: `string label = LocalizedText.GetTextByMember(property) ?? property.Name;`.

`LocalizedText.GetTextOfType` gives the text of a type itself, the same way: `GetTextOfType<Address>()`, or `GetTextOfType(type)` for a type your code only has at run time.

Each gives `null` where there's no text. The `string` is the wording for the user's language at the call, so read it where you show it. A call that names its type and its member in the code, such as `GetTextByMember<Address>(nameof(Address.City))`, is build error `LOC601` when the member has no text ([details](build-errors/LOC6XX-reading-a-text.md#loc601)).

A member of an enum has a text the same way, and a value of the enum gives it with `GetText()` ([Enums](enums.md#an-attribute-on-each-member)).

## What else to know

- **Records.** On a parameter of a record, write `property:` in front, as in `public record Line([property: Localized(en: "Quantity", nl: "Aantal")] int Quantity);`. That puts the text on the property the record makes of the parameter, which a text is found by. Without `property:`, the attribute doesn't compile on a parameter (`CS0592`).
- **Inherited members.** A class without a text for a member gets that of its nearest base class. So an override without `[Localized]` keeps the text of the property it overrides. A type's own text isn't inherited, and neither are the texts of an interface.
- **Types with type arguments.** `Page<Order>` gives the texts of `Page<T>`.
- **No values.** An attribute takes constants, so the wording can't show a value. Write a text with a value as a method of a set. A placeholder such as `{0}` gets warning `LOC403`.
- **A type the generated code can't name.** A member of a private or protected nested type, or of a file-local type, gets no text (`LOC502`). Make the type internal or public. A member of a type marked `[Obsolete]` as an error gets no text either.
- **A member of an enum marked `[Obsolete]` as an error.** It keeps its text, because stored data may still hold its value. `GetText()` gives that value the text.
- **Markup.** With SDK 10, a `[Localized]` in a `.razor` file gives its member no text either (`LOC502`), because the source generator can't read what Razor generates. That goes for a member in `@code`, and for `@attribute [Localized(...)]` on the component. Write the member, or the attribute on the component, in a code-behind file instead, such as `partial class OrderForm` in `OrderForm.razor.cs`. A class declared in `@code` moves to a C# file as a whole. From SDK 11 on, Razor writes both to a file that the source generator reads, so they get their text.
- **An indexer, and the field behind a property.** Neither has a text to find, so `[Localized]` on an indexer, or written as `[field: Localized]` on a property, gets `LOC502` too.
- **Another library's `[Localized]`.** Where a file has a `using` of both `CodeChops.Idioma` and another library with a `LocalizedAttribute`, the name is ambiguous (`CS0104`). Write one of them with its namespace, as in `[Other.Localized("Model.Name")]`.
- **Libraries.** No app can add a language to a text a library writes as an attribute, because it carries its wording itself, as an inline text does. An app with a language the library lacks gets warning `LOC701`, which counts such texts among the library's inline texts. The members of a library's enum are the exception: a set of the enum in the app gives `GetText()` its wording in every language ([Enums](enums.md#a-set-of-the-enum)).