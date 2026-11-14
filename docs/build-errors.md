# What the build rejects

The build reports these mistakes. Most apply in an assembly that declares languages; a row says so when it applies elsewhere. `LOC011` starts with the second language you declare: one language has nothing to be translated into. Your IDE offers a fix where one edit does: for `LOC002` making the class partial, naming it after its language, or removing `abstract` or `file`, for `LOC007` implementing the contract, and for `LOC017` turning `text.Equals(wording)` into `text == wording`.

| Code               | Mistake                                                                                                                                                                             |
|--------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `LOC001`           | A set with texts of its own in an assembly that declares no supported languages                                                                                                     |
| `LOC002`           | A default-language set the generator cannot extend: not partial, not named after its language, nested, file-local, generic, abstract, or without a public parameterless constructor |
| `LOC003` (warning) | A set that cannot register itself, also in an assembly without languages                                                                                                            |
| `LOC004`           | A language without a set of its own, also a listed region such as `en-GB` next to `en`, for a generated contract or one written by hand                                             |
| `LOC005`           | A set for a language not listed, also one you register yourself                                                                                                                     |
| `LOC006`           | A set deriving from a set of another language, which would leave texts in that language, also in an assembly without languages                                                      |
| `LOC007`           | A set that does not implement its contract, which no text would read                                                                                                                |
| `LOC008`           | A language a referenced library has no set for                                                                                                                                      |
| `LOC009` (warning) | A language a referenced library's inline texts lack, which nothing can add from outside                                                                                             |
| `LOC010`           | An invalid or repeated language in the attribute                                                                                                                                    |
| `LOC011`           | Wording written in one language: `"Orders"`, a `const`, `$"Order {number}"`, `"Order " + number`, `flag ? "Yes" : "No"`, and the same in Razor                                      |
| `LOC012`           | Calling by hand what only generated code should call                                                                                                                                |
| `LOC013`           | A language argument without its name, which could swap two languages unnoticed                                                                                                      |
| `LOC014`           | A text named like the generated class itself, as in `ShopTranslations.ShopTranslations`                                                                                             |
| `LOC015`           | A `LanguageCode` that implements nothing, on a set deriving from another set without listing `ITranslationSet`, also in an assembly without languages                               |
| `LOC016`           | Placeholders differing between languages, as in `Inline(en: "Hello, {0}!", nl: "Hallo!")`, also in a set, also in an assembly without languages                                     |
| `LOC017`           | A string compared as a text, as in `text.Equals("Orders")`, `list.Contains(name)`, or `counts[name]`, which no translated text equals, also in an assembly without languages        |
| `LOC018`           | A `LanguageCode` that is no culture name, such as `"en_GB"`, with which the set throws as it registers, also in an assembly without languages                                       |

A value that is not wording of yours still converts to a text, because it has no translations to demand: `Title = customer.Name`, `$"{first} {last}"`, `"-"`, and `"1234"`.

The build misses two things. It cannot tell such a value from wording kept in a local variable or returned by a helper method, so those compile too. And plain text in Razor markup, such as `<h1>Orders</h1>`, is not a text at all, so the build does not see it either: write `<h1>@LocalizableText.Inline(en: "Orders", nl: "Bestellingen")</h1>`.