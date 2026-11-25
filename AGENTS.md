# Idioma

## Writing style

This style applies to the README, the docs, build messages (diagnostics), exception messages and XML doc comments.

Keep the vocabulary:
- A *text* is the object, such as `ShopLocalization.Checkout`.
- Its *wording* is the string it shows in one language.
- A *set* is a class per language, the *contract* is the generated interface, and the *generated class* is `ShopLocalization`.

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
