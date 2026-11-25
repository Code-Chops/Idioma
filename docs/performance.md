# Performance

Idioma shows a text in less time than `IStringLocalizer`, which is the approach Microsoft recommends for localizing an app. This page compares how long a text takes to show with each, on .NET and in Blazor WebAssembly. It also compares what each adds to a Blazor WebAssembly download, where `IStringLocalizer` comes with its resx files.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## In short

- On .NET, a plain text of a set takes under a tenth of the time `IStringLocalizer` takes. It allocates nothing, where `IStringLocalizer` allocates 72 bytes.
- In Blazor WebAssembly with Mono's interpreter, the same takes a set under a tenth of the time too.
- A British user's text costs a set the same as any other. `IStringLocalizer` takes three times as long for a text that comes from English, because it looks for British wording first.
- A text with a parameter takes a set a tenth of what `IStringLocalizer` takes on .NET. It allocates only the string it returns.
- For a user whose language falls back, such as a German user of an app in English and Dutch, a text with a parameter takes about eight times as long. Its method runs in the culture of the language it falls back to, so the culture is switched for the call. It still takes a set less time than `IStringLocalizer` takes, and allocates only the string it returns.
- An inline text takes a tenth of what `IStringLocalizer` takes, and a text written as an attribute a seventh. Both allocate nothing.
- An inline text with a parameter builds its wording in every language on each call. With three languages, it takes a quarter of what `IStringLocalizer` takes.
- Idioma adds more to a Blazor WebAssembly download than resx and `IStringLocalizer` do. With 100 texts and the interpreter, Idioma adds 32.4 KB, compressed, plus 1.6 KB for each extra language. resx and `IStringLocalizer` add 19.3 KB, plus 3.8 KB for a user whose language isn't the default.

So speed rarely decides between them. A page that shows 200 texts spends about 9 µs on them with `IStringLocalizer` on .NET, and under 1 µs with Idioma's sets. With Mono's interpreter in WebAssembly, it spends about 0.15 ms with `IStringLocalizer`, and about 0.01 ms with Idioma's sets.

## What was measured

Three texts: `Checkout` (plain), `Cart` (British English rewords it as "Basket"), and `Greeting(name)` (with a parameter).

Four approaches show them:

- **IStringLocalizer**: `localizer["Checkout"]` on an `IStringLocalizer<Shop>`, from resx files (the approach Microsoft recommends). The resx files also get the class resx generates, as an app gets it from its IDE, and `IStringLocalizer<Shop>` is named after it.
- **Idioma set**: `ShopLocalization.Checkout`.
- **Inline**: `LocalizedText.Inline(...)`, called each time the text is shown, as markup calls it on every render.
- **Attribute**: `LocalizedText.GetTextByMember<ShopLabels>(nameof(ShopLabels.Checkout))`, for a property marked `[Localized]`. The text is found by its type and the name of its member each time, as a form finds the label of a field. An attribute takes no value, so it has no text with a parameter.

The app declares `("en", "nl", "en-GB")`. "Region" is a British user. "Fallback" is a German user. The app doesn't have German, so that user sees English, the default language.

