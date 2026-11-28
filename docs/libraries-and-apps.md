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

- **Write texts in sets.** An app can add a language to a set, but not to a standalone text, which is an inline text or a text written as an attribute ([LOC701](build-errors/LOC7XX-libraries.md#loc701)).
- **Make the sets public.** A contract takes the accessibility of its set, and an app needs a public contract to add a language.
- **Leave the sets unsealed and the texts `virtual`.** An app can then reword one text and keep the rest ([Changing a text of a library](#changing-a-text-of-a-library)). A `protected virtual` member that several texts read, such as the shop's name, lets an app change all of them at once.

A set of your library that needs constructor arguments can't register itself. Mark it `[ExcludeFromLocalizationDiscovery]` and pass an instance to `LocalizationRegistry.RegisterLibrarySets`, such as from your library's setup method. Unlike `LocalizationRegistry.Register`, it registers the set as the library's own, so an app's rewording still wins over it.

## The app's languages

Your users get the languages of the app itself, the project that starts, such as the one that holds `Program.cs`. Its `[assembly: SupportedLanguages]` is what `LocalizationRegistry.SupportedLanguages` returns. Its first language is the fallback for every text, library texts included. What a library declares matters only to the library's own build, which checks its own texts against it.

So every app declares its languages, even when its own project has no texts. An app without the attribute doesn't build ([LOC101](build-errors/LOC1XX-languages.md#loc101)):

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

Like any other app, it then declares its languages ([LOC101](build-errors/LOC1XX-languages.md#loc101)).

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

Nothing outside a library can add a language to its standalone texts, which it writes with `LocalizedText.Inline` or `[Localized]`. They carry their wording themselves. For those, the app gets a warning instead (`LOC701`), which is why a library should write its texts in sets. An enum whose members a library marks `[Localized]` is the exception: write a set of the enum in the app, and the library's `GetText()` gives its wording ([Enums](enums.md#a-set-of-the-enum)).

## Changing a text of a library

To change the wording of a library's text, write a set in your app that derives from the library's set for that language. Give it a name that ends in the same language, and override the text. This app changes the English wording of the library's `Checkout`:

```csharp
public sealed class MyShopLocalizationEn : ShopLocalizationEn
{
    public override string Checkout => "Pay now"; // Checkout is virtual in ShopLocalizationEn
}
```

Every other text keeps the library's wording, and so does every other language. To change the Dutch wording of `Checkout`, derive a set from the library's Dutch one. The set is registered automatically, like the app's own sets.

### Which set wins

For each contract and language, one set wins and words every text. It's the first of these:

1. A set you register yourself with `LocalizationRegistry.Register`. Of several, the one registered last wins.
2. A rewording in your app.
3. A rewording in another assembly, such as a shared library of yours. Of several, the one registered last wins.
4. The library's own set.

So a rewording always wins over the library's own set, whichever assembly loads first.

### Regional sets

A rewording doesn't reach a region that the library has a set for. British users see `ShopLocalizationEnGb`, so rewording `ShopLocalizationEn` doesn't change what they see. Reword the British set too, the same way:

```csharp
public sealed class MyShopLocalizationEnGb : ShopLocalizationEnGb
{
    public override string Checkout => "Pay now";
}
```

The build warns about a regional set that you don't reword ([LOC208](build-errors/LOC2XX-sets.md#loc208)).

### Several rewordings of one set

Two rewordings of one set don't add up. When a shared library of yours rewords some texts and your app rewords others, only your app's rewording shows. To show both, derive one from the other:

```csharp
public sealed class MyShopLocalizationEn : SharedShopLocalizationEn // SharedShopLocalizationEn derives from ShopLocalizationEn
{
    public override string Cart => "Bag";
}
```

Your app's rewording then wins, with the texts of both. The build warns about rewordings that don't add up ([LOC209](build-errors/LOC2XX-sets.md#loc209)).

### Members that aren't texts

A rewording can also override a member that isn't a text, such as a protected `ShopName` that the library's texts read. Every text of the set then shows with that member, because your rewording is the set that's read:

```csharp
public sealed class MyShopLocalizationEn : ShopLocalizationEn
{
    protected override string ShopName => "Contoso"; // ShopName is protected virtual in ShopLocalizationEn
}
```

For your British users, override it in your rewording of `ShopLocalizationEnGb` too.

### Sealed sets

A library allows all this by leaving its sets unsealed and its texts `virtual`, as in `public virtual string Checkout => "Checkout";`. Where a library's set is sealed, or the text isn't virtual, implement the library's contract in full, in the same language. Such a set wins as a rewording does.

## An assembly without languages

A library without `[assembly: SupportedLanguages]` has no texts of its own to translate. It has no `LocalizedText.Inline`, no `[Localized]` and no contract of its own. It can still reword a library's set, or give a library's contract another language.

An app always declares its languages ([The app's languages](#the-apps-languages)).
