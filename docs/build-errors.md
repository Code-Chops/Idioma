# What the build rejects

Idioma turns translation mistakes into build errors. Each message names what's wrong and what to change:

```text
error LOC402: 'ShopLocalizationNl.Greeting' doesn't use its parameter 'name', but the default wording of 'IShopLocalization.Greeting' shows it. Use 'name' in every language, or users of 'nl' won't see it.
```

Each subject has a page, with an example and the fix for every code. Your IDE offers a fix for the codes marked *(code fix)*.

The [README](../README.md#documentation) lists the pages on other subjects.

## All codes

The first digit of a code names its subject.

| Code                                                                                      | Mistake                                                     |
|-------------------------------------------------------------------------------------------|-------------------------------------------------------------|
| **[Languages](build-errors/LOC1XX-languages.md)**                                         |                                                             |
| [`LOC100`](build-errors/LOC1XX-languages.md#loc100) (code fix)                            | A set or standalone text in an assembly without languages   |
| [`LOC101`](build-errors/LOC1XX-languages.md#loc101)                                       | An invalid or repeated language                             |
| [`LOC102`](build-errors/LOC1XX-languages.md#loc102)                                       | A C# version older than 14                                  |
| [`LOC103`](build-errors/LOC1XX-languages.md#loc103)                                       | An app without languages                                    |
| [`LOC104`](build-errors/LOC1XX-languages.md#loc104) (warning)                             | A language code that doesn't exist, such as `dk`            |
| **[Sets](build-errors/LOC2XX-sets.md)**                                                   |                                                             |
| [`LOC200`](build-errors/LOC2XX-sets.md#loc200) (code fix)                                 | A default-language set the generator can't use              |
| [`LOC201`](build-errors/LOC2XX-sets.md#loc201) (code fix)                                 | A language without a set                                    |
| [`LOC202`](build-errors/LOC2XX-sets.md#loc202) (code fix)                                 | A set for a language that isn't listed                      |
| [`LOC203`](build-errors/LOC2XX-sets.md#loc203) (code fix)                                 | A set deriving from another language, as `nl` from `en`     |
| [`LOC204`](build-errors/LOC2XX-sets.md#loc204) (code fix)                                 | A set that doesn't list its contract                        |
| [`LOC205`](build-errors/LOC2XX-sets.md#loc205) (warning, code fix)                        | A set whose enums don't match its contract                  |
| [`LOC206`](build-errors/LOC2XX-sets.md#loc206) (code fix)                                 | A `LocalizationLanguageCode` that implements nothing        |
| [`LOC207`](build-errors/LOC2XX-sets.md#loc207)                                            | A `LocalizationLanguageCode` that isn't a culture name      |
| [`LOC208`](build-errors/LOC2XX-sets.md#loc208) (warning)                                  | A set that can't be registered automatically                |
| [`LOC209`](build-errors/LOC2XX-sets.md#loc209)                                            | Two sets of one contract for one language                   |
| **[Texts of a set](build-errors/LOC3XX-texts-of-a-set.md)**                               |                                                             |
| [`LOC300`](build-errors/LOC3XX-texts-of-a-set.md#loc300)                                  | A text with a name the generated class can't give it        |
| [`LOC301`](build-errors/LOC3XX-texts-of-a-set.md#loc301) (code fix)                       | A text with a setter                                        |
| [`LOC302`](build-errors/LOC3XX-texts-of-a-set.md#loc302) (code fix)                       | A text hidden instead of overridden                         |
| [`LOC303`](build-errors/LOC3XX-texts-of-a-set.md#loc303)                                  | A text the default-language set only inherits               |
| [`LOC304`](build-errors/LOC3XX-texts-of-a-set.md#loc304) (warning)                        | A text the default-language set lacks                       |
| [`LOC305`](build-errors/LOC3XX-texts-of-a-set.md#loc305) (warning)                        | A text of a set of an enum that isn't a member              |
| **[Texts with parameters](build-errors/LOC4XX-texts-with-parameters.md)**                 |                                                             |
| [`LOC400`](build-errors/LOC4XX-texts-with-parameters.md#loc400)                           | A public method that can't be a text                        |
| [`LOC401`](build-errors/LOC4XX-texts-with-parameters.md#loc401) (code fix)                | A parameter named differently than in the contract          |
| [`LOC402`](build-errors/LOC4XX-texts-with-parameters.md#loc402)                           | A value that a translation never uses                       |
| [`LOC403`](build-errors/LOC4XX-texts-with-parameters.md#loc403) (warning, code fix)       | A placeholder such as `{0}` in a wording                    |
| **[Standalone texts](build-errors/LOC5XX-inline-texts-and-attributes.md)**                |                                                             |
| [`LOC500`](build-errors/LOC5XX-inline-texts-and-attributes.md#loc500) (code fix)          | A language argument without its name                        |
| [`LOC501`](build-errors/LOC5XX-inline-texts-and-attributes.md#loc501) (warning, code fix) | An enum member without a text next to members with one      |
| [`LOC502`](build-errors/LOC5XX-inline-texts-and-attributes.md#loc502) (warning)           | A `[Localized]` text that won't be found                    |
| **[Reading a text](build-errors/LOC6XX-reading-a-text.md)**                               |                                                             |
| [`LOC600`](build-errors/LOC6XX-reading-a-text.md#loc600) (warning, code fix)              | An enum member that gives another member's text             |
| [`LOC601`](build-errors/LOC6XX-reading-a-text.md#loc601)                                  | A text asked of a member or a type that has none            |
| [`LOC602`](build-errors/LOC6XX-reading-a-text.md#loc602) (warning)                        | A text kept in a static field or property                   |
| **[Libraries](build-errors/LOC7XX-libraries.md)**                                         |                                                             |
| [`LOC700`](build-errors/LOC7XX-libraries.md#loc700) (code fix)                            | A library without a set for one of your languages           |
| [`LOC701`](build-errors/LOC7XX-libraries.md#loc701) (warning)                             | A library whose standalone texts lack one of your languages |
| [`LOC702`](build-errors/LOC7XX-libraries.md#loc702)                                       | A library built with an incompatible version of Idioma      |

## What the build misses

The build doesn't see three things.

First, wording written as a plain string compiles, and every user sees it in that one language:

```csharp
Title = "Orders"; // English, for every user
```

Write it as `LocalizedText.Inline(en: "Orders", nl: "Bestellingen")`, or in a set.

Second, plain text in Razor markup, such as `<h1>Orders</h1>`, isn't a text either. Write `<h1>@LocalizedText.Inline(en: "Orders", nl: "Bestellingen")</h1>`.

Third, a text that's kept outside a static initializer holds the wording of the moment it was read. The build reports a static field or property ([LOC602](build-errors/LOC6XX-reading-a-text.md#loc602)), and nothing else:

```csharp
public sealed class Menu // Registered as a singleton
{
    private readonly string label = ShopLocalization.Checkout; // Stays in the language of the first user
}
```

Read the text where you show it instead ([details](aspnet-core.md#what-to-watch-on-a-server)).

