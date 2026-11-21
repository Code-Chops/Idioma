# What the build rejects

Idioma turns translation mistakes into build errors. Each message names what's wrong and what to change:

```text
error LOC500: This text is in one language, but this assembly supports en, nl. Write it as LocalizableText.Inline(en: "...", nl: "...").
```

This page lists every code, grouped by subject, with an example and the fix. Your IDE offers a fix for the codes marked *(code fix)*.

Other subjects have a page of their own: [language codes](language-codes.md), [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), [coming from resx](coming-from-resx.md), [performance](performance.md), and [advanced topics](advanced-topics.md).

## All codes

The hundreds of a code name its subject. The subjects come in the order of this page.

| Code                                                | Mistake                                                 |
|-----------------------------------------------------|---------------------------------------------------------|
| **[Languages](#languages)**                         |                                                         |
| [`LOC100`](#loc100)                                 | A set or inline text in an assembly without languages   |
| [`LOC101`](#loc101)                                 | An invalid or repeated language                         |
| [`LOC102`](#loc102)                                 | A C# version older than 14                              |
| **[Sets](#sets)**                                   |                                                         |
| [`LOC200`](#loc200) (code fix)                      | A default-language set the generator can't use          |
| [`LOC201`](#loc201) (code fix)                      | A language without a set                                |
| [`LOC202`](#loc202)                                 | A set for a language that isn't listed                  |
| [`LOC203`](#loc203)                                 | A set deriving from another language, as `nl` from `en` |
| [`LOC204`](#loc204) (code fix)                      | A set that doesn't list its contract                    |
| [`LOC205`](#loc205)                                 | A text with a name the generated class can't give it    |
| [`LOC206`](#loc206) (code fix)                      | A text with a setter                                    |
| [`LOC207`](#loc207)                                 | A text hidden instead of overridden                     |
| [`LOC208`](#loc208)                                 | A text the default-language set only inherits           |
| **[Texts with parameters](#texts-with-parameters)** |                                                         |
| [`LOC300`](#loc300)                                 | A public method that can't be a text                    |
| [`LOC301`](#loc301)                                 | A parameter named differently than in the contract      |
| [`LOC302`](#loc302)                                 | A value that a translation never uses                   |
| [`LOC303`](#loc303) (warning, code fix)             | A text passed to a `string` parameter of a text         |
| **[Registration](#registration)**                   |                                                         |
| [`LOC400`](#loc400) (warning)                       | A set or a label that can't be registered automatically |
| [`LOC401`](#loc401) (code fix)                      | A `LocalizationLanguageCode` that implements nothing    |
| [`LOC402`](#loc402)                                 | A `LocalizationLanguageCode` that isn't a culture name  |
| **[Wording](#wording)**                             |                                                         |
| [`LOC500`](#loc500)                                 | Wording written in one language                         |
| [`LOC501`](#loc501) (code fix)                      | A language argument without its name                    |
| [`LOC502`](#loc502) (warning, code fix)             | A placeholder such as `{0}` in a wording                |
| [`LOC503`](#loc503) (warning)                       | An enum member without a label next to members with one |
| [`LOC504`](#loc504)                                 | A text asked of a member or a type that has none        |
| **[Libraries](#libraries)**                         |                                                         |
| [`LOC600`](#loc600) (code fix)                      | A library without a set for one of your languages       |
| [`LOC601`](#loc601) (warning)                       | A library whose inline texts lack one of your languages |

## Languages

### LOC100

A set lists `ILocalizationSet` next to a contract of its own assembly, but the assembly declares no languages. The generator needs the languages to write the contract. Add `[assembly: SupportedLanguages("en", "nl")]` with your languages, the default first.

A set that rewords a library's set, or adds a language to a library's contract, needs no languages.

A call of `LocalizableText.Inline` in such an assembly is LOC100 too. The generator writes `Inline` with one parameter per language, so without languages the method doesn't exist, and the compiler reports `CS0117` next to it.

So is a member marked `[Localized]`. The generator writes that attribute with one parameter per language too, so without languages the compiler reports `CS0246` next to it.

### LOC101

`[assembly: SupportedLanguages]` lists a language that isn't a culture name, or lists one twice. Both are build errors. List each language once, as a culture name such as `en` or `en-GB`.

### LOC102

The project declares its languages and compiles with a version of C# older than 14. Remove `<LangVersion>` from the project file, or set it to 14 or later. C# 14 is the default on .NET 10.

```text
error LOC102: Idioma needs C# 14 or later, and this project uses C# 13.0. Remove <LangVersion> from the project file, or set it to 14 or later.
```

The code generated for your languages uses extension members, which C# 14 brings. So the generators write none of it, and the compiler reports what your code then misses, such as the contract of a set (`CS0246`) or `LocalizableText.Inline` (`CS0117`).

A project that declares no languages builds with an older version, such as one that only references a library that uses Idioma.

## Sets

### LOC200

The generator can't use the default-language set, so it writes no texts for it. A default-language set it can use:

- is named after its language, such as `ShopLocalizationEn`
- is top-level, so not nested
- isn't file-local, generic, or abstract
- has a public parameterless constructor
- has no `required` members
- isn't marked `[Obsolete]` as an error
- is the only default-language set of its contract
- is a set of no other contract, such as a library's
- has a name that gives its generated class a name no other type or namespace has
- has a name that gives its contract a name no other type has

The generator still writes the contract when the only problem is one of these:

- the constructor
- a `required` member
- `[Obsolete]`
- the name of the generated class

Otherwise it writes no contract either. The contract your sets list then doesn't exist, so they don't compile (`CS0246`) until you fix this error. The message ends in what to change.

Two sets are the default-language set of one contract when their names differ only in the letter case of the language, as `ShopLocalizationEn` and `ShopLocalizationEN` do. The generator writes the contract from the first by name and reports the other. Remove one of them, or rename it.

The generated class is named after the set without its language, so `OrderStatusEn` next to an enum `OrderStatus` would give a second `OrderStatus`. Rename the set, as in `OrderStatusLocalizationEn`, or the other type. A static partial class of that name is fine, because it's a part of the generated class. A namespace of that name counts too, as the folder `Texts` gives `MyApp.Texts` next to `TextsEn`.

An enum in another namespace counts when the set names it by its name alone, as `ILocalizationSet<OrderStatus>` does behind `using Domain;`. Inside the set's namespace, the generated class would stand before the enum, so that name would mean the generated class.

The contract is named after the set too, so an interface `IShopLocalization` of your own next to `ShopLocalizationEn` would give a second `IShopLocalization`. Rename the set or your interface. An interface that derives from `ILocalizationSet` is a contract you write yourself, and the generator writes none next to it ([a contract you write yourself](advanced-topics.md#a-contract-you-write-yourself)).

A set that implements another contract, such as `Shop.IShopLocalization` of a library, translates or rewords that contract, and starts none of its own:

```csharp
public class AppLocalizationEn : ILocalizationSet, IAppLocalization, Shop.IShopLocalization // LOC200
```

Write the texts of `IAppLocalization` in a set of their own, and implement `Shop.IShopLocalization` in another class.

The IDE fix renames the class after its language, or removes `abstract` or `file`.

### LOC201

A contract has no set for one of your languages. The message names the class to add:

```text
error LOC201: 'ShopLocalization' has no set for 'de', which [assembly: SupportedLanguages] requires.
Add 'class ShopLocalizationDe : ILocalizationSet, IShopLocalization'. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.
```

The IDE fix adds that class in a file of its own, without texts. The compiler then names each text the set lacks (`CS0535`), and your IDE can add them for you to translate. "Fix all" adds the set of every missing language at once.

This also applies to a listed region, such as `en-GB` next to `en`, and to a contract you wrote by hand. A regional set derives from the set of its language and holds only the texts that differ.

A class that implements the contract without `ILocalizationSet` doesn't count as a set, whatever its name. To make it the set of its language, add `ILocalizationSet` next to the contract.

### LOC202

A set is for a language that `[assembly: SupportedLanguages]` doesn't list. Add the language to the attribute, or remove the set.

This also applies to a set you register yourself.

### LOC203

A set derives from a set of another language, so every text it doesn't override stays in that other language:

```csharp
public class ShopLocalizationNl : ShopLocalizationEn // Every text it doesn't override stays English
```

To translate, list `ILocalizationSet` and the contract instead (`: ILocalizationSet, IShopLocalization`), which requires every text. Derive from a set only to reword some of its texts in the same language.

A regional set may derive from the set of its language:

```csharp
public class ShopLocalizationEnGb : ShopLocalizationEn // Texts it doesn't override fall back to English
```

One region can't derive from another, because a region doesn't fall back to another region. `ShopLocalizationEnUs : ShopLocalizationEnGb` is LOC203.

### LOC204

A set lists `ILocalizationSet` without its contract, so no text reads it. This also applies to the default-language set. Add the contract next to `ILocalizationSet`, as in `class ShopLocalizationNl : ILocalizationSet, IShopLocalization`. The IDE fix does this for you.

### LOC205

A text has a name the generated class can't give it, so that text isn't generated. Rename the text. Four names are taken:

- The name of the generated class, as in `ShopLocalization.ShopLocalization`. In C#, a member can't have the name of its own class.
- `GetTextByKey`. The generated class has a method of that name, which gives a [text by its key](advanced-topics.md#getting-a-text-by-its-key).
- `GetTextByValue`, in a [set of an enum](advanced-topics.md#enums). The generated class has a method of that name, which gives the text of an enum value.
- `GetText`, in a set of an enum. The generated class has an extension method of that name, which gives a value its text: `order.Status.GetText()`.

### LOC206

A text has a setter, so its wording can change after the set is created:

```csharp
public string Greeting { get; set; } = "Hallo!"; // LOC206
```

Remove the setter. One instance of each set serves every user, so a change of wording reaches all of them. Any setter counts, also a `private` one.

- To set the wording when the set is created, assign it in the constructor or use `init`.
- To change the wording at run time, read it in the getter, for example from a store.

The IDE fix removes the setter, along with its body. An auto-property can still be assigned in the constructor. Any other assignment stops compiling, which shows you the code that changed the wording.

A member that isn't a text may have a setter, such as a helper marked `[ExcludeFromLocalizationDiscovery]`. The build doesn't catch a getter that returns a field which a method changes.

### LOC207

A set that derives from another hides one of its texts instead of overriding it, so users never see its wording:

```csharp
public class ShopLocalizationEnGb : ShopLocalizationEn
{
    public new string Color => "Colour"; // LOC207: British users still see "Color"
}
```

Make the text `virtual` in the set it derives from, and override it. The set implements the contract through the set it derives from, so the contract calls that set's member. A member of the same name only hides it. Without `new`, the compiler warns about the hiding (`CS0108`), and the IDE's fix for that warning adds `new`.

Where the text can't be virtual, such as a text of a library, list the contract again: `class MyShopLocalizationEn : ShopLocalizationEn, IShopLocalization`. The contract then calls the set's own member.

### LOC208

The default-language set inherits a text from a class of its assembly without declaring it, so its contract leaves the text out:

```csharp
public abstract class ShopBase
{
    public string Brand => "Idioma"; // LOC208: no other language has to word it
}

public class ShopLocalizationEn : ShopBase, ILocalizationSet, IShopLocalization
{
    public string Cart => "Cart";
}
```

Declare the text in the default-language set, by overriding it or by moving it there. The contract is written from the texts that set declares itself, so a text it only inherits isn't in it. No other language then has to word it, and the generated class has no text for it.

A member of the base that isn't a text needs `[ExcludeFromLocalizationDiscovery]`. A class of another assembly isn't read, so a property such as `Exception.Message` isn't reported.

## Texts with parameters

### LOC300

A public method of the default-language set that returns a `string` is a text with parameters. LOC300 means it can't be one, because the method:

- takes no parameters
- is generic
- shares its name with another method of the set
- takes more than eight parameters
- has a parameter that is passed by reference, is `params`, has a default value, or is `dynamic`
- has a parameter of a type a text can't keep until it's shown, such as a pointer or a span
- has the name of an enum member, in a [set of an enum](advanced-topics.md#enums), where the text of a member is a property

A method that can't become a property isn't reported: an override such as `ToString()`, or a method that another interface of the set asks for.

### LOC301

A set names a parameter differently than the contract does. The compiler checks only the types and order of parameters, so two values of one type could swap unnoticed:

```csharp
// The contract has Invited(string host, string guest)
public string Invited(string guest, string host) => $"{host} has invited {guest}"; // LOC301
```

Use the same parameter names in every language.

### LOC302

A translation never uses a value that the default wording shows:

```csharp
public string Greeting(string name) => $"Hello, {name}!"; // In ShopLocalizationEn
public string Greeting(string name) => "Hallo!";          // In ShopLocalizationNl: LOC302, the name is missing
```

Use the value in every language. Any use counts, in a condition or in the wording, so `count is 1 ? "1 artikel" : "meer artikelen"` uses `count`. A value that the default wording only uses to choose between wordings, such as a form of address, may go unused.

An inline text is held to the same rule. The wording of the default language shows a value where it holds one in an interpolated string:

```csharp
LocalizableText.Inline(en: $"Hello, {name}!", nl: "Hallo!") // LOC302: the 'nl' wording doesn't use 'name'
```

A value is a local variable, a parameter, or a field or property of your own assembly. A constant is wording, so it isn't a value. The build reports only what it can read, so these get no error:

- a wording that isn't written in the call, such as a variable or the result of a method
- a wording that calls a local function or a delegate, which may read the value
- a wording that reads a local variable declared with the value or assigned it, as in `var name = user.Name;`
- a wording that reads a local variable the build can't follow, such as one from a pattern, a deconstruction or `out var`

### LOC303

A text is passed to a `string` parameter of a text with parameters. This is a warning:

```csharp
public string PressToPay(string button) => $"Press {button} to pay"; // In ShopLocalizationEn

ShopLocalization.PressToPay(ShopLocalization.Checkout) // LOC303
```

Give the parameter the type `LocalizableText` in every set:

```csharp
public string PressToPay(LocalizableText button) => $"Press {button} to pay";
```

The text becomes a string at the call, in the language in use at that moment. The text around it is worded when it's shown. So a text kept in a static field, or shown with `ToString(culture)`, mixes two languages: Dutch users see "Druk op Checkout om te betalen". A `LocalizableText` parameter keeps the text, which then shows in the language of the wording around it.

To keep the wording of the moment on purpose, call `ToString()` on the text. The build then reports nothing.

The IDE fix changes the parameter's type in every set of your project at once. A call that passes a value, such as a customer's name, still compiles, because a string converts to a text. A call that passes wording written in the source, as in `PressToPay("Enter")`, becomes `LOC500`, because that wording is in one language. A text of a library gets no fix, because your project can't change the library's sets.

An inline text gets no warning for a text in its wording, because it fills its values in at once ([details](advanced-topics.md#texts-with-parameters)).

## Registration

### LOC400

A set can't be registered automatically, so its texts won't be found. This is a warning. The generated code that registers sets needs:

- a parameterless constructor
- no `required` members
- a language code: the end of its name, starting with a capital, such as `En`, or an implementation of `ILocalizationSet.LocalizationLanguageCode` (see [language codes](language-codes.md) for a longer code such as `EnGb`)
- a top-level class that isn't file-local (checked only for a set that rewords another assembly's texts)
- no `[Obsolete]` marked as an error, because generated code can't name such a class

Fix the cause, or mark the set `[ExcludeFromLocalizationDiscovery]` and pass an instance to `LocalizationRegistry.Register`.

A set marked `[Obsolete]` as a warning registers as before, and so does one marked `[Experimental]`. The generated code turns off what naming it reports, so a build that treats warnings as errors doesn't stop there.

A member marked `[Localized]` gets LOC400 when its text can't be registered, so `LocalizableText.GetTextByMember` won't find it:

- Its type is private or protected inside another type, or file-local. The generated code that holds the text can't name such a type. Make the type internal or public.
- A wording is `null`. Write a wording for that language.
- It's written in the `@code` of a `.razor` file. The source generator can't read what Razor generates, so write the type in a C# file.
- The attribute is on an indexer, which has no name to find a text by. Remove it.
- The attribute is on the field behind a property, as in `[field: Localized(...)]`. Write it without `field:`.

```text
warning LOC400: The text of 'Line.Article' can't be registered because generated code can't name 'Line', which is private, so it won't be found. Make 'Line' internal or public.
```

See [attributes on members](advanced-topics.md#attributes-on-members).

### LOC401

A set derives from another set and declares a `LocalizationLanguageCode`, but doesn't list `ILocalizationSet` itself. Its `LocalizationLanguageCode` then implements nothing, and the set serves the language of its name instead:

```csharp
public class ShopLocalizationBritish : ShopLocalizationEn // LOC401: LocalizationLanguageCode is never read
{
    public string LocalizationLanguageCode => "en-GB";
}
```

List the interface again: `class ShopLocalizationBritish : ShopLocalizationEn, ILocalizationSet`. The IDE fix does this for you.

### LOC402

A set's `LocalizationLanguageCode` isn't a culture name, such as `"en_GB"`. Registering the set would throw when the app starts. Return a culture name such as `en`, `en-GB` or `zh-Hans`.

This also applies to a set marked `[ExcludeFromLocalizationDiscovery]`, which would throw where you register it.

## Wording

### LOC500

A text is written in one language, so users of every other language would see it in that language too. These are all LOC500:

```csharp
Title = "Orders";
Title = $"Order {number}";
Title = "Order " + number;
Title = flag ? "Yes" : "No";
Title += " (draft)";
```

A `const` counts too, and so does the same in Razor. Write the text as `LocalizableText.Inline(en: "...", nl: "...")`, or in a set.

Any letter counts, so a name, a unit, or an abbreviation is reported too: `Title = "Idioma"` and `Title = $"{distance} km"` are LOC500. When it reads the same in every language, write the same wording for each: `LocalizableText.Inline(en: "Idioma", nl: "Idioma")`.

Or keep it in a property marked `[ExcludeFromLocalizationDiscovery]`. The build doesn't check that property's initializer or getter:

```csharp
[ExcludeFromLocalizationDiscovery] // Reads the same in every language
public static LocalizableText Brand { get; } = "Idioma";
```

The attribute covers the initializer and the getter, as in `public static LocalizableText Brand => "Idioma";`. Its setter and what other code assigns to the property are still checked, so a marked component parameter still holds its callers to every language.

A lookup by wording is LOC500 too, as in `texts.Contains("Orders")` or `counts["Orders"]` on a collection of texts. Two texts are equal when they show the same wording for the user ([Equality](advanced-topics.md#equality)), so the lookup only finds "Orders" for English users. Look the text up by itself instead: `texts.Contains(ShopLocalization.Orders)`.

LOC500 starts with your second language, because one language has nothing to be translated into. See also [what still converts](#what-still-converts).

### LOC501

An argument of `LocalizableText.Inline` or of `[Localized]` has no language name. Every parameter is a string, so two languages could swap unnoticed. Name each one: `LocalizableText.Inline(en: "Orders", nl: "Bestellingen")`, or `[Localized(en: "City", nl: "Plaats")]`.

The IDE fix names each argument after the language of its position.

### LOC502

A wording holds a placeholder of a format string, such as `{0}`. This is a warning. Nothing fills the placeholder in, so users would see `{0}` as written.

- In a set, write the text as a method that takes the value: `public string Greeting(string name) => $"Hello, {name}!";`.
- In an inline text, write the value into every language: `LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")`.
- On a member, `[Localized]` takes constants, so its wording can't show a value. Write the text as a method of a set.

The build reads only the strings a text returns:

- a string by itself
- the text of an interpolated string
- a part of a concatenation, a conditional (`?:` or `??`), or a `switch`

It doesn't read a string passed to a method, because the method may fill the placeholder in. So `string.Format("{0} items", count)` gets no warning. A string kept in a local variable isn't read either.

A placeholder is a number in braces, as `string.Format` reads it. An alignment or a format may follow the number:

- `{0}`, `{1,8}`, `{1, -8}` and `{0:N2}` are placeholders.
- `{name}`, `{}` and `{1, 2, 3}` are text, so they get no warning.

A wording that shows such braces on purpose, as `"Type {0} to confirm"` might, needs the warning turned off: `#pragma warning disable LOC502` around the text.

The IDE fix writes a parameter in place of each placeholder of a set's text. A method uses its own parameters, the first for `{0}`. A property becomes a method:

- When the contract's text takes no values yet, as in the default-language set, the method takes an `object?` per placeholder number. So `"Hello, {0}!"` becomes `public string Greeting(object? arg0) => $"Hello, {arg0}!";`. Then give each parameter its name and type.
- When the contract's text takes values, the method takes the contract's parameters. So fix the default-language set first, and name its parameters before you fix the other languages.

The fix changes the text in every language of your project at once, so every set still implements the contract. It counts the placeholders of every language, so each language gets the same parameters: `arg0`, `arg1` and so on. A Dutch wording that holds `{0}` alone still takes `arg1` when the English one holds `{1}`. A Dutch wording without a placeholder becomes a method too. The build then reports the value Dutch leaves out (`LOC302`).

Fixing Dutch changes the default-language set too. A set that implements the text explicitly changes with it, and so does one that overrides it.

Code that shows the text, as in `ShopLocalization.Greeting`, then has to pass the value, and the compiler points at each place that does.

The fix changes every language or none. There's no fix when:

- a language's text can't become a method, such as one with a setter or one the constructor assigns
- code of your project uses the text as a property of a set, such as another text that shows it, because that code would no longer compile. The override of the text in a regional set is the exception: its `base.Greeting` becomes `base.Greeting(arg0)`
- it's the text of an enum member, in a [set of the enum](advanced-topics.md#enums), because the contract takes that text from the enum
- the contract's text takes no values and the contract is another assembly's or one you wrote by hand, because the fix doesn't change a contract
- the text overrides a text of another assembly's set

A wording in a raw string keeps its placeholder and its warning, so write its value by hand.

An inline text gets no fix, because its wording has no parameter to take the value. Neither does a label written with `[Localized]`.

### LOC503

A member of an enum has no `[Localized]`, and another member of that enum has one. This is a warning. Users would see the member's name as written, in every language. It's usually a member added later, so mark it too:

```text
warning LOC503: 'ParcelStatus.Lost' has no text, but other members of 'ParcelStatus' have one, so users would see 'Lost' as written. Mark it [Localized(en: "...", nl: "...")].
```

An enum without a marked member gets no warning, also when the enum itself is marked, because its values may never be shown. Neither does a member with the value of a marked member, such as `Default = Active`, because it shows that member's text.

For a member that's never shown, turn the warning off around it: `#pragma warning disable LOC503`.

Where a missing text has to be a build error, write a [set of the enum](advanced-topics.md#enums) instead. An enum that has a set gets its `GetText()` from the set, also while its members are still marked.

### LOC504

A call asks for the text of a member or a type that has none, so it always gives `null`:

```csharp
LocalizableText.GetTextByMember<Address>(nameof(Address.Street)) // LOC504: Street isn't marked [Localized]
LocalizableText.GetTextOfType<Customer>()                        // LOC504: Customer has no text of its own
```

Mark the member or the type `[Localized(en: "...", nl: "...")]`. An override has the text of the property it overrides, so a marked property in a base class is enough. A type's own text isn't inherited.

The build checks a call that names its type and its member in the code: by a type argument or by `typeof`, and by a member name that's a constant, such as `nameof(Address.Street)`. It doesn't check a call that takes a `Type` or a `PropertyInfo` your code only has at run time. There, `null` is the answer for a member without a text, as a form gets it for a field without a label.

A type of another assembly is checked only when that assembly doesn't use Idioma, such as `DayOfWeek` of .NET itself. The attribute isn't kept in a compiled assembly, so the build can't see which members of a library have a text.

To ask for a text that isn't there yet on purpose, turn the error off around the call: `#pragma warning disable LOC504`.

### What still converts

A value that isn't wording of yours still converts to a text, because it has nothing to translate:

```csharp
Title = customer.Name;     // A value read at run time
Title = $"{first} {last}"; // Values alone, without wording
Title = "-";               // No letters
Title = "1234";
```

### What the build misses

The build misses three things. First, it can't tell such a value from wording kept in a local variable, a field or a property, or returned by a method, so those compile too:

```csharp
private static readonly string Heading = "Orders";

var title = "Orders";
Title = title;      // Wording, but the build sees a variable
Title = Heading;    // Wording, but the build sees a field, as it would a property
Title = GetTitle(); // Wording, but the build sees a method call
```

A `const` is the exception, because the build reads its value. It's LOC500.

Second, plain text in Razor markup, such as `<h1>Orders</h1>`, isn't a text at all, so the build doesn't see it either. Write `<h1>@LocalizableText.Inline(en: "Orders", nl: "Bestellingen")</h1>`.

Third, a text kept as a `string` compiles, and holds the wording of the moment it was made:

```csharp
private static readonly string Label = ShopLocalization.Checkout; // Stays in the language of the first user
```

Keep the `LocalizableText` instead, and convert it where you show it ([keep a text as a text](../README.md#keep-a-text-as-a-text)).

## Libraries

### LOC600

A library you reference has no set for one of your languages. The message names the set to add to your app:

```text
error LOC600: No set serves 'Shop.IShopLocalization' in 'de', which this assembly supports. Add 'class ShopLocalizationDe : ILocalizationSet, Shop.IShopLocalization'.
```

The IDE fix adds that class in a file of its own, without texts, as the fix of [LOC201](#loc201) does. The file goes next to the one that lists your languages, and the class gets that file's namespace. It names the contract after a `using` of its namespace, as in `using Shop;` and `IShopLocalization`, and writes its full name only where the short one would mean another type. "Fix all" adds a set for every library and language at once.

See [a language the library lacks](libraries-and-apps.md#a-language-the-library-lacks).

### LOC601

A library you reference has inline texts that lack one of your languages. This is a warning. Nothing outside the library can add a language to them, so your users see those texts in your app's default language. Where the library lacks that language too, they see the library's default language. The message names the language they see:

```text
warning LOC601: 'Shop' has inline texts without 'de', which this assembly supports. No set added here can translate them, so users of 'de' will see them in 'en'.
```

The texts a library writes as attributes, with `[Localized]`, count among them, because they carry their wording themselves too.

To accept it, add `LOC601` to `<NoWarn>` in the project file, or put `#pragma warning disable LOC601` around `[assembly: SupportedLanguages]`, where it's reported.