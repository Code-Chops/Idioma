# Idioma

[![NuGet](https://img.shields.io/nuget/v/CodeChops.Idioma)](https://www.nuget.org/packages/CodeChops.Idioma)

## Why use Idioma?

Lightweight and fast translations for any .NET app, written in C# and checked at compile time:

- The build catches the mistakes that resx files hide until a user sees them:
  - A mistyped key;
  - A text missing from a language;
  - A translation that never uses a value the original shows;
  - An enum member that has no translation.
- There are no resource files, so you get full IDE support:
  - Autocomplete;
  - Go to definition;
  - Every language's wording on hover;
  - Find all references.
- Idioma shows most texts in less time than IStringLocalizer, and plain texts allocate nothing ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/performance.md)).
- An AI coding assistant translates your texts well:
  - It sees the code that uses them so it knows the context.
  - The build names each missing translation, so the assistant knows when it's done ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/translating.md)).
- The package has no dependencies, and is safe to trim and to publish with Native AOT.

### At a glance

Declare your languages once, `[assembly: SupportedLanguages(defaultLanguage: "en", "nl")]`, and write each text in one of three ways:

- **In a set** per language, for texts you reuse, or enum members.
- **Inline**, where you use a text once.
- **As an attribute**, for the label of a type, a property or an enum member.

```csharp
// A set per language
public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
{
    public string Checkout => "Checkout";
}

public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
{
    public string Checkout => "Afrekenen";
}

// An attribute on a type or its member
[Localized(en: "Address", nl: "Adres")]
public class Address
{
    [Localized(en: "City", nl: "Plaats")]
    public string City { get; set; } = "";
}
```

From the English set, the source generator writes the contract `IShopLocalization`, which every set implements, and the class `ShopLocalization`. Each text then shows in the user's language:

```razor
@* A text of a set *@
<button>
  @ShopLocalization.Checkout
</button>

@* A text of a type, written as an attribute *@
<label>
  @(LocalizableText.GetTextOfType<Address>())
</label>
@* A text of its member, written as an attribute *@
<label>
  @(LocalizableText.GetTextByMember<Address>(nameof(Address.City)))
</label>

@* An inline text, written where it shows *@
<button>
  @LocalizableText.Inline(en: "Cancel", nl: "Annuleren")
</button>
```

