# Idioma

Translations for any .NET app, written in C#. No resource files, no reflection, no memory allocated when a text is shown, and safe to trim. The build makes sure every text exists in every language you support, and in Blazor WebAssembly, users switch language without reloading the app.

This README has two parts. [Getting started](#getting-started) shows in plain steps how to write your texts and show them. [How it works](#how-it-works) covers the details: which wording a user sees, what showing a text costs, what the source generator writes, how texts register themselves, how libraries and apps work together, and everything the build checks.

## Getting started

### Install

```shell
dotnet add package CodeChops.Idioma
```

The source generator comes with the package, so there is nothing else to set up.

### Declare your languages

Add this to any C# file of your project, after its `using` lines:

```csharp
using CodeChops.Idioma;

[assembly: SupportedLanguages("en", "nl")] // the first is the default
```

From then on, the build makes sure that every text of the project has each of these languages.

### Write your texts

Write a text in one of two ways, and mix them as you like:

- **One class per language**, for many reusable texts that belong together.
- **One line per text**, for single use in a component or page.

#### One class per language

Write the texts of your default language as a partial class named after that language, with one property per text:

```csharp
public partial class ShopTranslationsEN : ITranslations
{
    public virtual string Cart => "Cart";
    public virtual string Checkout => "Checkout";
    public virtual string Greeting => "Hello, {0}!";
}
```

The source generator then writes the contract `IShopTranslations`, which lists every text, and the class `ShopTranslations` to show them with. Write a class for every other language that implements the contract:

```csharp
public class ShopTranslationsNL : IShopTranslations
{
    public virtual string Cart => "Winkelwagen";
    public virtual string Checkout => "Afrekenen";
    public virtual string Greeting => "Hallo, {0}!";
}
```

Show a text by its name, wherever you need it:

```razor
<button>
  @ShopTranslations.Cart
</button>
```

A missing text or a missing language is a build error. There is nothing to register: every class registers itself.

#### One line per text

```razor
<button>
  @LocalizableText.Translated(en: "Checkout", nl: "Afrekenen")
</button>
```

`Translated` takes one argument per language you declared, so leaving a language out does not compile.

#### A regional variant, such as British English

`ShopTranslationsEN` serves every English culture, `en-US` and `en-GB` alike. For the texts that differ in Britain, list `en-GB`, and write a class with just those texts, named after the code in PascalCase and deriving from your English class:

```csharp
[assembly: SupportedLanguages("en", "nl", "en-GB")]

public class ShopTranslationsEnGB : ShopTranslationsEN
{
    public override string Cart => "Basket";
}
```

British users see "Basket", and the rest of your English. A class of texts with nothing British about it needs no British class: its English class serves Britain. A one-line text, though, takes a British wording too: `LocalizableText.Translated(en: "Color", nl: "Kleur", en_GB: "Colour")`. To leave one-line texts as they are, see [Language codes](#language-codes).

### Format a text

```razor
<p>@ShopTranslations.Greeting.Format(customer.Name)</p>
```

A user sees the wording for their language, and a user whose language you do not support sees your default language. A text goes wherever a `string` is expected (`string label = ShopTranslations.Checkout;`), and `Format` fills in its placeholders. Give every language the same placeholders: the build checks it. For the wording in another language than the user's, such as the language of an email's recipient, call `GetText`:

```csharp
string dutch = ShopTranslations.Checkout.GetText(CultureInfo.GetCultureInfo("nl"));
```

To fill in placeholders for that culture as well, pass it to `Format`, which then also formats the values for it:

```csharp
string greeting = ShopTranslations.Greeting.Format(CultureInfo.GetCultureInfo("nl-NL"), customer.Name);
```

### Switch language in Blazor WebAssembly

Every language is compiled into your app, so switching needs no download and no reload, unlike with resx files ([why](#switching-language-without-a-reload)). Set the culture, and a component shows the new language when it next renders; the one handling the click re-renders by itself:

```csharp
private void SelectLanguage(string name)
{
    var culture = CultureInfo.GetCultureInfo(name);
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = culture;
    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = culture;
}
```

Set the `DefaultThread` cultures too: a culture set only on `CultureInfo.CurrentUICulture` lasts until Blazor has handled the event, and the next event shows the old language again.

Dates and numbers follow the culture too, but they need its globalization data, and Blazor loads only the part that covers the culture the app starts with: an app starting in English shows Dutch dates in English. To load all of it, set `<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>` in the app's project file.

A text can be a parameter of a component with another render mode, such as an `InteractiveWebAssembly` component on a static page: Blazor passes it on as JSON, and the text carries every wording it can show, so it shows the user's language on the other side too, also after a switch.

### Compare texts

`text == "Afrekenen"` compares the wording for the current UI culture; a `null` text equals no wording, the empty string included. Comparing two texts, `text.Equals("Afrekenen")`, and `list.Contains("Afrekenen")` compare the texts themselves, whatever they currently show.

## How it works

### Which wording a user sees

A text follows `CultureInfo.CurrentUICulture`, and takes the first wording it finds:

1. The wording for that culture, or else for its parent culture: an `en` wording serves `en-US` and `en-GB` alike, and an `en-GB` wording wins over it in Britain.
2. The wording for the app's default language: the first one it declares. The texts of the libraries the app uses fall back to it as well, so no screen ends up in two languages. A culture none of the app's languages serves starts here, even where a library has its language: an app declaring `("en", "fr")` shows a Dutch user a library's texts in English, like its own.
3. The wording for the default language of the project that wrote the text.

`GetText` follows the same order for the culture you pass. `Format` formats its values for `CultureInfo.CurrentCulture`, or for the culture you pass, so a placeholder can carry a format, as in `{0:N2}` or `{0:d}`. It reads the wording as a format string whatever the number of values, so write a literal brace as `{{` or `}}`.

### What showing a text costs

Showing a text allocates no memory. A text finds its wording in the sets registered for the user's culture and hands you the string your set holds, whichever step of the order above it comes from, so a page full of texts leaves nothing for the garbage collector. That holds for `ToString`, `GetText`, a text in Razor markup, and `text == "Afrekenen"`.

Two things do allocate: `Format`, which builds a new string from the wording and your values, and a text of a set that builds its wording on every read (`public string Checkout => $"Pay at {this.ShopName}";`) instead of returning a constant.

### What the source generator writes

`LocalizableText.Translated` gets one required parameter per declared language, named after it.

From the set of the default language, `ShopTranslationsEN`, it writes the contract `IShopTranslations` and the class `ShopTranslations`. Each generated text links to its wording in every language, so you can jump from `ShopTranslations.Checkout` to `ShopTranslationsNL.Checkout`. The generated class is partial, so a helper can sit next to the texts, and it holds the constant `ResourceKeyPrefix` (`"Shop."`) next to them, so no text can take that name.

Each generated text is a `ResourceText` with a key, `Shop.Checkout` here. The package never reads the key: it is for an app or library that also looks texts up in resource files, such as through `IStringLocalizer`, so a resource file can override a wording without a rebuild.

### Registration

Every set registers itself: an app registers the sets of every assembly it references when it starts, whether it uses that assembly or not. An assembly the app loads later, such as one a Blazor WebAssembly app loads lazily, registers its sets when its code first runs; loading it alone does not.

For one contract and language, a set that rewords a library's set, or one you register yourself, always wins over the library's own, whichever assembly loads first.

Rewordings add up: a text shows the last rewording that changes it. So when a shared library of yours rewords some of a library's texts, and your app rewords others of the same set, both show.

A set that cannot register itself, because it has no parameterless constructor or no language code, gets build warning `LOC003`, and so does a set rewording another assembly's texts that is nested in another class or file-local, where generated code cannot reach it. To register a set by hand, mark it `[ExcludeFromTranslationDiscovery]` and call `TranslationsRegistry.Register`; the build still counts it as a set of its language, and still requires that language to be listed (`LOC005`), since a user whose culture your app does not declare sees your default language, whatever is registered. `TranslationsRegistry` also reads a set directly, with `TranslationsRegistry.Current.Find<IShopTranslations>(culture)`, and gives part of an app, or a test, sets of its own, with `TranslationsRegistry.Create`. Such a registry stands apart from your app's languages as well: it shows every language it has a set for, unless you pass it languages of its own, `TranslationsRegistry.Create(sets, "en", "nl")`.

### Language codes

A set's language is the end of its name. A two-letter code is the last two letters: `ShopTranslationsEN` serves `en`, and every culture under it.

A longer code, such as `en-GB`, `zh-Hans`, or `fil`, comes from the name as well, written in PascalCase (`ShopTranslationsEnGB`, `ShopTranslationsZhHans`, `ShopTranslationsFil`), when `[assembly: SupportedLanguages]` lists it. The build then records the code for the set, and a set deriving from it serves the same language. A name alone cannot tell `ShopTranslationsEnGB` (British English) from `AdminUiNL` (Dutch), so without the declaration only the last two letters count, and `ShopTranslationsEnGB` serves `gb`, which is no language (build error `LOC005`).

For English in the United States and Britain, you need no `en-US` set: `ShopTranslationsEN` serves `en-US`, `en-GB`, and every other English culture, so a British set holds only the texts that differ. There are two ways to add one:

- List `en-GB` in `[assembly: SupportedLanguages("en", "nl", "en-GB")]`. The name is enough, but every one-line text then takes a British wording too (`en_GB:`).

  ```csharp
  public class ShopTranslationsEnGB : ShopTranslationsEN
  {
      public override string Cart => "Basket";
  }
  ```

- Leave it out, `("en", "nl")`, so that one-line texts show their English in Britain, and implement `LanguageCode` in the set:

  ```csharp
  public class ShopTranslationsEnGB : ShopTranslationsEN, ITranslations
  {
      string ITranslations.LanguageCode => "en-GB";

      public override string Cart => "Basket";
  }
  ```

A set deriving from another set lists `ITranslations` again to implement `LanguageCode`, as in the second example; without it, its `LanguageCode` implements nothing (`LOC015`).

Separate `en-US` and `en-GB` sets without an `en` set work as well, but each then has every text, as one region cannot derive from another (`LOC006`: texts it does not override would stay American), and a user whose culture is plain `en`, or `en-AU`, gets your default language.

A regional default language works the same way: with `[assembly: SupportedLanguages("en-GB", "nl")]`, `public partial class ShopTranslationsEnGB : ITranslations` gives the contract `IShopTranslations`.

### Libraries and apps

A library declares its languages, and so does an app. An app supporting a language a library does not have gets a build error naming the set to add. A library's one-line texts cannot be given a language from outside, so for those the app gets a warning instead, and a library should use one class per language.

A library leaves its sets unsealed and its texts virtual, so an app can reword them by deriving. The rewording reaches the library's regional sets that derive from the reworded set too: rewording its `ShopTranslationsEN` changes a text for British users as well, unless its `ShopTranslationsEnGB` gives that text other wording than its English set.

An app rewords a sealed set by implementing the library's contract in full, in the same language. No regional set can derive from a sealed set, so each of the library's regional sets words every text itself and keeps its own wording: reword those the same way, one by one.

An assembly without `[assembly: SupportedLanguages]` has no texts of its own to translate: its plain strings work as texts, and it has no `LocalizableText.Translated` and no contract of its own. It can still reword a library's set, or give a library's contract another language.

### Texts in JSON

A text survives JSON, as a Blazor component parameter that crosses a render mode does. It is written with every wording it can show: its own translations, and for a text of a set, the wording of every language your app declares that a set is registered for, their regions included, as a user of that language sees it. Read back, it shows that wording for every culture without the sets, and a resource text keeps its key. It belongs to no contract any more, so a set registered later does not reword it.

A JSON string reads as a text too, with that one wording for every culture, so JSON you write by hand, such as a settings file, can hold `"title": "Orders"`.

### Switching language without a reload

With resx files (`IStringLocalizer`), Blazor WebAssembly downloads the translations, the satellite assemblies, only for the culture the app starts with. Switch to another language, and the texts stay in the default language until the app reloads. Microsoft's own [culture selector](https://learn.microsoft.com/aspnet/core/blazor/globalization-localization#dynamically-set-the-client-side-culture-by-user-preference) reloads the app for that (`forceLoad: true`). The only way around it is to download the satellite assemblies of every language at startup, with the runtime option `loadAllSatelliteResources`.

Here every language is compiled into your assemblies, so switching needs no download and no reload.

### What the build rejects

The build reports these mistakes. Most apply in an assembly that declares languages; a row says so when it applies elsewhere:

| Code               | Mistake                                                                                                                                             |
|--------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| `CS7036`           | `Translated` without one of the languages                                                                                                           |
| `CS0535`           | A set without one of the texts                                                                                                                      |
| `LOC001`           | A set in an assembly that declares no languages                                                                                                     |
| `LOC002`           | A default-language set the generator cannot extend: not partial, not named after its language, nested, file-local, generic, or abstract             |
| `LOC003` (warning) | A set that cannot register itself                                                                                                                   |
| `LOC004`           | A language without a set, for a generated contract or one written by hand                                                                           |
| `LOC005`           | A set for a language not listed, also one you register yourself                                                                                     |
| `LOC006`           | A set deriving from a set of another language, which would leave texts in that language, also in an assembly without languages                      |
| `LOC007`           | A set that does not implement its contract, which no text would read                                                                                |
| `LOC008`           | A language a referenced library has no set for                                                                                                      |
| `LOC009` (warning) | A language a referenced library's one-line texts lack, which nothing can add from outside                                                           |
| `LOC010`           | An invalid or repeated language in the attribute                                                                                                    |
| `LOC011`           | Wording written in one language: `"Orders"`, a `const`, `$"Order {number}"`, `"Order " + number`, `flag ? "Yes" : "No"`, and the same in Razor      |
| `LOC012`           | Calling by hand what only generated code should call                                                                                                |
| `LOC013`           | A language argument without its name, since `Translated("Bestellingen", "Orders")` would swap two languages unnoticed                               |
| `LOC014`           | A text named `ResourceKeyPrefix`, or named like the generated class itself                                                                          |
| `LOC015`           | A `LanguageCode` that implements nothing, on a set deriving from another set without listing `ITranslations`, also in an assembly without languages |
| `LOC016`           | Placeholders differing between languages, as in `Translated(en: "Hello, {0}!", nl: "Hallo!")`, also in a set, also in an assembly without languages |

A value that is no wording of yours still converts to a text, because it has no translations to demand: `Title = customer.Name`, `$"{first} {last}"`, `"-"`, and `"1234"`. The build cannot tell wording kept in a local variable or returned by a helper method from such a value, so those compile too. Plain text in Razor markup, such as `<h1>Orders</h1>`, is no text at all, so the build does not see it either: write `<h1>@LocalizableText.Translated(en: "Orders", nl: "Bestellingen")</h1>`.

## License

MIT. Code the source generator writes into your project is yours, and carries no obligations under this license.