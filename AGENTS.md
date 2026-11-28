# Idioma

## Writing style

This style applies to the README, the docs, build messages (diagnostics), exception messages and XML doc comments.

Keep the vocabulary:
- A *text* is the object, such as `ShopLocalization.Checkout`.
- Its *wording* is the string it shows in one language.
- A *set* is a class per language, the *contract* is the generated interface, and the *generated class* is `ShopLocalization`.
- A *standalone text* belongs to no set and carries its own wording in every language. It's an inline text (`LocalizedText.Inline`) or a text written as an attribute (`[Localized]`). Name the two apart only where they differ.
- A language *serves* a culture that is that language or a region of it, as `en` serves `en-GB`. A set serves the cultures its language serves.
- A *rewording* is a set that derives from another set of the same language and overrides some of its texts.
- The build *discovers* each set to register it, and each text of a set to put it in the contract. `[ExcludeFromLocalizationDiscovery]` keeps it from discovering one. Write "discover", not "recognize" or "pick up".
- The *user* is the person whose language a text shows in. Don't call them the reader.

Rules:

1. **One claim per sentence.** Use a colon only before an example or a list, never before a reason. Give a reason its own sentence, or join it with "because" or "so".
2. **Lead with the rule, then the reason.** Say first what to do or what happens. Explain why after that.
3. **Use the common term in headings and on first mention:** plural, fallback, placeholder, parameter, key. Readers search for these words.
4. **Use lists for more than two conditions.** No sentence with three "or"s, and no table cell longer than a line.
5. **Write contractions in prose and messages** (doesn't, can't, won't) where they read more naturally. Keep the examples' code exact.
6. **Name the language in a message when it's known.** Write "Dutch users won't see it", not "it is left out for the users of one language".
7. **Use British English next to American English (`en-GB`, `en`) for a regional example,** and Dutch (`nl`) as the second language.

Before and after:

> ~~Use the value in the wording of every language: otherwise it is left out for the users of one language.~~
> Use 'name' in every language, or users of 'nl' won't see it.

> ~~Each language chooses its wording for a number itself, in C#: English and Dutch have two forms, and a Polish set writes the conditions for its own. Idioma has no plural rules.~~
> Idioma has no plural rules. Each language writes its own conditions in C#: English and Dutch have two forms, and Polish has more.

## Code style

1. **Put `<summary>` and `</summary>` each on a line of their own,** also for a summary of one sentence. The generator writes the summaries of generated code the same way.

```csharp
/// <summary>
/// The class a diagnostic sits on, or <see langword="null"/>.
/// </summary>
```

2. **Put the tags of a `<param>` or `<exception>` each on a line of their own when its description takes more than one line.** A description of one line can stay between its tags.

```csharp
/// <param name="scripts">
/// The default script of each language, such as <c>Hant</c> for <c>zh-TW</c>, or <see langword="null"/> where the
/// build knows none. In the order of <paramref name="languages"/>.
/// </param>
/// <exception cref="ArgumentNullException">When one of the sets is <see langword="null"/>.</exception>
```
