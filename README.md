# Idioma

Translations for any .NET app, written in C# and checked at compile time:

- The build catches the mistakes that resx files hide until a user sees them: a mistyped key, a text missing from a language, or a translation that never uses a value the original shows.
- There are no resource files, so you get full IDE support: autocomplete, go to definition, and find all references.
- The package has no dependencies, and is safe to trim and to publish with Native AOT.
- Idioma shows most texts in less time than IStringLocalizer, and plain texts allocate nothing. A text with parameters takes longer for a user whose language your app doesn't support, but that's an edge case.
- An AI coding assistant can translate your texts, and the build tells it which are missing ([details](#translating-your-texts)).

Declare your languages once, `[assembly: SupportedLanguages("en", "nl")]`, and write each text in one of two ways:
in a set per language, for texts you reuse, or inline, where you use it once.

```csharp
public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
{
    public string Cart => "Cart";
    public string Checkout => "Checkout";
}

public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
{
    public string Cart => "Winkelwagen";
    public string Checkout => "Afrekenen";
}
```

From the English set, the source generator writes the contract `IShopLocalization`, which every set implements, and the class `ShopLocalization`, which shows each text in the user's language:

```razor
@* A text of a set *@
<button>
  @ShopLocalization.Checkout
</button>

@* An inline text *@
<button>
  @LocalizableText.Inline(en: "Cancel", nl: "Annuleren")
</button>
```

[Formosa](https://github.com/Code-Chops/Formosa), which generates Blazor forms from your C# models, writes its texts with Idioma too.

## Getting started

> For complete apps to run, see the [demos](https://github.com/Code-Chops/Idioma/blob/main/demos/README.md), or run them in your browser:
>
> [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Code-Chops/Idioma?quickstart=1)

### Install

```shell
dotnet add package CodeChops.Idioma
```

The package needs .NET 10 or later and C# 14. C# 14 is the default on .NET 10, unless your project sets an older `LangVersion`.

### Declare your languages

Add this to any C# file of your project, after its `using` lines:

```csharp
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl")] // The first is the default
```

From then on, the build checks that every text in the project has all of these languages.

If your texts are in a library, declare your languages in the app's project too. Your users get the app's languages ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/libraries-and-apps.md#the-apps-languages)).

### Write your texts

Write a text in one of two ways, and mix them as you like.

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

A missing text or a missing language is a build error. So is a set without its contract, and your IDE adds it. Without `ILocalizationSet`, a class isn't a set, so the build reports its language as missing. There's nothing to register, because every set is registered automatically.

If you add German to your languages, the build tells you which set to write:

```text
error LOC201: 'ShopLocalization' has no set for 'de', which [assembly: SupportedLanguages] requires.
Add 'class ShopLocalizationDe : ILocalizationSet, IShopLocalization'. A class without ILocalizationSet doesn't count as a set, and neither does a nested, generic, or file-local one.
```

#### Write an inline text

An inline text is written where you use it, in markup or in C#. It suits a text you use once:

```csharp
public LocalizableText Title { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
```

`Inline` takes one argument per language you declared, so leaving out a translation doesn't compile.

To include a value, write it into every language: `LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")`. A Dutch wording that lost its `{name}` is a build error (`LOC302`).

### Show a text

Show a text of a set by its name, wherever you need it:

```razor
<button>
  @ShopLocalization.Cart
</button>
```

In your IDE, hovering over `ShopLocalization.Cart` shows its wording in every language, and you can jump to each.

An inline text shows where you write it.

Both kinds show the wording for the user's language in markup, in string interpolation, or as a `string`, as in `string label = ShopLocalization.Checkout;`.

Every text is a `LocalizableText`. Use that type when your own code stores or accepts a text, as `Title` above does.

### The user's language

The user's language is `CultureInfo.CurrentUICulture`.
A desktop or console app starts with the language of the user's system, and a Blazor WebAssembly app with that of the browser.

> An ASP.NET Core app needs request localization, as in `app.UseRequestLocalization("en", "nl")`,
> which sets the language for each request. Without it, every user gets the language of the server.

Users whose language you don't support see your default language ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#fallback-order)).

To get the wording in another language, such as that of an email's recipient, pass its culture to `ToString`:

```csharp
string dutch = ShopLocalization.Checkout.ToString(CultureInfo.GetCultureInfo("nl"));
```

An app that keeps the user's language somewhere else, such as in a user profile, sets `LocalizationRegistry.CurrentUICultureProvider` once at startup. Every text then follows the culture it returns ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#the-current-culture)):

```csharp
LocalizationRegistry.CurrentUICultureProvider = () => UserProfile.Current?.Culture;
```

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
error LOC302: 'ShopLocalizationNl.Greeting' doesn't use its parameter 'name', but the default wording of 'IShopLocalization.Greeting' shows it. Use 'name' in every language, or users of 'nl' won't see it.
```

A placeholder such as `{0}` gets build warning `LOC502`, because nothing fills it in. Your IDE turns such a text into a method that takes the value.

`ToString` with a culture also formats the values for that culture:

```csharp
string greeting = ShopLocalization.Greeting(customer.Name).ToString(CultureInfo.GetCultureInfo("nl-NL"));
```

The wording can also depend on a value, as for a plural ([Plurals and other choices by value](#plurals-and-other-choices-by-value)).

## Going further

### Plurals and other choices by value

A text with parameters can choose its wording by their values, as for a plural or a form of address:

```csharp
public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
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

public class ShopLocalizationNl : ILocalizationSet, IShopLocalization
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
  @ShopLocalization.ItemsInCart(cart.Count, shop.Name)
</p>
```

The build checks that every language uses the same parameters, names included, so two values of one type can't swap unnoticed:

```text
error LOC301: 'ShopLocalizationNl.ItemsInCart' names this parameter 'aantal', but 'IShopLocalization.ItemsInCart' names it 'count'. Use the same parameter names in every language, so that two values of one type can't swap unnoticed.
```

Idioma has no plural rules. Each language writes its own conditions in C#: English and Dutch have two forms, and Polish has more ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md#texts-with-parameters)).

### Regional variants, such as British English

`ShopLocalizationEn` serves every English culture, `en-US` and `en-GB` alike. For the texts that differ in Britain:

1. List `en-GB` in your languages.
2. Make those texts `virtual` in your English set.
3. Write a set with only those texts, named after the code in PascalCase (`EnGb`) and deriving from the English set:

```csharp
[assembly: SupportedLanguages("en", "nl", "en-GB")]

public class ShopLocalizationEn : ILocalizationSet, IShopLocalization
{
    public virtual string Cart => "Cart"; // Virtual, so the British set can override it
    // Your other texts...
}

public class ShopLocalizationEnGb : ShopLocalizationEn
{
    public override string Cart => "Basket";
}
```

British users see "Basket" and the rest of your English.

Once you list `en-GB`, the build asks for British wording everywhere, so that none is forgotten:

- Every contract gets a British set. One with nothing British about it is a single line: `public class GridLocalizationEnGb : GridLocalizationEn;`.
- Every inline text takes a British wording: `LocalizableText.Inline(en: "Color", nl: "Kleur", enGb: "Colour")`.

To add British wording in one place only, see [Language codes](https://github.com/Code-Chops/Idioma/blob/main/docs/language-codes.md#regional-variants).

### Change a text of a library

A library can use Idioma for its own texts. To change one of them, write a set in your app that derives from the library's set for that language, give it a name that ends in the same language, and override the text:

```csharp
// DataTableLocalizationEn is the English set of a library your app uses
public sealed class MyDataTableLocalizationEn : DataTableLocalizationEn
{
    public override string Empty => "No orders yet"; // Empty is virtual in DataTableLocalizationEn
}
```

Your wording replaces the library's. Every other text keeps the library's wording.

For a text that isn't virtual, or a library that lacks one of your languages, see [Libraries that use Idioma](https://github.com/Code-Chops/Idioma/blob/main/docs/libraries-and-apps.md#changing-a-text-of-a-library).

## Translating your texts

Your texts are C# in your repository, so an AI coding assistant can translate them, and the build tells it what's missing:

- Add a language to the list, and the build names each set that is missing.
- Add a text, and the build names it for each language that lacks it.

So a new text doesn't wait for its translation. It gets wording in every language in the same change that adds it.

### Reviewing translations

Review each translation in the pull request that adds it. The build checks that every language has wording, not that anyone read it. So Dutch that an AI assistant wrote compiles just like Dutch that a translator checked.

Each language has a set of its own, so the diff of `ShopLocalizationNl.cs` shows exactly what changed in Dutch. On GitHub, a `CODEOWNERS` file asks the right reviewer for each language:

```text
*LocalizationNl.cs  @my-org/dutch-translators
```

Review a translation as code too, because a set is C#. A change to a set can run any code when its text is shown. So next to the translator, have a developer approve it.

## Where Idioma doesn't fit

- When you need to add translations without a new build. Every language is compiled into your assembly, so a new language or a changed wording takes a new build. Where a wording must change without one, your app can let a resource file override it ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/coming-from-resx.md#keeping-a-resource-file-as-an-override)).
- When your translators work in a translation tool, which reads files such as XLIFF.
- When you use an older version of .NET than 10 or C# 14.

## Documentation

Each subject has a page of its own:

- [Language codes](https://github.com/Code-Chops/Idioma/blob/main/docs/language-codes.md): how a set's name gives its language, and the two ways to add a regional variant.
- [Libraries that use Idioma](https://github.com/Code-Chops/Idioma/blob/main/docs/libraries-and-apps.md): which project's languages your users get, what the build asks when such a library lacks one of them, and how your app changes one of its texts.
- [Blazor](https://github.com/Code-Chops/Idioma/blob/main/docs/blazor.md): switching language without a reload, Blazor on a server, dates and numbers, and a text as a component parameter.
- [What the build rejects](https://github.com/Code-Chops/Idioma/blob/main/docs/build-errors.md): every build error and warning by its code, what still converts to a text, and what the build misses.
- [Coming from resx](https://github.com/Code-Chops/Idioma/blob/main/docs/coming-from-resx.md): what becomes what, moving a resource file, and keeping a resource file as an override.
- [Performance](https://github.com/Code-Chops/Idioma/blob/main/docs/performance.md): how long a text takes to show next to `IStringLocalizer`, on .NET and in Blazor WebAssembly, and what Idioma adds to a download.
- [The demos](https://github.com/Code-Chops/Idioma/blob/main/demos/README.md): two complete apps, each in one file, which `dotnet run` runs without a project, and which fetch the latest package version.
- [How Idioma works](https://github.com/Code-Chops/Idioma/blob/main/docs/how-it-works.md): the background, which you don't need to use Idioma. It covers which wording a user sees, what the source generator writes, texts with parameters, how sets register, how to read a set yourself, how a text travels through JSON, and when two texts are equal.

## License

MIT. Code the source generator writes into your project is yours, and carries no obligations under this license.