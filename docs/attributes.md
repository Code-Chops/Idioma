# Attributes

`[Localized]` is the third way to write a text, next to a set and an inline text ([an example](../README.md#write-an-attribute)). It puts a text on the member it names, which is a property, a field, a member of an enum, or a type. That suits a label, which a form, a table or an export shows. Such a text carries its own wording in every language, as an inline text does, and it belongs to no set.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## Finding a text by its member

`LocalizableText.GetTextByMember` gives the text of a member:

- `GetTextByMember<Address>(nameof(Address.City))` is for a type your code knows.
- `GetTextByMember(type, "City")` is for a type your code only has at run time.
- `GetTextByMember(property)` is for a member your code holds, such as the `PropertyInfo` of a form field. Only its name and the type that declares it are read. A form can fall back to the property's name: `string label = LocalizableText.GetTextByMember(property) ?? property.Name;`.

`LocalizableText.GetTextOfType` gives the text of a type itself, the same way: `GetTextOfType<Address>()`, or `GetTextOfType(type)` for a type your code only has at run time.

Each gives `null` where there's no text. The `string` is the wording for the user's language at the call, so read it where you show it. A call that names its type and its member in the code, such as `GetTextByMember<Address>(nameof(Address.City))`, is build error `LOC504` when the member has no text ([details](build-errors.md#loc504)).

A member of an enum has a text the same way, and a value of the enum gives it with `GetText()` ([Enums](enums.md#an-attribute-on-each-member)).

## What the source generator writes

The source generator reads each `[Localized]` as the project builds, and writes the text as code. For `Address.City` in the namespace `Shop.Domain`, in outline:

```csharp
// The wording of each marked member, in the order of the assembly's languages (en, nl)
string[] city = ["City", "Plaats"];

// One function per assembly, which the assembly's registrar records
static string[]? Find(Type type, string? member)
{
    if (type == typeof(Shop.Domain.Address))
        return member switch { "City" => city, _ => null };

    return null;
}
```

So nothing reads the attribute at run time, and no assembly is scanned. The attribute itself isn't part of the compiled assembly, because its wording would be stored there a second time.

`GetTextByMember` and `GetTextOfType` ask the recorded functions, keep the answer, and give the wording for the user's language.

## What else to know

- **Inherited members.** A class without a text for a member gets that of its nearest base class. So an override without `[Localized]` keeps the text of the property it overrides. A type's own text isn't inherited, and neither are the texts of an interface.
- **Types with type arguments.** `Page<Order>` gives the texts of `Page<T>`.
- **No values.** An attribute takes constants, so the wording can't show a value. Write a text with a value as a method of a set. A placeholder such as `{0}` gets warning `LOC502`.
- **A type the generated code can't name.** A member of a private or protected nested type, or of a file-local type, gets no text (`LOC505`). Make the type internal or public.
- **Markup.** A member marked `[Localized]` in the `@code` of a `.razor` file gets no text either (`LOC505`), because the source generator can't read what Razor generates. Write the type in a C# file.
- **An indexer, and the field behind a property.** Neither has a text to find, so `[Localized]` on an indexer, or written as `[field: Localized]` on a property, gets `LOC505` too.
- **Registration.** A text is found once its assembly's registrar ran ([Registration](advanced-topics.md#registration)). An app that declares its languages calls the registrar of every assembly it references when it starts. So the label of a library's property is there before any code of the library ran.
- **Libraries.** No app can add a language to a text a library writes as an attribute, because it carries its wording itself, as an inline text does. An app with a language the library lacks gets warning `LOC601`, which counts such texts among the library's inline texts.
- **An attribute of your own named `Localized`.** The generated attribute lies in the global namespace, and C# decides which of the two a member gets:
  - Yours wins when it lies in a namespace or type around the member, or behind a `using` written inside a namespace. Such a member is yours, and gets no text.
  - The generated one wins over yours when only a `using` at the top of the file, or a `global using`, brings yours in reach. Such a member gets its text.
