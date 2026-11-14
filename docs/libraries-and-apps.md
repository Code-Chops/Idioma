# Libraries and apps

A library declares its languages, and so does an app. An app supporting a language a library does not have gets a build error naming the set to add. A library's set covers the regions of its language: an app that lists `en-GB` owes a library with an English set no British one. A library's inline texts cannot be given a language from outside, so for those the app gets a warning instead, and a library should write its texts in sets.

A library leaves its sets unsealed and its texts virtual, so an app can reword them: derive from the library's set in the same language, and override the texts to change, as in `public sealed class MyShopTranslationsEN : ShopTranslationsEN`. Such a rewording:

- Always wins over the library's own set, whichever assembly loads first. So does a set you register yourself.
- Reaches the library's regional sets: rewording `ShopTranslationsEN` changes a text for British users as well, unless `ShopTranslationsEnGB` has wording of its own for that text.
- Adds up with other rewordings: when a shared library of yours rewords some texts and your app rewords others of the same set, both show. When both reword the same text, the set registered last wins.

An app rewords a sealed set by implementing the library's contract in full, in the same language. That rewording does not reach the library's regional sets, as none can derive from a sealed set: reword each of those the same way.

An assembly without `[assembly: SupportedLanguages]` has no texts of its own to translate: its plain strings work as texts, and it has no `LocalizableText.Inline` and no contract of its own. It can still reword a library's set, or give a library's contract another language.