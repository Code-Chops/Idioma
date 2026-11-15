# What the build rejects

The build reports the mistakes in the table below.

- Most apply only in an assembly that declares languages. The last column marks those that apply in an assembly without languages as well.
- `LOC011` starts with the second language you declare: one language has nothing to be translated into.
- Your IDE offers a fix where one edit does:
  - `LOC002`: it makes the class partial, names it after its language, or removes `abstract` or `file`.
  - `LOC007`: it implements the contract.
  - `LOC017`: it turns `text.Equals(wording)` into `text == wording`.

Other subjects have a page of their own: [how Idioma works](how-it-works.md), [language codes](language-codes.md), [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), and [coming from resx](coming-from-resx.md).

## The codes

Each message says what to change, as this one does:

```text
error LOC011: This text is written in one language, and this assembly supports en, nl. Write it as LocalizableText.Inline(en: "...", nl: "...").
```

| Code               | Mistake                                                                                                                                                                             | Without languages |
|--------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------|
| `LOC001`           | A set with texts of its own                                                                                                                                                         | Only there        |
| `LOC002`           | A default-language set the generator cannot extend: not partial, not named after its language, nested, file-local, generic, abstract, or without a public parameterless constructor |                   |
| `LOC003` (warning) | A set that cannot register itself                                                                                                                                                   | Yes               |
| `LOC004`           | A language without a set of its own, also a listed region such as `en-GB` next to `en`, for a generated contract or one written by hand                                             |                   |
| `LOC005`           | A set for a language not listed, also one you register yourself, and one whose name does not end in a language code: its last two letters count, as `ts` for `ShopTexts`            |                   |
| `LOC006`           | A set deriving from a set of another language, which would leave texts in that language                                                                                             | Yes               |
| `LOC007`           | A set that does not implement its contract, which no text would read                                                                                                                |                   |
| `LOC008`           | A language a referenced library has no set for                                                                                                                                      |                   |
| `LOC009` (warning) | A language a referenced library's inline texts lack, which nothing can add from outside                                                                                             |                   |
| `LOC010`           | An invalid or repeated language in the attribute                                                                                                                                    |                   |
| `LOC011`           | Wording written in one language: `"Orders"`, a `const`, `$"Order {number}"`, `"Order " + number`, `flag ? "Yes" : "No"`, and the same in Razor                                      |                   |
| `LOC012`           | Calling by hand what only generated code should call                                                                                                                                |                   |
| `LOC013`           | A language argument without its name, which could swap two languages unnoticed                                                                                                      |                   |
| `LOC014`           | A text named like the generated class itself, as in `ShopTranslations.ShopTranslations`                                                                                             |                   |
| `LOC015`           | A `LanguageCode` that implements nothing, on a set deriving from another set without listing `ITranslationSet`                                                                      | Yes               |
| `LOC016`           | A placeholder of a format string in a wording, as in `"Hello, {0}!"`, in a set or in `LocalizableText.Inline`: nothing fills it in, so write the value as a parameter               | Yes               |
| `LOC017`           | A string compared as a text, as in `text.Equals("Orders")`, `list.Contains(name)`, or `counts[name]`, which no translated text equals                                               | Yes               |
| `LOC018`           | A `LanguageCode` that is not a culture name, such as `"en_GB"`, which makes the set throw as it registers                                                                           | Yes               |
| `LOC019`           | A public method of a default-language set that returns a string and takes parameters, but cannot be a text: generic, sharing its name, or with a parameter the text cannot keep     |                   |
| `LOC020`           | A parameter of a text that takes values named differently than in its contract, as in `Invited(string guest, string host)` for `Invited(string host, string guest)`                 | Yes               |
| `LOC021`           | A text that leaves out a value the default wording shows, as in a Dutch `Greeting(string name) => "Hallo!"` for an English `$"Hello, {name}!"`                                      | Yes               |

## What still converts

A value that is not wording of yours still converts to a text, because it has no translations to demand:

```csharp
Title = customer.Name;     // A value read at run time
Title = $"{first} {last}"; // Values alone, without wording
Title = "-";               // No letters
Title = "1234";
```

## What the build misses

The build misses two things. It cannot tell such a value from wording kept in a local variable or returned by a helper method, so those compile too:

```csharp
var title = "Orders";
Title = title;      // Wording, but the build sees a variable
Title = GetTitle(); // Wording, but the build sees a method call
```

And plain text in Razor markup, such as `<h1>Orders</h1>`, is not a text at all, so the build does not see it either: write `<h1>@LocalizableText.Inline(en: "Orders", nl: "Bestellingen")</h1>`.