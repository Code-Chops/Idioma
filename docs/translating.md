# Translating your texts

Your texts are C# in your repository. So an AI coding assistant can easily translate them as it knows the context, and a person can review each language in a pull request. This page covers both.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## Translating with an AI assistant

An assistant already knows your code. The build tells it what's missing:

- Add a language to the list, and the build names each set that is missing.
- Add a text, and the build names it for each language that lacks it.

So a new text doesn't wait for its translation. It gets wording in every language in the same change that adds it.

### Adding a language

To add German, list it with your languages:

```csharp
[assembly: SupportedLanguages(defaultLanguage: "en", "nl", "de")]
```

Then ask your assistant to translate, for example:

```text
Add German. Build, add each set and wording the build asks for, and build again until it passes.
Keep the parameters of each text, and don't change the other languages.
```

The build leads it there in three steps:

1. Build error `LOC201` names each set to add. Your IDE's fix adds the sets of every contract at once, without texts.
2. Build error `CS0535` names each text a new set lacks. Your IDE's "Implement interface" adds them, for the assistant to translate.
3. Build error `CS7036` names each inline text and each attribute without German, because every language is a parameter of `Inline` and `[Localized]`.

A library you use that lacks German gets build error `LOC600`, which names the set to add to your app ([details](libraries-and-apps.md#a-language-the-library-lacks)).

A translation that drops a value, such as a German greeting without `{name}`, is build error `LOC302`. So the assistant can't lose a value that your default language shows.

## Reviewing translations

Review each translation in the pull request that adds it. The build checks that every language has wording, not that anyone read it. So Dutch that an AI assistant wrote compiles just like Dutch that a translator checked.

Each language has a set of its own, so the diff of `ShopLocalizationNl.cs` shows exactly what changed in Dutch. On GitHub, a `CODEOWNERS` file asks the right reviewer for each language:

```text
*LocalizationNl.cs  @my-org/dutch-translators
```

An inline text and an attribute hold every language in one place. So their Dutch has no file of its own, and is reviewed in the pull request that changes that line.

Review a translation as code too, because a set is C#. A change to a set can run any code when its text is shown. So next to the translator, have a developer approve it.