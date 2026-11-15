# Libraries that use Idioma

A library can use Idioma for its own texts, as an app does: it declares its languages with `[assembly: SupportedLanguages]` and writes its sets. This page is about such a library and an app that references it: which of the two decides the languages your users get, what the app adds when the library lacks one of its languages, how the app changes one of the library's texts, and what an assembly that declares no languages can still do.

In the examples, the library is `Shop`: its contract is `Shop.IShopTranslations`, and its English set is `ShopTranslationsEN`.

Other subjects have a page of their own: [how Idioma works](how-it-works.md), [language codes](language-codes.md), [Blazor](blazor.md), [what the build rejects](build-errors.md), and [coming from resx](coming-from-resx.md).

## The app's languages

Your users get the languages of the app itself: the project that starts, such as the one that holds `Program.cs`. Its `[assembly: SupportedLanguages]` is what `TranslationsRegistry.SupportedLanguages` returns, and its first language is the one every text falls back to, the texts of its libraries included. What a library declares is for the build of that library alone: the languages its own texts must have.

So an app that keeps its texts in a library declares its languages in both projects, also when the app's own project holds no text:

```csharp
// In the app's own project, in Program.cs for example
[assembly: SupportedLanguages("en", "nl")]
```

The build of the app then also checks that every library it uses has those languages (`LOC008`, below). Without the attribute, the app has no languages of its own: `TranslationsRegistry.SupportedLanguages` is empty, so a language picker has nothing to list, and a culture without wording gets each text's own default language, where an app with languages gives every text the same one.

An app that declares no languages takes those of an app it references, as the server project of a Blazor Web App takes those of its client project, and a test project those of the app it tests.

## A language the library lacks

An app supporting a language a library does not have gets a build error naming the set to add (`LOC008`):

```text
error LOC008: Nothing translates 'Shop.IShopTranslations' into 'de', which this assembly supports. Add 'class ShopTranslationsDE : Shop.IShopTranslations'.
```

Write that set in the app, as a class implementing the library's contract. It registers itself, like the app's own sets:

```csharp
public class ShopTranslationsDE : Shop.IShopTranslations
{
    public string Cart => "Warenkorb";
    public string Checkout => "Zur Kasse";
    public string Greeting(string name) => $"Hallo, {name}!";
}
```

A library's set covers the regions of its language: an app that lists `en-GB` need not add a British set to a library that has an English one. A library's inline texts cannot be given a language from outside, so for those the app gets a warning instead (`LOC009`), and a library should write its texts in sets.

## Changing a text of a library

To change the wording of a text that a library holds, reword it in your app: write a set that derives from the library's set of that language, name it after that language too, and override the text. This app changes the English wording of the library's `Checkout`:

```csharp
public sealed class MyShopTranslationsEN : ShopTranslationsEN
{
    public override string Checkout => "Pay now"; // Checkout is virtual in ShopTranslationsEN
}
```

Every other text keeps the library's wording, and so does every other language: for the Dutch wording of `Checkout`, derive a set from the library's Dutch one. The set registers itself, like the app's own sets.

Such a rewording:

- Always wins over the library's own set, whichever assembly loads first. So does a set you register yourself.
- Reaches the library's regional sets: rewording `ShopTranslationsEN` changes a text for British users as well, unless `ShopTranslationsEnGB` has wording of its own for that text.
- Adds up with other rewordings: when a shared library of yours rewords some texts and your app rewords others of the same set, both show. When both reword the same text, the set registered last wins.

A library allows this by leaving its sets unsealed and its texts `virtual`, as in `public virtual string Checkout => "Checkout";`. Where a library's set is sealed, or the text is not virtual, reword it by implementing the library's contract in full, in the same language. Rewording a sealed set does not reach the library's regional sets, as none can derive from a sealed set: reword each of those the same way.

## An assembly without languages

An assembly without `[assembly: SupportedLanguages]` has no texts of its own to translate: its plain strings work as texts, and it has no `LocalizableText.Inline` and no contract of its own. It can still reword a library's set, or give a library's contract another language.

```csharp
public LocalizableText Title { get; } = "Orders"; // Compiles here, where it is LOC011 in an assembly with two languages
```