> Also see the library [Formosa](https://github.com/Code-Chops/Formosa). It generates Blazor forms from your C# models, and writes its texts with Idioma too.

## Getting started

> Each [demo app](https://github.com/Code-Chops/Idioma/blob/main/demos/README.md) is one file that `dotnet run` runs without a project. You can also run them in your browser:
>
> [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Code-Chops/Idioma?quickstart=1)

### Install

```shell
dotnet add package CodeChops.Idioma
```

The package needs .NET 10 or later and C# 14. C# 14 is the default on .NET 10. An older `LangVersion` in your project is build error `LOC102`.

### Declare your languages

Add this to any C# file of your project, after its `using` lines:

```csharp
using CodeChops.Idioma;

[assembly: SupportedLanguages(defaultLanguage: "en", "nl")]
```

From then on, the build checks that every text in the project has all of these languages.

If your texts are in a library, declare your languages in the app's project too. Your users get the app's languages ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/libraries-and-apps.md#the-apps-languages)).

### Write your texts

Write a text in one of three ways, and mix them as you like.

#### Write a set

A set (like a resx file) is a class that holds related texts in one language. Each text is a property, or a method if it takes a value. Sets suit texts you reuse.

Write the set of your default language as a class whose name ends in the language code in PascalCase, such as `ShopLocalizationEn`. It lists `ILocalizationSet`, and the contract the source generator writes from it:

```csharp
public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
{
    public string Cart => "Cart";
    public string Checkout => "Checkout";
    public string Greeting(string name) => $"Hello, {name}!";
}
```

That makes three names to tell apart:

- `ShopLocalizationEn` is the set you wrote, in your default language.
- `IShopLocalization` is the contract, which lists every text. The source generator writes it from your set.
- `ShopLocalization` is the class you show the texts with. The source generator writes it as well.

Both generated names are the set's name without its language code, and both land in the set's namespace.

For every other language, write a set that lists the same two interfaces:

```csharp
public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
{
    public string Cart => "Winkelwagen";
    public string Checkout => "Afrekenen";
    public string Greeting(string name) => $"Hallo, {name}!";
}
```

The build checks your sets:

- A missing text or a missing language is a build error.
- A set without its contract is a build error too, and your IDE adds the contract.
- A class without `ILocalizationSet` isn't a set, so the build reports its language as missing.

There's nothing to register, because every set is registered automatically.

If you add German to your languages, the build tells you which set to write:

```text
error LOC201: 'ShopLocalization' has no set for 'de', which [assembly: SupportedLanguages] requires.
Add 'class ShopLocalizationDe : ILocalizationSet, IShopLocalization'. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.
```

An enum gets a set of its own, which lists `ILocalizationSet<TEnum>` ([Enums](#enums)).

#### Write an inline text

An inline text is written where you use it, in markup or in C#. It suits a text you use once:

```csharp
public string Title => LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
```

`Inline` takes one argument per language you declared, so leaving out a translation doesn't compile. It returns the wording for the user's language, so write the property with `=>`. A property with an initializer keeps the wording of the first user.

To include a value, write it into every language: `LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")`. A Dutch wording that lost its `{name}` is a build error (`LOC402`).

#### Write an attribute

An attribute puts a text on the member it names: a property, a field, an enum member, or a type. It suits a label, such as the name of a property of a model, which a form, a table or an export shows:

```csharp
[Localized(en: "Address", nl: "Adres")]
public class Address
{
    [Localized(en: "City", nl: "Plaats")]
    public string City { get; set; } = "";

    [Localized(en: "Postal code", nl: "Postcode")]
    public string PostalCode { get; set; } = "";
}
```

`[Localized]` takes one argument per language you declared, as `Inline` does, so leaving out a translation doesn't compile.

Its wording is a constant, so it can't include a value. Write a text with a value in a set or inline.

### Show a text

Show a text of a set by its name, wherever you need it:

```razor
<button>
  @ShopLocalization.Cart
</button>
```

In your IDE, hovering over `ShopLocalization.Cart` shows its wording in every language, and you can jump to each.

An inline text shows where you write it.

To show a text written as an attribute, ask for it by its type and the name of its member. A type's own text needs only the type ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/attributes.md#finding-a-text-by-its-member)):

```razor
<h2>
  @(LocalizableText.GetTextOfType<Address>())
</h2>

<label>
  @(LocalizableText.GetTextByMember<Address>(nameof(Address.City)))
</label>
```

All three give a `string`: the wording for the user's language at the moment you read the text. So they work in markup, in string interpolation, and wherever a `string` is expected, as in `string label = ShopLocalization.Checkout;`.

#### Read a text where you show it

A text doesn't follow a later change of language, because it's a `string`. Don't keep it in a static field, a cache or a view model that outlives one request. On a server, a `string` in a static field shows every user the language of the first one:

```csharp
static readonly string Frozen = ShopLocalization.Checkout; // The first user's language, for every user
static string Label => ShopLocalization.Checkout;          // Each user's own language
```

The build warns about a text in the initializer of a static field or property (`LOC602`).

### The user's language

The user's language is `CultureInfo.CurrentUICulture`.
A desktop or console app starts with the language of the user's system, and a Blazor WebAssembly app with that of the browser.

> An ASP.NET Core app needs request localization, as in `app.UseRequestLocalization([.. LocalizationRegistry.SupportedLanguages])`,
> which sets the language for each request. Without it, every user gets the language of the server. The [ASP.NET Core page](https://github.com/Code-Chops/Idioma/blob/main/docs/aspnet-core.md) has the rest.

Users whose language you don't support see your default language ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#fallback-order)).

An app that keeps the user's language somewhere else, such as in a user profile, sets `LocalizationRegistry.CurrentUICultureProvider` once at startup. Every text then follows the culture it returns ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#the-current-culture)):

```csharp
LocalizationRegistry.CurrentUICultureProvider = () => UserProfile.Current?.Culture;
```

#### Another language

To get the wording in another language, such as that of an email's recipient, wrap the block that reads the texts in `LocalizationRegistry.UseCulture`:

```csharp
using (LocalizationRegistry.UseCulture(CultureInfo.GetCultureInfo("nl")))
{
    // "Hallo, Anna!", whatever the user's language
    string greeting = ShopLocalization.Greeting("Anna");
}
```

The culture formats dates and numbers too, so the whole block is Dutch. The scope follows your code across an `await` ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#another-language)).

### Texts with parameters

A text that takes a value, such as `Greeting` above, is a method. Call it with the value:

```razor
<p>
  @ShopLocalization.Greeting(customer.Name)
</p>
```

Every language implements the method, so each can put the value where it reads best. A set that leaves the method out, or gives it other parameters, doesn't compile. A value can carry a format, as in `$"Total: {amount:N2}"`.

Use the value in every language. A Dutch greeting that lost its `{name}` is a build error:

```text
error LOC402: 'ShopLocalizationNl.Greeting' doesn't use its parameter 'name', but the default wording of 'IShopLocalization.Greeting' shows it. Use 'name' in every language, or users of 'nl' won't see it.
```

A placeholder such as `{0}` gets build warning `LOC403`, because nothing fills it in. Your IDE turns such a text into a method that takes the value.

The values are formatted for `CultureInfo.CurrentCulture`, as in any interpolated string.

### Plurals

A text with parameters can choose its wording by their values, as for a plural:

```csharp
public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
{
    public string ItemsInCart(int count, string shop) => count is 1
        ? $"1 item in your cart at {shop}"
        : $"{count} items in your cart at {shop}";
}

public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
{
    public string ItemsInCart(int count, string shop) => count is 1
        ? $"1 artikel in je winkelwagen bij {shop}"
        : $"{count} artikelen in je winkelwagen bij {shop}";
}
```

Idioma has no plural rules. Each language writes its own conditions in C#: English and Dutch have two forms, and Polish has more ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#texts-with-parameters)).

### Enums

A set of an enum gives every member a text. It lists `ILocalizationSet<TEnum>` in place of `ILocalizationSet`:

```csharp
public enum OrderStatus { Pending, Shipped }

public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public string Pending => "Pending";
    public string Shipped => "Shipped";
}
```

A member you add to the enum later is a build error in every language until it has a text. A set also works for an enum you can't change, such as `DayOfWeek`. Show a value's text with `order.Status.GetText()`. To keep the wording next to each member instead, mark each one `[Localized]` ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/enums.md#an-attribute-on-each-member)).

## Where Idioma doesn't fit

- When you need to add translations without a new build. Every language is compiled into your assembly, so a new language or a changed wording takes a new build. Where a wording must change without one, your app can ask an `IStringLocalizer` first, such as one that reads a database ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/coming-from-resx.md#keeping-a-resource-file-as-an-override)).
- When your translators work in a translation tool, which reads files such as XLIFF.
- When an attribute of .NET such as `[Required]` or `[Display]` has to read a label you write with `[Localized]`. Those attributes read only a text of a set, by its name ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/coming-from-resx.md#validation-and-display-attributes)).
- When your app targets .NET 9 or earlier, or can't move to C# 14.

## Documentation

The docs cover the rest, a page per subject:

- [Enums](https://github.com/Code-Chops/Idioma/blob/main/docs/enums.md): a [set of an enum](https://github.com/Code-Chops/Idioma/blob/main/docs/enums.md#a-set-of-the-enum), or [an attribute on each member](https://github.com/Code-Chops/Idioma/blob/main/docs/enums.md#an-attribute-on-each-member).
- [Attributes](https://github.com/Code-Chops/Idioma/blob/main/docs/attributes.md): labels in a form, a table or an export, written with `[Localized]`, and [finding them by their member](https://github.com/Code-Chops/Idioma/blob/main/docs/attributes.md#finding-a-text-by-its-member).
- [Advanced topics](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md): [which wording a user sees](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#fallback-order), [getting a text by its key](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#getting-a-text-by-its-key), registering a set by hand, [testing](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#testing), and what the source generator writes.
- [Language codes](https://github.com/Code-Chops/Idioma/blob/main/docs/language-codes.md): how a set's name gives its language, and [regional variants](https://github.com/Code-Chops/Idioma/blob/main/docs/language-codes.md#regional-variants) such as British English.
- [Libraries that use Idioma](https://github.com/Code-Chops/Idioma/blob/main/docs/libraries-and-apps.md): writing a library, and [changing a text of a library](https://github.com/Code-Chops/Idioma/blob/main/docs/libraries-and-apps.md#changing-a-text-of-a-library) in your app.
- [Translating your texts](https://github.com/Code-Chops/Idioma/blob/main/docs/translating.md): with an AI coding assistant, and reviewing each language in a pull request.
- [ASP.NET Core](https://github.com/Code-Chops/Idioma/blob/main/docs/aspnet-core.md): the language of a request, labels and validation messages, and what to watch on a server.
- [Blazor](https://github.com/Code-Chops/Idioma/blob/main/docs/blazor.md): a language picker for every render mode, dates and numbers, and a text as a component parameter.
- [What the build rejects](https://github.com/Code-Chops/Idioma/blob/main/docs/build-errors.md): every build error and warning, by its code.
- [Coming from resx](https://github.com/Code-Chops/Idioma/blob/main/docs/coming-from-resx.md): what becomes what, and keeping a resource file as an override.
- [Performance](https://github.com/Code-Chops/Idioma/blob/main/docs/performance.md): how long a text takes next to `IStringLocalizer`, and what Idioma adds to a download.
- [The demos](https://github.com/Code-Chops/Idioma/blob/main/demos/README.md): four complete apps, each one file that `dotnet run` runs.

## License

MIT. Code the source generator writes into your project is yours, and carries no obligations under this license.