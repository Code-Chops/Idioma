# Libraries that use Idioma

A library can use Idioma for its own texts, as an app does. It declares its languages with `[assembly: SupportedLanguages]` and writes its sets. This page covers such a library and an app that references it:

- what the library does so that apps can translate and reword it
- which of the two decides the languages your users get
- what the app adds when the library lacks one of its languages
- how the app changes one of the library's texts
- what a library that declares no languages can still do

In the examples, the library is `Shop`. Its contract is `Shop.IShopLocalization`, and its English set is `ShopLocalizationEn`.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## Writing a library

Three choices decide what an app that uses your library can change:

- **Write texts in sets.** An app can add a language to a set, but not to an inline text or a text written as an attribute ([LOC701](build-errors/LOC7XX-libraries.md#loc701)).
- **Make the sets public.** A contract takes the accessibility of its set, and an app needs a public contract to add a language.
- **Leave the sets unsealed and the texts `virtual`.** An app can then reword one text and keep the rest ([Changing a text of a library](#changing-a-text-of-a-library)).

## The app's languages

Your users get the languages of the app itself, the project that starts, such as the one that holds `Program.cs`. Its `[assembly: SupportedLanguages]` is what `LocalizationRegistry.SupportedLanguages` returns. Its first language is the fallback for every text, library texts included. What a library declares matters only to the library's own build, which checks its own texts against it.

So every app declares its languages, even when its own project has no texts. An app without the attribute doesn't build ([LOC103](build-errors/LOC1XX-languages.md#loc103)):

```csharp
// In the app's own project, in Program.cs for example
[assembly: SupportedLanguages(defaultLanguage: "en", "en-GB", "nl")]
```

The app's build then also checks that every library it uses has those languages (`LOC700`, below).

Every project that starts is an app, and the languages of an app it references don't count:

- **An app that keeps its texts in a library** declares its languages in both projects.
- **A host that reaches a library through another project** declares them too. The host decides, not the projects in between.
- **The server project of a Blazor Web App** declares the same languages as its client project.
- **A test project** declares the languages its tests read.

The build takes a project for an app when it builds an executable or an Android app. A project that a host loads, such as an AWS Lambda function or an add-in of Excel, builds as a library. Set the MSBuild property `IdiomaApplication` in its project file, so the build takes it for the app:

```xml
<PropertyGroup>
  <IdiomaApplication>true</IdiomaApplication>
</PropertyGroup>
```

Like any other app, it then declares its languages ([LOC103](build-errors/LOC1XX-languages.md#loc103)).

A process that no such app starts has no languages, such as a host written in F# that loads your library. There, `LocalizationRegistry.SupportedLanguages` is empty, every culture shows its own wording, and each text falls back to its own default language.

## A language the library lacks

If an app supports a language that a library doesn't have, the build names the set to add (`LOC700`):

```text
error LOC700: No set serves 'Shop.IShopLocalization' in 'de', which this assembly supports. Add 'class ShopLocalizationDe : ILocalizationSet, Shop.IShopLocalization'.
```

Write that set in the app, as a class implementing the library's contract. Your IDE offers a fix that adds the class without texts. It's registered automatically, like the app's own sets:

```csharp
public class ShopLocalizationDe : ILocalizationSet, Shop.IShopLocalization
{
    public string Cart => "Warenkorb";
    public string Checkout => "Zur Kasse";
    public string Greeting(string name) => $"Hallo, {name}!";
}
```

A library's set covers the regions of its language. An app that lists `en-GB` doesn't need to add a British set to a library that has an English one.

Nothing outside a library can add a language to its inline texts. For those, the app gets a warning instead (`LOC701`), which is why a library should write its texts in sets. The same goes for a text a library writes as an attribute, with `[Localized]`, because it carries its wording itself too.

## Changing a text of a library

To change the wording of a library's text, write a set in your app that derives from the library's set for that language. Give it a name that ends in the same language, and override the text. This app changes the English wording of the library's `Checkout`:

```csharp
public sealed class MyShopLocalizationEn : ShopLocalizationEn
{
    public override string Checkout => "Pay now"; // Checkout is virtual in ShopLocalizationEn
}
```

Every other text keeps the library's wording, and so does every other language. To change the Dutch wording of `Checkout`, derive a set from the library's Dutch one. The set is registered automatically, like the app's own sets.

Such a rewording:

- Always wins over the library's own set, whichever assembly loads first. So does a set you register yourself.
- Reaches the library's regional sets. Rewording `ShopLocalizationEn` also changes a text for British users, unless `ShopLocalizationEnGb` has its own wording for that text. To change that wording too, reword `ShopLocalizationEnGb` the same way.
- Adds up with other rewordings. When a shared library of yours rewords some texts and your app rewords others of the same set, both show. When both reword the same text, the set registered last wins.

A library allows this by leaving its sets unsealed and its texts `virtual`, as in `public virtual string Checkout => "Checkout";`. Where a library's set is sealed, or the text isn't virtual, implement the library's contract in full, in the same language. Such a set reaches the library's regional sets too, as a rewording does.

## An assembly without languages

A library without `[assembly: SupportedLanguages]` has no texts of its own to translate. It has no `LocalizedText.Inline`, no `[Localized]` and no contract of its own. It can still reword a library's set, or give a library's contract another language.

An app always declares its languages ([The app's languages](#the-apps-languages)).
