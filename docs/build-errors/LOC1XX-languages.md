# Build errors about languages

Each code has an example and the fix. The [list of every code](../build-errors.md#all-codes) shows which ones your IDE can fix.

## LOC100

A set lists `ILocalizationSet` next to a contract of its own assembly, but the assembly declares no languages. The generator needs the languages to write the contract. Add `[assembly: SupportedLanguages("en", "nl")]` with your languages, the default first.

A set that rewords a library's set, or adds a language to a library's contract, needs no languages.

A call of `LocalizableText.Inline` in such an assembly is LOC100 too. The generator writes `Inline` with one parameter per language, so without languages the method doesn't exist, and the compiler reports `CS0117` next to it.

So is a member marked `[Localized]`. The generator writes that attribute with one parameter per language too, so without languages the compiler reports `CS0246` next to it.

The IDE fix declares the languages that an `Inline` call or a `[Localized]` names, in the order it names them. So `LocalizableText.Inline(en: "Orders", nl: "Bestellingen")` adds `[assembly: SupportedLanguages("en", "nl")]` at the top of its file, with English as the default. A call with an argument without a language name gets no fix. Neither does a set, because its name doesn't say which language is the default.

## LOC101

`[assembly: SupportedLanguages]` lists a language that isn't a culture name, or lists one twice. Both are build errors. List each language once, as a culture name such as `en` or `en-GB`.

A code that has the form of a culture name but names no language, such as `dk` for Danish, is [LOC104](#loc104).

## LOC102

The project declares its languages and compiles with a version of C# older than 14. Remove `<LangVersion>` from the project file, or set it to 14 or later. C# 14 is the default on .NET 10.

```text
error LOC102: Idioma needs C# 14 or later, and this project uses C# 13.0. Remove <LangVersion> from the project file, or set it to 14 or later.
```

The code generated for your languages uses extension members, which C# 14 brings. So the generators write none of it, and the compiler reports what your code then misses, such as the contract of a set (`CS0246`) or `LocalizableText.Inline` (`CS0117`).

A library that declares no languages builds with an older version, such as one that only references a library that uses Idioma. An app always declares its languages ([LOC103](#loc103)), so it needs C# 14.

## LOC103

The project builds an app, and it declares no languages. Add `[assembly: SupportedLanguages("en", "nl")]` with your languages, the default first.

```text
error LOC103: 'Shop.Server' is an app, but it declares no languages. Add [assembly: SupportedLanguages("en", "nl")] with your languages, the default first.
```

The app's attribute decides the languages your users get, and its first language is the fallback for every text. A library's attribute only describes the library's own texts. So an app declares its languages even when it has no texts of its own:

- a test project
- the server project of a Blazor Web App, which declares the same languages as its client
- an app that keeps its texts in a library

A library needs the attribute only for texts of its own ([LOC100](#loc100)).

The build takes a project for an app when it builds an executable or an Android app. A project that builds as a library but runs as the app, such as an AWS Lambda function, gets no LOC103. Declare its languages all the same, with `IsApplication = true` ([details](../libraries-and-apps.md#the-apps-languages)).

## LOC104

A language code has the form of a culture name, but its language or its region doesn't exist. This is a warning. The code compiles and its sets are complete, but no user's culture ever matches it:

```csharp
[assembly: SupportedLanguages("en", "dk")] // LOC104: Danish is 'da'
```

```text
warning LOC104: 'dk' won't match any user's culture, because 'dk' isn't an ISO 639-1 language code. Danish is 'da', so write 'da'.
warning LOC104: 'en-UK' won't match any user's culture, because 'UK' isn't an ISO 3166-1 region code. The United Kingdom is 'GB', so write 'en-GB'.
```

Write the code as .NET names the culture. The usual mistake is the code of a country in place of the code of a language:

| Written | Write   | Language or region |
|---------|---------|--------------------|
| `dk`    | `da`    | Danish             |
| `jp`    | `ja`    | Japanese           |
| `cz`    | `cs`    | Czech              |
| `gr`    | `el`    | Greek              |
| `ua`    | `uk`    | Ukrainian          |
| `cn`    | `zh`    | Chinese            |
| `en-UK` | `en-GB` | The United Kingdom |

The build checks two parts of a code:

- A language of two letters is a code of ISO 639-1.
- A region of two letters is a code of ISO 3166-1. `XK` for Kosovo counts too, because .NET uses it.

It doesn't check a longer language such as `fil`, a script such as `Hans`, or a region of three digits such as `419`. A code that doesn't have the form of a culture name is [LOC101](#loc101) or [LOC207](LOC2XX-sets.md#loc207).

The warning is reported in two places:

- at `[assembly: SupportedLanguages]`, for a language it lists
- at a set's `LocalizationLanguageCode`, for the constant it returns

`se` gets no warning, because it's Northern Sami. Swedish is `sv`.

The build carries its own lists of codes, so it reports the same on every machine. A list can lag behind its standard. If your code is right, turn the warning off with `#pragma warning disable LOC104` around the attribute or the property.
