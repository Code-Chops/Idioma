# Idioma

Translations for any .NET app, written in C# and checked at compile time:

- The build catches the mistakes that resx files hide until a user sees them: a mistyped key, a text missing from a language, or a translation that takes other values than the original.
- There are no resource files, so you get full IDE support: autocomplete, go to definition, and find all references.
- An AI coding assistant can translate your texts, and the build tells it which are missing ([details](#translating-your-texts)).
- The package has no dependencies, is light and fast, and is safe to trim and to publish with Native AOT.

Declare your languages once, `[assembly: SupportedLanguages("en", "nl")]`, and write each text in one of two ways:
in a set per language, for texts you reuse, or inline, where you use it once.

```csharp
public partial class ShopTranslationsEN : ITranslationSet
{
    public string Cart => "Cart";
    public string Checkout => "Checkout";
}

public class ShopTranslationsNL : IShopTranslations
{
    public string Cart => "Winkelwagen";
    public string Checkout => "Afrekenen";
}
```

From the English set, the source generator writes the contract `IShopTranslations`, which the Dutch set implements, and the class `ShopTranslations`, which shows each text in the user's language:

```razor
@* A text of a set *@
<button>
  @ShopTranslations.Checkout
</button>

@* An inline text *@
<button>
  @LocalizableText.Inline(en: "Cancel", nl: "Annuleren")
</button>
```

## Getting started

> For complete apps to run, see the [demos](https://github.com/Code-Chops/Idioma/blob/main/demos/README.md), or run them in your browser:
>
> [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Code-Chops/Idioma?quickstart=1)

### Install

```shell
dotnet add package CodeChops.Idioma
```

The package needs .NET 10 or later and C# 14, the language version .NET 10 uses unless your project sets an older `LangVersion`.

### Declare your languages

Add this to any C# file of your project, after its `using` lines:

```csharp
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl")] // The first is the default
```

From then on, the build makes sure that every text of the project has each of these languages.

When your texts are in a library, declare your languages in the app's own project as well, the project that starts: your users get the languages of the app ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/libraries-and-apps.md#the-apps-languages)).

In a Blazor or Razor project, add the namespace to `_Imports.razor` as well, together with the namespace of your sets:

```razor
@using CodeChops.Idioma
@using MyApp.Translations
```

### Write your texts

Write a text in one of two ways, and mix them as you like.

#### Write a set

A set (like a resx file) holds the texts that belong together, in one language: a class with a property per text, or a method for a text that takes a value.
It suits texts you reuse. Write the set of your default language as a partial class whose name ends in the code of that language in capitals, as in `ShopTranslationsEN`:

```csharp
public partial class ShopTranslationsEN : ITranslationSet
{
    public string Cart => "Cart";
    public string Checkout => "Checkout";
    public string Greeting(string name) => $"Hello, {name}!";
}
```

That makes three names to tell apart:

- `ShopTranslationsEN` is the set you wrote, in your default language.
- `IShopTranslations` is the contract, which lists every text. The source generator writes it from your set.
- `ShopTranslations` is the class you show the texts with. The source generator writes it as well.

Both generated names are the set's name without its language code, and both land in the set's namespace.

Write a set for every other language, a class that implements the contract:

```csharp
public class ShopTranslationsNL : IShopTranslations
{
    public string Cart => "Winkelwagen";
    public string Checkout => "Afrekenen";
    public string Greeting(string name) => $"Hallo, {name}!";
}
```

A missing text or a missing language is a build error. There is nothing to register: every set registers itself.

Add German to your languages, and the build names the set to write:

```text
error LOC004: 'ShopTranslations' has no translation into 'de', which [assembly: SupportedLanguages] requires.
Add 'class ShopTranslationsDE : IShopTranslations'. A nested, generic, or file-local class does not count as a set.
```

#### Write an inline text

An inline text is written in one line where you use it, for single use in markup or in C#:

```csharp
public LocalizableText Title { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
```

`Inline` takes one argument per language you declared, so leaving out a translation does not compile.

A value goes into every language as you write the text: `LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")`.

### Show a text

Show a text by its name, wherever you need it:

```razor
<button>
  @ShopTranslations.Cart
</button>
```

In your IDE, hovering over `ShopTranslations.Cart` shows its wording in every language, and you can jump to each.

A text shows the wording for the user's language in markup, in string interpolation, or as a `string`, as in `string label = ShopTranslations.Checkout;`. A user whose language you do not support sees your default language ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#which-wording-a-user-sees)).

Every text is a `LocalizableText`, so that is the type for a text your own code holds or takes, as the `Title` above does. A text of a set is a `ResourceText`: a `LocalizableText` that also carries a key ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#what-the-source-generator-writes)).

The user's language is `CultureInfo.CurrentUICulture`.
A desktop or console app starts with the language of the user's system, and a Blazor WebAssembly app with that of the browser.

> An ASP.NET Core app needs request localization, as in `app.UseRequestLocalization("en", "nl")`,
> which sets the language for each request. Without it, every user gets the language of the server.

For the wording in a language other than the user's, such as the language of an email's recipient, pass its culture to `ToString`:

```csharp
string dutch = ShopTranslations.Checkout.ToString(CultureInfo.GetCultureInfo("nl"));
```

### Pass values to a text

A text that takes a value, such as `Greeting` above, is a method. Call it with the value:

```razor
<p>
  @ShopTranslations.Greeting(customer.Name)
</p>
```

Every language implements the method, so each puts the value where it reads best, and a set that leaves the method out, or gives it other parameters, does not compile. A value can carry a format, as in `$"Total: {amount:N2}"`.

For a language other than the user's, pass its culture to `ToString`, which then also formats the values for it:

```csharp
string greeting = ShopTranslations.Greeting(customer.Name).ToString(CultureInfo.GetCultureInfo("nl-NL"));
```

A wording can also depend on a value, as for a plural ([Choose a wording by a value](#choose-a-wording-by-a-value-as-for-a-plural)).

## Going further

### Choose a wording by a value, as for a plural

The wording of a text that takes values can depend on them, as for a plural or a form of address:

```csharp
public partial class ShopTranslationsEN : ITranslationSet
{
    public string ItemsInCart(int count, string shop) => count is 1
        ? $"1 item in your cart at {shop}"
        : $"{count} items in your cart at {shop}";

    public string Welcome(Salutation salutation) => salutation switch
    {
        Salutation.Madam => "Welcome back, madam",
        Salutation.Sir => "Welcome back, sir",
        _ => "Welcome back",
    };
}

public class ShopTranslationsNL : IShopTranslations
{
    public string ItemsInCart(int count, string shop) => count is 1
        ? $"1 artikel in je winkelwagen bij {shop}"
        : $"{count} artikelen in je winkelwagen bij {shop}";

    public string Welcome(Salutation salutation) => salutation switch
    {
        Salutation.Madam => "Welkom terug, mevrouw",
        Salutation.Sir => "Welkom terug, meneer",
        _ => "Welkom terug",
    };
}
```

`Salutation` is an enum of your own. Show such a text by calling it with its values:

```razor
<p>
  @ShopTranslations.ItemsInCart(cart.Count, shop.Name)
</p>
```

The build holds every language to the parameters of the method, also to their names, so that two values of one type cannot swap unnoticed:

```text
error LOC020: 'ShopTranslationsNL.ItemsInCart' names this parameter 'aantal', and 'IShopTranslations.ItemsInCart' names it 'count'. Give the parameters of a text the same names in every language: otherwise two values of one type can swap unnoticed for the users of one language.
```

Each language chooses its wording for a number itself, in C#: English and Dutch have two forms, and a Polish set writes the conditions for its own. Idioma has no plural rules ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#texts-that-take-values)).

### Add a regional variant, such as British English

`ShopTranslationsEN` serves every English culture, `en-US` and `en-GB` alike.
For the texts that differ in Britain, list `en-GB`, make them `virtual` in your English set, and write a set with just those texts, named after the code in PascalCase and deriving from the English one:

```csharp
[assembly: SupportedLanguages("en", "nl", "en-GB")]

public partial class ShopTranslationsEN : ITranslationSet
{
    public virtual string Cart => "Cart"; // Virtual, so the British set can override it
    // Your other texts, as they were
}

public class ShopTranslationsEnGB : ShopTranslationsEN
{
    public override string Cart => "Basket";
}
```

British users see "Basket" and the rest of your English.

Once you list `en-GB`, the build asks for British wording everywhere, so that none is forgotten:

- Every contract gets a British set. One with nothing British about it is a single line: `public class GridTranslationsEnGB : GridTranslationsEN;`.
- Every inline text takes a British wording: `LocalizableText.Inline(en: "Color", nl: "Kleur", enGB: "Colour")`.

To add British wording in one place only, see [Language codes](https://github.com/Code-Chops/Idioma/blob/main/docs/language-codes.md#regional-variants).

### Compare a text with a string

To compare a text with a string, use `==`, which compares the wording for the current UI culture:

```csharp
bool isCheckout = text == "Afrekenen";
```

`Equals` and `Contains` compare texts instead, so they are false for a string even while the text shows that wording. The build rejects a string passed to them ([`LOC017`](https://github.com/Code-Chops/Idioma/blob/main/docs/build-errors.md)).

### Change a text of a library

A library can use Idioma for its own texts, as your app does. To change one of them, write a set in your app that derives from the library's set of that language, name it after that language too, and override the text:

```csharp
// DataTableTranslationsEN is the English set of a library your app uses
public sealed class MyDataTableTranslationsEN : DataTableTranslationsEN
{
    public override string Empty => "No orders yet"; // Empty is virtual in DataTableTranslationsEN
}
```

Your wording wins over the library's, and every other text keeps the library's wording.

For a text that is not virtual, or a library that lacks one of your languages, see [Libraries that use Idioma](https://github.com/Code-Chops/Idioma/blob/main/docs/libraries-and-apps.md#changing-a-text-of-a-library).

## Translating your texts

An AI coding assistant can translate your texts, as they are C# in your repository, and the build tells it what to write:

- Add a language to the list, and the build names each set that is missing.
- Add a text, and the build names it for each language that lacks it.

So a text does not wait for its translation: it gets a wording in every language in the change that adds it. That leaves a translator to review the wording, in a file with a line per text.

The build checks every edit, an assistant's or a person's: a text left out, or one given other values, does not compile, so neither reaches a user.

## Where Idioma does not fit

- When you need to add translations without a new build. Every language is compiled into your assembly, so a new language or a changed wording takes a new build. Where a wording must change without one, your app can let a resource file override it ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/coming-from-resx.md#keeping-a-resource-file-as-an-override)).
- When your translators work in a translation tool, which reads files such as XLIFF.
- When you use an older version of .NET than 10 or C# 14.

## Details

Each subject has a page of its own:

- [How Idioma works](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md): which wording a user sees, what the source generator writes, texts that take values, how sets register, how to read a set yourself, and how a text travels through JSON.
- [Language codes](https://github.com/Code-Chops/Idioma/blob/main/docs/language-codes.md): how a set's name gives its language, and the two ways to add a regional variant.
- [Libraries that use Idioma](https://github.com/Code-Chops/Idioma/blob/main/docs/libraries-and-apps.md): which project's languages your users get, what the build asks when such a library lacks one of them, and how your app changes one of its texts.
- [Blazor](https://github.com/Code-Chops/Idioma/blob/main/docs/blazor.md): switching language without a reload, Blazor on a server, dates and numbers, and a text as a component parameter.
- [What the build rejects](https://github.com/Code-Chops/Idioma/blob/main/docs/build-errors.md): every build error and warning by its code, what still converts to a text, and what the build misses.
- [Coming from resx](https://github.com/Code-Chops/Idioma/blob/main/docs/coming-from-resx.md): what becomes what, moving a resource file, and keeping a resource file as an override.
- [The demos](https://github.com/Code-Chops/Idioma/blob/main/demos/README.md): two complete apps, each in one file, which `dotnet run` runs without a project, and which fetch the latest package version.

## License

MIT. Code the source generator writes into your project is yours, and carries no obligations under this license.