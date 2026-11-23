# Idioma

Translations for any .NET app, written in C# and checked at compile time:

- The build catches the mistakes that resx files hide until a user sees them: a mistyped key, a text missing from a language, or a translation that never uses a value the original shows.
- There are no resource files, so you get full IDE support: autocomplete, go to definition, every language's wording on hover, and find all references.
- The package has no dependencies, and is safe to trim and to publish with Native AOT.
- Idioma shows most texts in less time than IStringLocalizer, and plain texts allocate nothing ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/performance.md)).
- An AI coding assistant can translate your texts, and the build tells it which are missing ([details](#translating-your-texts)).

Declare your languages once, `[assembly: SupportedLanguages(defaultLanguage: "en", "nl")]`, and write each text in one of three ways:

- **In a set** per language, for texts you reuse.
- **Inline**, where you use a text once.
- **As an attribute**, for the label of a type, a property or an enum member.

```csharp
// A set per language
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

// An attribute on a type and on each of its members
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

@* A text written as an attribute *@
<label>
  @(LocalizableText.GetTextByMember<Address>(nameof(Address.City)))
</label>

@* An inline text, written where it shows *@
<button>
  @LocalizableText.Inline(en: "Cancel", nl: "Annuleren")
</button>
```

> Also see [Formosa](https://github.com/Code-Chops/Formosa). It generates Blazor forms from your C# models, and writes its texts with Idioma too.

## Getting started

> Each [demo app](https://github.com/Code-Chops/Idioma/blob/main/demos/README.md) is one file that `dotnet run` runs without a project. You can also run them in your browser:
>
> [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Code-Chops/Idioma?quickstart=1)

### Install

```shell
dotnet add package CodeChops.Idioma
```

The package needs .NET 10 or later and C# 14. "C# 14 is the default on .NET 10. An older `LangVersion` in your project is build error `LOC102`."

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

#### Write an inline text

An inline text is written where you use it, in markup or in C#. It suits a text you use once:

```csharp
public LocalizableText Title { get; } = LocalizableText.Inline(en: "Orders", nl: "Bestellingen");
```

`Inline` takes one argument per language you declared, so leaving out a translation doesn't compile.

To include a value, write it into every language: `LocalizableText.Inline(en: $"Hello, {name}!", nl: $"Hallo, {name}!")`. A Dutch wording that lost its `{name}` is a build error (`LOC302`).

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

Ask for a text written as an attribute by the type and the name of its member, or for the text of the type itself ([details](#labels-in-a-form-a-table-or-an-export)):

```razor
<h2>
  @(LocalizableText.GetTextOfType<Address>())
</h2>

<label>
  @(LocalizableText.GetTextByMember<Address>(nameof(Address.City)))
</label>
```

All three show the wording for the user's language in markup, in string interpolation, or as a `string`, as in `string label = ShopLocalization.Checkout;`.

Every text is a `LocalizableText`. Use that type when your own code stores or accepts a text, as `Title` above does.

A `string` made from a text keeps the wording of that moment. So keep the `LocalizableText` in a static field, a cache or a view model, and convert it where you show it. On a server, a `string` in a static field shows every user the language of the first one.

### The user's language

The user's language is `CultureInfo.CurrentUICulture`.
A desktop or console app starts with the language of the user's system, and a Blazor WebAssembly app with that of the browser.

> An ASP.NET Core app needs request localization, as in `app.UseRequestLocalization([.. LocalizationRegistry.SupportedLanguages])`,
> which sets the language for each request. Without it, every user gets the language of the server.

Users whose language you don't support see your default language ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#fallback-order)).

To get the wording in another language, such as that of an email's recipient, pass its culture to `ToString`:

```csharp
string dutch = ShopLocalization.Checkout.ToString(CultureInfo.GetCultureInfo("nl"));
```

An app that keeps the user's language somewhere else, such as in a user profile, sets `LocalizationRegistry.CurrentUICultureProvider` once at startup. Every text then follows the culture it returns ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#the-current-culture)):

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

`ToString` with a culture other than the user's also formats the values for that culture:

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

Idioma has no plural rules. Each language writes its own conditions in C#: English and Dutch have two forms, and Polish has more ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#texts-with-parameters)).

### Enums

Give an enum its texts in one of two ways:

- **A set of the enum** makes a member without a text a build error. It also works for an enum you can't change, such as `DayOfWeek` or one from a library.
- **An attribute on each member** keeps the wording next to the member, and a member without one is a build warning. It needs the enum's source, because the attribute goes on each member.

Either way, a value of the enum gives its text itself, with `GetText()`:

```razor
<span>
  @order.Status.GetText()
</span>
```

#### A set of the enum

Write a set that lists `ILocalizationSet<TEnum>` in place of `ILocalizationSet`. Each member of the enum is a text, named after the member:

```csharp
public enum OrderStatus { Pending, Shipped }

public class OrderStatusLocalizationEn : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public string Pending => "Pending";
    public string Shipped => "Shipped";
}

public class OrderStatusLocalizationNl : ILocalizationSet<OrderStatus>, IOrderStatusLocalization
{
    public string Pending => "In behandeling";
    public string Shipped => "Verzonden";
}
```

Every language needs a text for every member. A member without one is a build error, so a member you add to the enum later can't go untranslated:

```text
error CS0535: 'OrderStatusLocalizationNl' does not implement interface member 'IOrderStatusLocalization.Shipped'
```

The source generator writes `GetText()` next to the generated class, so it's in reach wherever the namespace of your set is. It calls `OrderStatusLocalization.GetTextByValue(order.Status)`, which you can write yourself too.

A text of a member is a text like any other, so `OrderStatusLocalization.Shipped` works too ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#enums)).

#### An attribute on each member

Mark each member of the enum `[Localized]`:

```csharp
[Localized(en: "Parcel status", nl: "Pakketstatus")]
public enum ParcelStatus
{
    [Localized(en: "Picked", nl: "Gepickt")]
    Picked,

    [Localized(en: "Delivered", nl: "Bezorgd")]
    Delivered,
}
```

A member without `[Localized]` shows its name. The build warns about it when other members of the enum are marked (`LOC503`), so a member you add later doesn't go unnoticed.

The attribute on the enum itself gives the enum a text of its own, which `LocalizableText.GetTextOfType<ParcelStatus>()` shows.

### Labels in a form, a table or an export

Code that shows any model, such as a form or an export, holds a property or a type, and not its name. Ask for the text written as an attribute by what the code holds:

```csharp
// By a property, such as the one a form field shows. A property without [Localized] shows its name here.
LocalizableText label = LocalizableText.GetTextByMember(property) ?? property.Name;

// By a type and the name of its member
LocalizableText? city = LocalizableText.GetTextByMember(type, "City");

// The text of a type itself
LocalizableText? caption = LocalizableText.GetTextOfType(type);
```

Each gives `null` where there's no `[Localized]`. Where the call names the type and the member in the code, as `GetTextByMember<Address>(nameof(Address.City))` does, a member without `[Localized]` is build error `LOC504` instead.

A class without a text for a member gets that of its nearest base class. So an override without `[Localized]` keeps the text of the property it overrides.

The source generator reads each `[Localized]` as your project builds, and writes its text as code. So nothing reads the attribute at run time, and the attribute isn't part of your assembly ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#attributes-on-members)).

### Get a text by its key

When your code only has the name of a text at run time, such as a key from a database, ask the generated class for it with `GetTextByKey`:

```csharp
LocalizableText cart = ShopLocalization.GetTextByKey("Cart");
string dutch = cart.ToString(CultureInfo.GetCultureInfo("nl"));
```

The key is the name of the text. `GetTextByKey` returns the text, the same one as `ShopLocalization.Cart`.

A text with parameters takes its values after the key:

```csharp
LocalizableText greeting = ShopLocalization.GetTextByKey("Greeting", customer.Name);
```

The build can't check a key, so a key that no text has throws `KeyNotFoundException`. Where you know the text, write `ShopLocalization.Cart` instead ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#getting-a-text-by-its-key)).

### Equality

Two texts are equal when they show the same wording in the user's language, and a text's hash follows that wording. So a `Dictionary` or `HashSet` keyed by texts can't find its texts after a language switch, or when users of different languages share it. Give such a collection `ReferenceEqualityComparer.Instance`, which tells texts apart by object ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md#equality)).

### Regional variants, such as British English

`ShopLocalizationEn` serves every English culture, `en-US` and `en-GB` alike. For the texts that differ in Britain:

1. List `en-GB` in your languages.
2. Make those texts `virtual` in your English set.
3. Write a set with only those texts, named after the code in PascalCase (`EnGb`) and deriving from the English set:

```csharp
[assembly: SupportedLanguages(defaultLanguage: "en", "nl", "en-GB")]

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
- Every attribute takes one too: `[Localized(en: "Color", nl: "Kleur", enGb: "Colour")]`.

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

Your texts are C# in your repository, so an AI coding assistant (that already knows the context) can translate them, and the build tells it what's missing:

- Add a language to the list, and the build names each set that is missing.
- Add a text, and the build names it for each language that lacks it.

So a new text doesn't wait for its translation. It gets wording in every language in the same change that adds it.

### Reviewing translations

Review each translation in the pull request that adds it. The build checks that every language has wording, not that anyone read it. So Dutch that an AI assistant wrote compiles just like Dutch that a translator checked.

Each language has a set of its own, so the diff of `ShopLocalizationNl.cs` shows exactly what changed in Dutch. On GitHub, a `CODEOWNERS` file asks the right reviewer for each language:

```text
*LocalizationNl.cs  @my-org/dutch-translators
```

An inline text and an attribute hold every language in one place. So their Dutch has no file of its own, and is reviewed in the pull request that changes that line.

Review a translation as code too, because a set is C#. A change to a set can run any code when its text is shown. So next to the translator, have a developer approve it.

## Where Idioma doesn't fit

- When you need to add translations without a new build. Every language is compiled into your assembly, so a new language or a changed wording takes a new build. Where a wording must change without one, your app can let a resource file override it ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/coming-from-resx.md#keeping-a-resource-file-as-an-override)).
- When your translators work in a translation tool, which reads files such as XLIFF.
- When an attribute of .NET such as `[Required]` or `[Display]` has to take a text directly. An attribute takes only constants, so it reads your texts through a small class of `string` properties ([details](https://github.com/Code-Chops/Idioma/blob/main/docs/coming-from-resx.md#validation-and-display-attributes)). Those attributes don't read a label you write with `[Localized]`.
- When you use an older version of .NET than 10 or C# 14.

## Documentation

Each subject has a page of its own:

- [Language codes](https://github.com/Code-Chops/Idioma/blob/main/docs/language-codes.md): how a set's name gives its language, and the two ways to add a regional variant.
- [Libraries that use Idioma](https://github.com/Code-Chops/Idioma/blob/main/docs/libraries-and-apps.md): what a library does so that apps can translate and reword it, which project's languages your users get, what the build asks when such a library lacks one of them, and how your app changes one of its texts.
- [Blazor](https://github.com/Code-Chops/Idioma/blob/main/docs/blazor.md): a language picker for every render mode, a browser language the app doesn't have, dates and numbers, and a text as a component parameter.
- [What the build rejects](https://github.com/Code-Chops/Idioma/blob/main/docs/build-errors.md): every build error and warning by its code, what still converts to a text, and what the build misses.
- [Coming from resx](https://github.com/Code-Chops/Idioma/blob/main/docs/coming-from-resx.md): what becomes what, moving a resource file, and keeping a resource file as an override.
- [Performance](https://github.com/Code-Chops/Idioma/blob/main/docs/performance.md): how long a text takes to show next to `IStringLocalizer`, on .NET and in Blazor WebAssembly, and what Idioma adds to a download.
- [The demos](https://github.com/Code-Chops/Idioma/blob/main/demos/README.md): four complete apps, each in one file, which `dotnet run` runs without a project, and which fetch the latest package version.
- [Advanced topics](https://github.com/Code-Chops/Idioma/blob/main/docs/advanced-topics.md): what happens behind the texts you write, such as which wording a user sees and what the source generator writes. It also covers the tasks this page leaves out, such as registering a set by hand, testing with sets of your own, and writing a contract yourself.

## License

MIT. Code the source generator writes into your project is yours, and carries no obligations under this license.