# Idioma

## Introduction

Translations for any .NET app, written in C#. The build makes sure every text exists in every language you support.

With resx files and `IStringLocalizer`, a text is a string key, and a mistake stays hidden until a user sees it: a mistyped key shows as the key itself, and a wording nobody translated shows in the default language. Here texts are C#, so both are build errors.

Declare your languages once, `[assembly: SupportedLanguages("en", "nl")]`, and write each text in one of two ways.

### Inline texts

An inline text is written in one line, where you use it:

```razor
<button>
  @LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")
</button>
```

Check the demo [`InlineTextsDemo.cs`](https://github.com/Code-Chops/Idioma/blob/main/demos/InlineTextsDemo.cs) to see it in action on your machine.

### Sets

A set holds the texts that belong together, in one language: a class with a property per text. From the set of your default language, the build writes the contract `IShopTranslations`, which the other sets implement, and the class `ShopTranslations`, which shows the texts.

```csharp
public partial class ShopTranslationsEN : ITranslationSet
{
    public virtual string Cart => "Cart";
    public virtual string Checkout => "Checkout";
}

public class ShopTranslationsNL : IShopTranslations
{
    public virtual string Cart => "Winkelwagen";
    public virtual string Checkout => "Afrekenen";
}
```

```razor
<button>
  @ShopTranslations.Checkout
</button>
```

Check the demo [`SetsDemo.cs`](https://github.com/Code-Chops/Idioma/blob/main/demos/SetsDemo.cs) to see it in action on your machine.

Add German to your languages, and the build asks for it in every inline text, and names the set to write:

```text
error LOC004: 'ShopTranslations' has no translation into 'de', which [assembly: SupportedLanguages] requires. Add 'class ShopTranslationsDE : IShopTranslations'. A nested, generic, or file-local class does not count as a set.
```

Besides that:

- No resource files and no string keys. In your IDE, hovering over `ShopTranslations.Checkout` shows its wording in every language, and you can jump to each.
- Showing a text allocates no memory ([what it costs](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#what-showing-a-text-costs)), and the package is safe to trim.
- In Blazor WebAssembly, users switch language without reloading the app, where resx files reload it or download every language at startup ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#switching-language-without-a-reload)).

The rest of this page shows how to write your texts and show them. To see it working first, [run a demo](#run-a-demo). For the details, such as which wording a user sees and what each build error means, see [how Idioma works](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md).

## Getting started

### Install

```shell
dotnet add package CodeChops.Idioma
```

The source generator comes with the package, so there is nothing else to set up. It needs .NET 10 or later and C# 14, the language version .NET 10 uses unless your project sets an older `LangVersion`.

### Declare your languages

Add this to any C# file of your project, after its `using` lines:

```csharp
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl")] // the first is the default
```

From then on, the build makes sure that every text of the project has each of these languages.

In a Blazor or Razor project, add the namespace to `_Imports.razor` as well, together with the namespace of your sets:

```razor
@using CodeChops.Idioma
@using MyApp.Translations
```

### Write your texts

Write a text in one of two ways, and mix them as you like:

- **Inline text**, written in one line where you use it, for single use in a component or page.
- **A set per language**, each a class, for many reusable texts that belong together.

#### Write an inline text

Write the text where you use it: in markup, as in the [introduction](#inline-texts), or in C#:

```csharp
public LocalizableText Title { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
```

`Inline` takes one argument per language you declared, so leaving a language out does not compile.

#### Write a set

A set is a class that holds your texts in one language. Write the set of your default language as a partial class named after that language, with one property per text:

```csharp
public partial class ShopTranslationsEN : ITranslationSet
{
    public virtual string Cart => "Cart";
    public virtual string Checkout => "Checkout";
    public virtual string Greeting => "Hello, {0}!";
}
```

The source generator then writes the contract `IShopTranslations`, which lists every text, and the class `ShopTranslations` to show them with. Both take their name from the set's name without its language code, and land in the set's namespace. Write a set for every other language, a class that implements the contract:

```csharp
public class ShopTranslationsNL : IShopTranslations
{
    public virtual string Cart => "Winkelwagen";
    public virtual string Checkout => "Afrekenen";
    public virtual string Greeting => "Hallo, {0}!";
}
```

Each text is `virtual`, so that a set deriving from this one can override it: a [regional variant](#a-regional-variant-such-as-british-english), or an app's rewording of a library's texts ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#libraries-and-apps)).

Show a text by its name, wherever you need it:

```razor
<button>
  @ShopTranslations.Cart
</button>
```

A missing text or a missing language is a build error. There is nothing to register: every set registers itself.

### A regional variant, such as British English

`ShopTranslationsEN` serves every English culture, `en-US` and `en-GB` alike. For the texts that differ in Britain, list `en-GB`, and write a set with just those texts, named after the code in PascalCase and deriving from your English set:

```csharp
[assembly: SupportedLanguages("en", "nl", "en-GB")]

public class ShopTranslationsEnGB : ShopTranslationsEN
{
    public override string Cart => "Basket";
}
```

British users see "Basket" and the rest of your English.

Once you list `en-GB`, the build asks for British wording everywhere, so that none is forgotten:

- Every contract gets a British set. One with nothing British about it is a single line: `public class GridTranslationsEnGB : GridTranslationsEN;`.
- Every inline text takes a British wording: `LocalizableText.Inline(en: "Color", nl: "Kleur", enGB: "Colour")`.

To add British wording in one place only, see [Language codes](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#language-codes).

### Show a text

A text shows the wording for the user's language wherever you put it: in markup as above, in string interpolation, or as a `string`, as in `string label = ShopTranslations.Checkout;`. A user whose language you do not support sees your default language ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#which-wording-a-user-sees)).

For the wording in a language other than the user's, such as the language of an email's recipient, pass its culture to `ToString`:

```csharp
string dutch = ShopTranslations.Checkout.ToString(CultureInfo.GetCultureInfo("nl"));
```

To compare a text with a string, use `==`, as in `text == "Afrekenen"`: it compares the wording for the current UI culture, where `Equals` and `Contains` do not ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#comparing-texts)).

### Format a text

```razor
<p>
  @ShopTranslations.Greeting.Format(customer.Name)
</p>
```

`Format` fills in a text's placeholders. Give every language the same placeholders: the build checks it.

For a language other than the user's, pass its culture to `Format`, which then also formats the values for it:

```csharp
string greeting = ShopTranslations.Greeting.Format(CultureInfo.GetCultureInfo("nl-NL"), customer.Name);
```

### Blazor

Every language is compiled into your app, so in Blazor WebAssembly a user switches language without a download or a reload. [Blazor](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#blazor) in the details shows a language picker that does it, and covers Blazor on a server, dates and numbers, and texts passed between render modes.

## Translating your texts

Your texts are C# in your repository, where an AI coding assistant works, and translating is something it does well. The build tells it what to write: add a language to the list, and the build names each set that is missing; add a text, and it names that text for each language that lacks it. So a text does not wait for its translation: it gets a wording in every language in the change that adds it.

That leaves a translator to review the wording, in a file with a line per text:

```csharp
public virtual string Checkout => "Afrekenen";
```

The build checks every edit, an assistant's or a person's: a text left out or a placeholder dropped does not compile, so neither reaches a user.

## Run a demo

The repository has two demos in its [`demos`](https://github.com/Code-Chops/Idioma/tree/main/demos) folder. Each is a complete app in one file, which `dotnet run` runs without a project:

- [`SetsDemo.cs`](https://github.com/Code-Chops/Idioma/blob/main/demos/SetsDemo.cs) writes its texts in a set per language.
- [`InlineTextsDemo.cs`](https://github.com/Code-Chops/Idioma/blob/main/demos/InlineTextsDemo.cs) writes its text inline.

Both support English, Dutch, and British English (`en-GB`), a [regional variant](#a-regional-variant-such-as-british-english) of English, and print their texts in each. Run one from the `demos` folder of a clone:

```shell
dotnet run SetsDemo.cs
```

A demo runs with .NET 10 or later. Its first line fetches the latest version of the package.

Remove a Dutch wording from either file, and it no longer builds.

## License

MIT. Code the source generator writes into your project is yours, and carries no obligations under this license.