A Blazor app can make its default language the UI culture of such a user, as [Blazor](blazor.md#a-browser-language-the-app-doesnt-have) shows. A German user's texts then take as long as for a user of the default language.

Each benchmark first checks that every approach shows the expected wording, so no approach is timed doing less work.

The numbers come from one run on one machine, an AMD Ryzen AI 9 HX PRO 370 on Windows 11. They ran on .NET 11.0.0 with BenchmarkDotNet 0.16.0-preview.2, and in Blazor WebAssembly on .NET 11.0.0 in a headless Chrome 158. [Its tables](../benchmarks/results) are kept. Compare the approaches with each other rather than with your own numbers, because those depend on the machine.

What wasn't measured again since a text became a `string`:

- Mono's AOT in Blazor WebAssembly, which needs the `wasm-tools` workload.
- A second machine. The tables in [`benchmarks/earlier-design-results`](../benchmarks/earlier-design-results) are from an Apple M2 Max and from before that change, so they describe the earlier design.

An earlier run, on the same machine while it was busy, gave times up to a quarter longer. Its factors were the same, so compare the factors rather than the nanoseconds.

## Showing a text

Each benchmark shows one text once, as a render does. The `IStringLocalizer` column shows how long that takes. The other columns show their time as a factor of it, so 0.50× takes half as long. On .NET, a cell also shows the bytes allocated each time a text is shown.

### On .NET

| Text, user's culture                  | IStringLocalizer | Idioma set   | Inline       | Attribute  |
|---------------------------------------|------------------|--------------|--------------|------------|
| Plain text, default language          | 45 ns, 72 B      | 0.08×, 0 B   | 0.11×, 0 B   | 0.15×, 0 B |
| Plain text, second language           | 47 ns, 72 B      | 0.08×, 0 B   | 0.11×, 0 B   | 0.14×, 0 B |
| Plain text, region                    | 148 ns, 168 B    | 0.03×, 0 B   | 0.03×, 0 B   | 0.05×, 0 B |
| Reworded text, region                 | 45 ns, 72 B      | 0.08×, 0 B   | 0.10×, 0 B   | 0.14×, 0 B |
| Plain text, fallback                  | 45 ns, 72 B      | 0.08×, 0 B   | 0.10×, 0 B   | 0.14×, 0 B |
| Text with parameter, default language | 70 ns, 152 B     | 0.09×, 48 B  | 0.24×, 144 B | –          |
| Text with parameter, second language  | 69 ns, 152 B     | 0.09×, 48 B  | 0.25×, 144 B | –          |
| Text with parameter, region           | 165 ns, 248 B    | 0.04×, 48 B  | 0.10×, 144 B | –          |
| Text with parameter, fallback         | 72 ns, 152 B     | 0.70×, 48 B  | 0.24×, 144 B | –          |

### In Blazor WebAssembly

Blazor WebAssembly runs .NET code on the Mono runtime by default, with an interpreter. Publishing with `RunAOTCompilation` compiles it to WebAssembly ahead of time (AOT) instead, which makes the download bigger. Only the interpreter was measured.

The browser has no BenchmarkDotNet, so the app times each text itself. Each time is the median of nine rounds of at least 25 ms, after a warm-up, without the cost of the loop. This table shows no allocations. The first page is loaded twice, and its first result dropped, because the first page of a fresh browser ran before Chrome had optimized the WebAssembly.

With Mono's interpreter:

| Text, user's culture                  | IStringLocalizer | Idioma set | Inline | Attribute |
|---------------------------------------|------------------|------------|--------|-----------|
| Plain text, default language          | 723 ns           | 0.09×      | 0.10×  | 0.15×     |
| Plain text, second language           | 721 ns           | 0.08×      | 0.10×  | 0.15×     |
| Plain text, region                    | 2,030 ns         | 0.03×      | 0.03×  | 0.05×     |
| Reworded text, region                 | 712 ns           | 0.08×      | 0.10×  | 0.16×     |
| Plain text, fallback                  | 715 ns           | 0.09×      | 0.10×  | 0.16×     |
| Text with parameter, default language | 1,164 ns         | 0.13×      | 0.29×  | –         |
| Text with parameter, second language  | 1,158 ns         | 0.13×      | 0.29×  | –         |
| Text with parameter, region           | 2,588 ns         | 0.06×      | 0.13×  | –         |
| Text with parameter, fallback         | 1,156 ns         | 0.79×      | 0.30×  | –         |

### A language that falls back

The "fallback" rows of both tables are a German user of an app in English and Dutch, who sees English.

A plain text costs a German user about the same as an English one. A text with a parameter takes a set about eight times as long on .NET, and about six times as long with the interpreter. That's the row where a set comes closest to `IStringLocalizer`. It takes about seven tenths of its time on .NET, and four fifths with the interpreter. [What the numbers mean](#what-the-numbers-mean) explains why.

### What the numbers mean

- **Plain text.** A set takes under a tenth of the time `IStringLocalizer` takes, and allocates nothing. Idioma keeps, per culture, the set that words each text. So showing a text is a read of that table and a call of the set's own property.
- **Region.** A British user's text costs a set the same as any other, whether it comes from the British set or from English. Idioma works out once which set words a text for a culture, from what the build recorded. `IStringLocalizer` looks for British wording first on every call, so a text that comes from English takes it about 150 ns and 168 bytes on .NET.
- **Text with parameter.** A set hands the values to the method of the set for the user's language. That takes about 6 ns on .NET, and allocates only the string the method makes, 48 bytes here. `IStringLocalizer` takes about 70 ns and allocates 152 bytes.
- **Inline.** A plain inline text picks one of the wordings it's called with. It takes about 4.6 ns on .NET and 73 ns with the interpreter, and allocates nothing.
- **Inline text with parameter.** C# builds the wording of every language before `Inline` picks one. With three languages, it takes about 17 ns and allocates 144 bytes, three strings of 48. Its cost grows with the number of languages. For a text you show often, or one with a costly value, write a method of a set, which builds one wording.
- **Attribute.** Idioma finds a text written as an attribute by its type and the name of its member each time it's asked for. It compares the name as an object first, and `nameof(Address.City)` is the same string on every call. It takes about 6.5 ns on .NET and 112 ns with the interpreter, and allocates nothing.
- **A language that falls back, and a culture provider.** A text with a parameter runs its set's method in the culture of the language it words. That switches the thread's culture for the call where it isn't that culture already:
  - for a user whose language falls back to another, such as a German user of an app in English and Dutch
  - with `CurrentUICultureProvider` set to a culture the thread doesn't have

  The first switch in an execution context, such as the first of a request, copies the context. The next ones reuse that copy. Idioma keeps the set that words the text, with the culture to switch to, so a later call doesn't look the set up again. For a German user, such a call takes about eight times as long as for an English one ([above](#a-language-that-falls-back)). It allocates only its wording. A text without parameters switches nothing, so it costs the same for every user.

## Labels and enum values

The Attribute column finds a text by its type and the name of its member. Code can also find it by a property it holds, or ask for the text of a type or of a value of an enum.

Here Idioma is compared with `[Display]`, the attribute .NET itself has for the label of a property. `[Display]` isn't part of Idioma. It's the alternative, as `IStringLocalizer` is for the texts above, and it reads its wording from the same resource file. .NET reads an attribute through reflection, so a framework that shows forms reads `[Display]` once and keeps it. Both are measured.

Each row finds one label and shows it once, for a Dutch user, on .NET:

| Approach           | What's measured                                        | Time   | Allocated |
|--------------------|--------------------------------------------------------|--------|-----------|
| .NET's `[Display]` | Its attribute read once and kept                       | 35 ns  | 0 B       |
| .NET's `[Display]` | Its attribute read from the property each time         | 785 ns | 760 B     |
| Idioma             | `GetTextByMember<Order>(nameof(Order.Checkout))`       | 6.3 ns | 0 B       |
| Idioma             | `GetTextByMember(property)`, by a `PropertyInfo`       | 9.6 ns | 0 B       |
| Idioma             | `GetTextOfType<Order>()`                               | 5.1 ns | 0 B       |
| Idioma             | `status.GetText()`, an enum with `[Localized]` members | 4.7 ns | 0 B       |
| Idioma             | `status.GetText()`, a set of the enum                  | 3.7 ns | 0 B       |

What the numbers mean:

- **`[Display]`.** With its attribute kept, a label takes 35 ns, most of it the lookup in the resource file. Read from the property each time, it takes about 0.8 µs and allocates 760 bytes, because .NET creates the attribute anew on every read.
- **Idioma, a property.** A label takes 6 to 10 ns, a sixth to a quarter of what `[Display]` takes with its attribute kept. It allocates nothing, and the caller has nothing to keep.
- **Idioma, a type.** The text of a type takes 5.1 ns.
- **Idioma, a value of an enum.** `GetText()` chooses the text in a `switch`. It takes 4.7 ns for an enum with `[Localized]` members, and 3.7 ns for a set of the enum, as a text of a set does.

These rows weren't measured in Blazor WebAssembly.

## Download size in Blazor WebAssembly

Each approach is measured against the same app without localization, which shows the same 100 shop texts as English strings. So an approach's size is everything it adds to that app:

- its own assemblies
- its texts and the code generated for them, in the app's assembly
- what it keeps of .NET's own assemblies, which Blazor trims out of an app that doesn't use them

For `IStringLocalizer`, that includes its resx files and the class resx generates. The sizes are on Mono with the interpreter, compressed with Brotli as a server sends them:

| What's downloaded                                                        |    Size |
|--------------------------------------------------------------------------|--------:|
| resx and `IStringLocalizer`, with 100 texts in the default language      | 19.3 KB |
| resx: each extra language of 100 texts, as a satellite assembly          |  3.8 KB |
| Idioma, with 100 texts in the default language                           | 32.4 KB |
| Idioma: each extra language of 100 texts, as a set in the app's assembly |  1.6 KB |

Where the first and the third row come from:

| Part                                                        | resx and `IStringLocalizer` |  Idioma |
|-------------------------------------------------------------|----------------------------:|--------:|
| Its own assemblies                                          |                     11.3 KB | 16.9 KB |
| The app's assembly, with the texts and their generated code |                      4.1 KB |  9.3 KB |
| .NET's own assemblies                                       |                      3.9 KB |  6.1 KB |
| The rest, such as `dotnet.js`                               |                      0.1 KB |  0.1 KB |

What to know:

- What Idioma keeps of .NET is mostly `System.Collections.Immutable` (2.5 KB), `System.Threading` (1.7 KB), `System.Private.CoreLib` (0.9 KB) and `System.Linq` (0.8 KB). Smaller files make up the last 0.2 KB. An app that uses them itself downloads them anyway, so Idioma costs it less. resx keeps 2.4 KB of `System.Private.CoreLib`, for `ResourceManager`.
- The app's assembly holds each approach's texts in the default language, and the code generated for them. For resx, that's its resources and the class resx generates. The class stays even though `IStringLocalizer` never calls it, because Blazor doesn't trim the app's own assembly by default (`TrimMode` is `partial`). For Idioma, it's the sets, and the contract, the generated class and the registrar that the source generator writes.
- A user of resx downloads only the satellite assembly of their own language. A user of the default language downloads none. So resx and `IStringLocalizer` cost a user 19.3 KB, or 23.1 KB for a user whose language isn't the default.
- Idioma compiles the set of every language into the app's assembly, so every user downloads all of them. So an app in three languages costs every user 32.4 KB plus 2 × 1.6 KB, which is 35.6 KB.
- A set in an assembly of its own, which the app loads lazily, doesn't pay off for 100 texts. An app in two languages then costs a user of the default language 47.9 KB and a Dutch user 52.9 KB, where both sets in the app's assembly cost every user 34.1 KB.
- The sizes with AOT weren't measured again. AOT also turns code into native code in `dotnet.native.wasm`, so it adds more than the interpreter does.
- The sizes come from an app made to measure them. It's published without localization, and with each approach, once with English only and once with Dutch too.

## Run it yourself

The benchmarks are in [`benchmarks`](../benchmarks). One script runs them all, and writes the tables to `benchmarks/results`:

```shell
./benchmarks/run.sh
```

What to know:

- It takes about 30 minutes.
- It needs Chrome. Set `CHROME` to its path when it's installed somewhere else.
- The AOT steps need the `wasm-tools` workload: `dotnet workload install wasm-tools`. Without it, the script skips those steps.