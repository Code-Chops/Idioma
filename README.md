# Idioma

Translations for any .NET app, written in C#. No resource files, no reflection, no memory allocated when a text is shown, and safe to trim. The build makes sure every text exists in every language you support, and in Blazor WebAssembly, users switch language without reloading the app.

This README has three parts:
- [Getting started](#getting-started) shows how to write your texts and show them.
- [Run a demo](#run-a-demo) points to two small apps in the repository that you can run.
- [How it works](#how-it-works) covers the details for when you need them.

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

### Write your texts

Write a text in one of two ways, and mix them as you like:

- **A set per language**, each a class, for many reusable texts that belong together.
- **Inline text**, written in one line where you use it, for single use in a component or page.

#### A set

A set is a class that holds your texts in one language. Write the set of your default language as a partial class named after that language, with one property per text:

```csharp
public partial class ShopTranslationsEN : ITranslationSet
{
    public virtual string Cart => "Cart";
    public virtual string Checkout => "Checkout";
    public virtual string Greeting => "Hello, {0}!";
}
```

The source generator then writes the contract `IShopTranslations`, which lists every text, and the class `ShopTranslations` to show them with. Write a set for every other language, a class that implements the contract:

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

A missing text or a missing language is a build error. There is nothing to register: every set registers itself.

#### Inline text

```razor
<button>
  @LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")
</button>
```

`Inline` takes one argument per language you declared, so leaving a language out does not compile.

#### A regional variant, such as British English

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

To add British wording in one place only, see [Language codes](#language-codes).

### Format a text

```razor
<p>
  @ShopTranslations.Greeting.Format(customer.Name)
</p>
```

`Format` fills in a text's placeholders. Give every language the same placeholders: the build checks it.

A text goes wherever a `string` is expected: `string label = ShopTranslations.Checkout;`. A user sees the wording for their language, or your default language when you do not support theirs ([details](#which-wording-a-user-sees)).

For the wording in another language than the user's, such as the language of an email's recipient, pass its culture to `ToString`:

```csharp
string dutch = ShopTranslations.Checkout.ToString(CultureInfo.GetCultureInfo("nl"));
```

To fill in placeholders for that culture as well, pass it to `Format`, which then also formats the values for it:

```csharp
string greeting = ShopTranslations.Greeting.Format(CultureInfo.GetCultureInfo("nl-NL"), customer.Name);
```

### Compare texts

`text == "Afrekenen"` compares the wording for the current UI culture. A `null` text equals no wording, the empty string included.

Everything else compares the texts themselves, whatever they currently show: `text == otherText`, `text.Equals("Afrekenen")`, and `list.Contains("Afrekenen")`. So the last two are false for a translated text, even while it shows "Afrekenen". In a project with two or more languages the build rejects both (`LOC011`), so the mistake does not reach your users.

### Blazor

Every language is compiled into your app, so in Blazor WebAssembly switching needs no download and no reload, where resx files need a runtime option for that ([details](#switching-language-without-a-reload)). Set the culture, and a component shows the new language when it next renders; the one handling the click re-renders by itself:

```csharp
private void SelectLanguage(string name)
{
    var culture = CultureInfo.GetCultureInfo(name);
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = culture;
    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = culture;
}
```

Set the `DefaultThread` cultures too: a culture set only on `CultureInfo.CurrentUICulture` lasts until Blazor has handled the event, and the next event shows the old language again. Do this in WebAssembly only, where the app runs for one user: on a server it would change the default culture for every user.

On a server, every user has their own language. A text follows the culture ASP.NET Core gives the request or the Blazor Server circuit it is shown in, so set that up with request localization (`app.UseRequestLocalization`). A circuit keeps the culture of the request that opened it: to switch, store the new culture in the culture cookie and reload the page, as [Microsoft's culture selector](https://learn.microsoft.com/aspnet/core/blazor/globalization-localization#dynamically-set-the-server-side-culture-by-user-preference) does.

Dates and numbers follow the culture too, but they need its globalization data, and Blazor loads only the part that covers the culture the app starts with: an app starting in English shows Dutch dates in English. To load all of it, set `<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>` in the app's project file.

A text can be a parameter of a component with another render mode, such as an `InteractiveWebAssembly` component on a static page: Blazor passes it on as JSON, and the text still shows the user's language on the other side, even after a switch ([details](#texts-in-json)).

## Run a demo

The repository has two demos in its [`demos`](https://github.com/Code-Chops/Idioma/tree/main/demos) folder. Each is a complete app in one file, which `dotnet run` runs without a project:

- [`SetsDemo.cs`](https://github.com/Code-Chops/Idioma/blob/main/demos/SetsDemo.cs) writes its texts in a set per language.
- [`InlineTextsDemo.cs`](https://github.com/Code-Chops/Idioma/blob/main/demos/InlineTextsDemo.cs) writes its text inline.

Both support English, Dutch, and British English (`en-GB`), a [regional variant](#a-regional-variant-such-as-british-english) of English, and print their text in each. Run one from the `demos` folder of a clone:

```shell
dotnet run SetsDemo.cs
```

The first line of a demo fetches the latest version of the package. A clone runs it with the .NET SDK that the repository's `global.json` names; copied out of the repository, a demo runs with .NET 10 or later.

Remove a Dutch wording from either file, and it no longer builds.

## How it works

### Which wording a user sees

A text follows `CultureInfo.CurrentUICulture`, and takes the first wording it finds:

1. The wording for that culture, or else for its parent culture: an `en` wording serves `en-US` and `en-GB` alike, and an `en-GB` wording wins over it in Britain.
2. The wording for the app's default language: the first one it declares. The texts of the libraries the app uses fall back to it as well, so no screen ends up in two languages. A culture none of the app's languages serves starts here, even where a library has its language: an app declaring `("en", "fr")` shows a Dutch user a library's texts in English, like its own.
3. The wording for the default language of the project that wrote the text.

`ToString` follows the same order for the culture you pass. `Format` formats its values for `CultureInfo.CurrentCulture`, or for the culture you pass, so a placeholder can carry a format, as in `{0:N2}` or `{0:d}`. It always reads the wording as a format string, even without values, so write a literal brace as `{{` or `}}`.

### What showing a text costs

Showing a text allocates no memory. A text finds its wording in the sets registered for the user's culture and hands you the string your set holds, whichever step of the order above it comes from, so a page full of texts leaves nothing for the garbage collector. That holds for `ToString`, with a culture or without, a text in Razor markup, and `text == "Afrekenen"`.

Three things do allocate:

- `Format`, which builds a new string from the wording and your values.
- A text of a set that builds its wording on every read (`public string Checkout => $"Pay at {this.ShopName}";`) instead of returning a constant.
- An inline text written where it is shown, such as `@LocalizableText.Inline(en: "Checkout", nl: "Afrekenen")` in markup. Showing it allocates nothing, but every render creates the text itself anew: two small objects, which rarely matters. In a row of a long list that renders often it adds up, so keep the text in a static field or property there, and it is created once.

### What the source generator writes

`LocalizableText.Inline` gets one required parameter per declared language, named after it: `enGB` for `en-GB`.

From the set of the default language, `ShopTranslationsEN`, it writes the contract `IShopTranslations` and the class `ShopTranslations`. Each generated text links to its wording in every language, so you can jump from `ShopTranslations.Checkout` to `ShopTranslationsNL.Checkout`. The generated class is partial, so a helper can sit next to the texts, and it holds the constant `ResourceKeyPrefix` (`"Shop."`) next to them, so no text can take that name.

Each generated text is a `ResourceText` with a key, `Shop.Checkout` here. The package never reads the key: it is for an app or library that also looks texts up in resource files, such as through `IStringLocalizer`, so a resource file can override a wording without a rebuild.

### Registration

Every set registers itself: an app registers the sets of every assembly it references when it starts, whether it uses that assembly or not. An assembly the app loads later, such as one a Blazor WebAssembly app loads lazily, registers its sets when its code first runs; loading it alone does not.

A set that cannot register itself gets build warning `LOC003`:

- It has no parameterless constructor or no language code.
- It rewords another assembly's texts from a nested or file-local class, where generated code cannot reach it.

A nested, generic, or file-local class with your own texts is no set to the build, so its language is reported as missing (`LOC004`): make it a top-level class that is not generic.

To register a set by hand, mark it `[ExcludeFromTranslationDiscovery]` and call `TranslationsRegistry.Register`. The build still counts it as a set of its language, and still requires that language to be listed (`LOC005`).

`TranslationsRegistry` does two more things:

- It reads a set directly: `TranslationsRegistry.Current.Find<IShopTranslations>(culture)`.
- It gives part of an app, or a test, sets of its own: `TranslationsRegistry.Create(sets)`. Such a registry stands apart from your app's languages as well: it shows every language it has a set for, unless you pass it languages of its own, `TranslationsRegistry.Create(sets, "en", "nl")`.

### Language codes

A set's language is the end of its name. A two-letter code is the last two letters: `ShopTranslationsEN` serves `en`, and every culture under it.

A longer code, such as `en-GB`, `zh-Hans`, or `fil`, comes from the name as well, written in PascalCase (`ShopTranslationsEnGB`, `ShopTranslationsZhHans`, `ShopTranslationsFil`), but only when `[assembly: SupportedLanguages]` lists it. A set deriving from such a set serves the same language.

An inline text names a language as a set's name ends, in camelCase: `nl:` next to `ShopTranslationsNL`, and `enGB:`, `zhHans:`, and `fil:` next to the sets above.

A name alone cannot tell `ShopTranslationsEnGB` (British English) from `AdminUiNL` (Dutch). So when the code is not listed, only the last two letters count: `ShopTranslationsEnGB` serves `gb`, which is not a language (build error `LOC005`).

For English in the United States and Britain, you need no `en-US` set: `ShopTranslationsEN` serves `en-US`, `en-GB`, and every other English culture, so a British set holds only the texts that differ. There are two ways to add one:

- List `en-GB` in `[assembly: SupportedLanguages("en", "nl", "en-GB")]`. The name is enough. British English is then a language of its own to the build: every contract needs a British set (`LOC004`), which can derive from the English one and add nothing, and every inline text takes a British wording (`enGB:`).

  ```csharp
  public class ShopTranslationsEnGB : ShopTranslationsEN
  {
      public override string Cart => "Basket";
  }
  ```

- Leave it out, `("en", "nl")`, so that your other sets and your inline texts show their English in Britain, and implement `LanguageCode` in the set:

  ```csharp
  public class ShopTranslationsEnGB : ShopTranslationsEN, ITranslationSet
  {
      string ITranslationSet.LanguageCode => "en-GB";

      public override string Cart => "Basket";
  }
  ```

A set deriving from another set lists `ITranslationSet` again to implement `LanguageCode`, as in the second example; without it, its `LanguageCode` implements nothing (`LOC015`).

Separate `en-US` and `en-GB` sets without an `en` set work as well, with two drawbacks. Each set then has every text, as one region cannot derive from another (`LOC006`: texts it does not override would stay American). And a user whose culture is plain `en`, or `en-AU`, gets your default language.

A regional default language works the same way: with `[assembly: SupportedLanguages("en-GB", "nl")]`, `public partial class ShopTranslationsEnGB : ITranslationSet` gives the contract `IShopTranslations`.

### Libraries and apps

A library declares its languages, and so does an app. An app supporting a language a library does not have gets a build error naming the set to add. A library's set covers the regions of its language: an app that lists `en-GB` owes a library with an English set no British one. A library's inline texts cannot be given a language from outside, so for those the app gets a warning instead, and a library should write its texts in sets.

A library leaves its sets unsealed and its texts virtual, so an app can reword them: derive from the library's set in the same language, and override the texts to change, as in `public sealed class MyShopTranslationsEN : ShopTranslationsEN`. Such a rewording:

- Always wins over the library's own set, whichever assembly loads first. So does a set you register yourself.
- Reaches the library's regional sets: rewording `ShopTranslationsEN` changes a text for British users as well, unless `ShopTranslationsEnGB` has wording of its own for that text.
- Adds up with other rewordings: when a shared library of yours rewords some texts and your app rewords others of the same set, both show. When both reword the same text, the set registered last wins.

An app rewords a sealed set by implementing the library's contract in full, in the same language. That rewording does not reach the library's regional sets, as none can derive from a sealed set: reword each of those the same way.

An assembly without `[assembly: SupportedLanguages]` has no texts of its own to translate: its plain strings work as texts, and it has no `LocalizableText.Inline` and no contract of its own. It can still reword a library's set, or give a library's contract another language.

### Texts in JSON

A text survives JSON, such as a Blazor component parameter that crosses a render mode. It is written with every wording it can show: its own translations, and for a text of a set, the wording of every language your app declares that has a registered set, regions included. Read back, it shows that wording for every culture without needing the sets, and a resource text keeps its key. It belongs to no contract any more, so a set registered later does not reword it.

A JSON string reads as a text too, with that one wording for every culture, so JSON you write by hand, such as a settings file, can hold `"title": "Orders"`.

### Switching language without a reload

With resx files (`IStringLocalizer`), Blazor WebAssembly downloads the translations, the satellite assemblies, only for the culture the app starts with. Switch to another language, and the texts stay in the default language until the app reloads. Microsoft's own [culture selector](https://learn.microsoft.com/aspnet/core/blazor/globalization-localization#dynamically-set-the-client-side-culture-by-user-preference) reloads the app for that (`forceLoad: true`). The only way around it is to download the satellite assemblies of every language at startup, with the runtime option `loadAllSatelliteResources`.

Here every language is compiled into your assemblies, so switching needs no download and no reload.

### What the build rejects

The build reports these mistakes. Most apply in an assembly that declares languages; a row says so when it applies elsewhere. `LOC011` starts with the second language you declare: one language has nothing to be translated into. Your IDE offers a fix where one edit does: for `LOC002` making the class partial, naming it after its language, or removing `abstract` or `file`, and for `LOC007` implementing the contract.

| Code               | Mistake                                                                                                                                                                      |
|--------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `LOC001`           | A set with texts of its own in an assembly that declares no supported languages                                                                                              |
| `LOC002`           | A default-language set the generator cannot extend: not partial, not named after its language, nested, file-local, generic, abstract, or without a parameterless constructor |
| `LOC003` (warning) | A set that cannot register itself                                                                                                                                            |
| `LOC004`           | A language without a set of its own, also a listed region such as `en-GB` next to `en`, for a generated contract or one written by hand                                      |
| `LOC005`           | A set for a language not listed, also one you register yourself                                                                                                              |
| `LOC006`           | A set deriving from a set of another language, which would leave texts in that language, also in an assembly without languages                                               |
| `LOC007`           | A set that does not implement its contract, which no text would read                                                                                                         |
| `LOC008`           | A language a referenced library has no set for                                                                                                                               |
| `LOC009` (warning) | A language a referenced library's inline texts lack, which nothing can add from outside                                                                                      |
| `LOC010`           | An invalid or repeated language in the attribute                                                                                                                             |
| `LOC011`           | Wording written in one language: `"Orders"`, a `const`, `$"Order {number}"`, `"Order " + number`, `flag ? "Yes" : "No"`, and the same in Razor                               |
| `LOC012`           | Calling by hand what only generated code should call                                                                                                                         |
| `LOC013`           | A language argument without its name, which could swap two languages unnoticed                                                                                               |
| `LOC014`           | A text named `ResourceKeyPrefix`, or named like the generated class itself                                                                                                   |
| `LOC015`           | A `LanguageCode` that implements nothing, on a set deriving from another set without listing `ITranslationSet`, also in an assembly without languages                        |
| `LOC016`           | Placeholders differing between languages, as in `Inline(en: "Hello, {0}!", nl: "Hallo!")`, also in a set, also in an assembly without languages                              |

A value that is not wording of yours still converts to a text, because it has no translations to demand: `Title = customer.Name`, `$"{first} {last}"`, `"-"`, and `"1234"`.

The build misses two things. It cannot tell such a value from wording kept in a local variable or returned by a helper method, so those compile too. And plain text in Razor markup, such as `<h1>Orders</h1>`, is not a text at all, so the build does not see it either: write `<h1>@LocalizableText.Inline(en: "Orders", nl: "Bestellingen")</h1>`.

## License

MIT. Code the source generator writes into your project is yours, and carries no obligations under this license.