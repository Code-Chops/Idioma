# Libraries that use Idioma

A library can use Idioma for its own texts, as an app does. It declares its languages with `[assembly: SupportedLanguages]` and writes its sets. This page covers such a library and an app that references it:

- which of the two decides the languages your users get
- what the app adds when the library lacks one of its languages
- how the app changes one of the library's texts
- what an assembly that declares no languages can still do

In the examples, the library is `Shop`. Its contract is `Shop.IShopLocalization`, and its English set is `ShopLocalizationEn`.

Other subjects have a page of their own: [language codes](language-codes.md), [Blazor](blazor.md), [what the build rejects](build-errors.md), [coming from resx](coming-from-resx.md), [performance](performance.md), and [how Idioma works](how-it-works.md).

## The app's languages

Your users get the languages of the app itself, the project that starts, such as the one that holds `Program.cs`. Its `[assembly: SupportedLanguages]` is what `LocalizationRegistry.SupportedLanguages` returns. Its first language is the fallback for every text, library texts included. What a library declares matters only to the library's own build, which checks its own texts against it.

So an app that keeps its texts in a library declares its languages in both projects, even when the app's own project has no texts:

```csharp
// In the app's own project, in Program.cs for example
[assembly: SupportedLanguages("en", "nl")]
```

The app's build then also checks that every library it uses has those languages (`LOC600`, below). Without the attribute, the app has no languages of its own:

- `LocalizationRegistry.SupportedLanguages` is empty, so a language picker has nothing to list.
- A culture without wording falls back to each text's own default language. An app with languages gives every text the same fallback.

An app that declares no languages takes those of an app it references, as the server project of a Blazor Web App takes those of its client project, and a test project those of the app it tests.

## A language the library lacks

If an app supports a language that a library doesn't have, the build names the set to add (`LOC600`):

```text
error LOC600: No set serves 'Shop.IShopLocalization' in 'de', which this assembly supports. Add 'class ShopLocalizationDe : ILocalizationSet, Shop.IShopLocalization'.
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

Nothing outside a library can add a language to its inline texts. For those, the app gets a warning instead (`LOC601`), which is why a library should write its texts in sets. The same goes for a text a library writes as an attribute, with `[Localized]`, because it carries its wording itself too.

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
- Reaches the library's regional sets. Rewording `ShopLocalizationEn` also changes a text for British users, unless `ShopLocalizationEnGb` has its own wording for that text.
- Adds up with other rewordings. When a shared library of yours rewords some texts and your app rewords others of the same set, both show. When both reword the same text, the set registered last wins.

A library allows this by leaving its sets unsealed and its texts `virtual`, as in `public virtual string Checkout => "Checkout";`. Where a library's set is sealed, or the text isn't virtual, implement the library's contract in full, in the same language. A sealed set has no regional sets to reach, because none can derive from it. Where the library's set isn't sealed, your wording also shows for its regional sets, for every text such a set leaves alone. Reword a text that a regional set changes the same way.

## An assembly without languages

An assembly without `[assembly: SupportedLanguages]` has no texts of its own to translate. Its plain strings work as texts, and it has no `LocalizableText.Inline`, no `[Localized]` and no contract of its own. It can still reword a library's set, or give a library's contract another language.

```csharp
public LocalizableText Title { get; } = "Orders"; // Compiles here, where it is LOC500 in an assembly with two languages
```