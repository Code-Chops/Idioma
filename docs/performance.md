# Performance

Idioma shows a text in less time than `IStringLocalizer`, which is the approach Microsoft recommends for localizing an app. This page compares how long a text takes to show with each, on .NET and in Blazor WebAssembly. It also compares what each adds to a Blazor WebAssembly download, where `IStringLocalizer` comes with its resx files.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## In short

- On .NET, a plain text of a set takes about 3.4 ns, where `IStringLocalizer` takes about 45 ns. It allocates nothing, where `IStringLocalizer` allocates 72 bytes.
- In Blazor WebAssembly with Mono's interpreter, the same takes a set about 60 ns, where `IStringLocalizer` takes about 680 ns.
- A British user's text costs a set the same as any other. `IStringLocalizer` takes three times as long for a text that comes from English, because it looks for British wording first.
- A text with a parameter takes a set about 6 ns on .NET, a tenth of what `IStringLocalizer` takes. It allocates only the string it returns.
- For a user whose language falls back, such as a German user of an app in English and Dutch, a text with a parameter takes more than ten times as long, and allocates about 90 bytes more. Its method runs in the culture of the language it falls back to, so the culture is switched for the call.
- An inline text takes about 3.9 ns, and a text written as an attribute about 5.7 ns. Both allocate nothing.
- An inline text with a parameter builds its wording in every language on each call. It takes about 16 ns with three languages, a quarter of what `IStringLocalizer` takes.
- Idioma adds more to a Blazor WebAssembly download than resx and `IStringLocalizer` do. With 100 texts and the interpreter, Idioma adds 30.1 KB, compressed, plus 1.8 KB for each extra language. resx and `IStringLocalizer` add 19.3 KB, plus 3.9 KB for a user whose language isn't the default.

So speed rarely decides between them. A page that shows 200 texts spends about 9 µs on them with `IStringLocalizer` on .NET, and under 1 µs with Idioma's sets. With Mono's interpreter in WebAssembly, it spends about 0.14 ms with `IStringLocalizer`, and about 0.01 ms with Idioma's sets.

## What was measured

Three texts: `Checkout` (plain), `Cart` (British English rewords it as "Basket"), and `Greeting(name)` (with a parameter).

Four approaches show them:

- **IStringLocalizer**: `localizer["Checkout"]` on an `IStringLocalizer<Shop>`, from resx files (the approach Microsoft recommends). The resx files also get the class resx generates, as an app gets it from its IDE, and `IStringLocalizer<Shop>` is named after it.
- **Idioma set**: `ShopLocalization.Checkout`.
- **Inline**: `LocalizableText.Inline(...)`, called each time the text is shown, as markup calls it on every render.
- **Attribute**: `LocalizableText.GetTextByMember<ShopLabels>(nameof(ShopLabels.Checkout))`, for a property marked `[Localized]`. The text is found by its type and the name of its member each time, as a form finds the label of a field. An attribute takes no value, so it has no text with a parameter.

The app declares `("en", "nl", "en-GB")`. "Region" is a British user.

A user whose language the app doesn't have, such as a German user, sees English. Their texts were measured on another machine, so they have [a table of their own](#a-language-that-falls-back). A Blazor app can make its default language their UI culture, as [Blazor](blazor.md#a-browser-language-the-app-doesnt-have) shows. Their texts then take as long as for a user of the default language.

Each benchmark first checks that every approach shows the expected wording, so no approach is timed doing less work.

Apart from that table, the numbers come from one machine, an AMD Ryzen AI 9 HX PRO 370 on Windows 11. They ran on .NET 11.0.0 with BenchmarkDotNet 0.16.0-preview.2, and in Blazor WebAssembly on .NET 11.0.0 in a headless Chrome 157. [Its tables](../benchmarks/results/second-machine) are kept. Compare the approaches with each other rather than with your own numbers, because those depend on the machine.

What wasn't measured again since a text became a `string`:

- Mono's AOT in Blazor WebAssembly, which needs the `wasm-tools` workload.
- A second machine. The tables directly in [`benchmarks/results`](../benchmarks/results) are from an Apple M2 Max and from before that change, so they describe the earlier design.

An earlier, shorter run on the same machine gave the plain texts within 2% of these times.

## Showing a text

Each benchmark shows one text once, as a render does. The `IStringLocalizer` column shows how long that takes. The other columns show their time as a factor of it, so 0.50× takes half as long. On .NET, a cell also shows the bytes allocated each time a text is shown.

### On .NET

| Text, user's culture                  | IStringLocalizer | Idioma set  | Inline       | Attribute  |
|---------------------------------------|------------------|-------------|--------------|------------|
| Plain text, default language          | 46 ns, 72 B      | 0.08×, 0 B  | 0.09×, 0 B   | 0.12×, 0 B |
| Plain text, second language           | 46 ns, 72 B      | 0.07×, 0 B  | 0.09×, 0 B   | 0.12×, 0 B |
| Plain text, region                    | 140 ns, 168 B    | 0.02×, 0 B  | 0.03×, 0 B   | 0.04×, 0 B |
| Reworded text, region                 | 44 ns, 72 B      | 0.08×, 0 B  | 0.09×, 0 B   | 0.13×, 0 B |
| Text with parameter, default language | 66 ns, 152 B     | 0.09×, 48 B | 0.24×, 144 B | –          |
| Text with parameter, second language  | 68 ns, 152 B     | 0.09×, 48 B | 0.23×, 144 B | –          |
| Text with parameter, region           | 163 ns, 248 B    | 0.04×, 48 B | 0.10×, 144 B | –          |

### In Blazor WebAssembly

Blazor WebAssembly runs .NET code on the Mono runtime by default, with an interpreter. Publishing with `RunAOTCompilation` compiles it to WebAssembly ahead of time (AOT) instead, which makes the download bigger. Only the interpreter was measured.

The browser has no BenchmarkDotNet, so the app times each text itself. Each time is the median of nine rounds of at least 25 ms, after a warm-up, without the cost of the loop. This table shows no allocations. The first page is loaded twice, and its first result dropped, because the first page of a fresh browser ran before Chrome had optimized the WebAssembly.

With Mono's interpreter:

| Text, user's culture                  | IStringLocalizer | Idioma set | Inline | Attribute |
|---------------------------------------|------------------|------------|--------|-----------|
| Plain text, default language          | 678 ns           | 0.09×      | 0.11×  | 0.16×     |
| Plain text, second language           | 697 ns           | 0.09×      | 0.11×  | 0.16×     |
| Plain text, region                    | 1,970 ns         | 0.03×      | 0.04×  | 0.06×     |
| Reworded text, region                 | 686 ns           | 0.08×      | 0.11×  | 0.16×     |
| Text with parameter, default language | 1,112 ns         | 0.13×      | 0.31×  | –         |
| Text with parameter, second language  | 1,125 ns         | 0.13×      | 0.30×  | –         |
| Text with parameter, region           | 2,490 ns         | 0.06×      | 0.14×  | –         |

### A language that falls back

A German user of an app in English and Dutch sees English. These rows come from another machine, an Apple M2 Max on macOS. They ran on the same .NET and BenchmarkDotNet, and with Mono's interpreter in a headless Chrome 158. So compare them with the default language of the same run, shown next to them, rather than with the tables above.

On .NET:

| Text, user's culture                  | IStringLocalizer | Idioma set   | Inline       | Attribute  |
|---------------------------------------|------------------|--------------|--------------|------------|
| Plain text, default language          | 43 ns, 72 B      | 0.11×, 0 B   | 0.18×, 0 B   | 0.18×, 0 B |
| Plain text, German                    | 45 ns, 72 B      | 0.13×, 0 B   | 0.18×, 0 B   | 0.17×, 0 B |
| Text with parameter, default language | 77 ns, 152 B     | 0.12×, 48 B  | 0.32×, 144 B | –          |
| Text with parameter, German           | 80 ns, 152 B     | 1.47×, 136 B | 0.33×, 144 B | –          |

With Mono's interpreter:

| Text, user's culture                  | IStringLocalizer | Idioma set | Inline | Attribute |
|---------------------------------------|------------------|------------|--------|-----------|
| Plain text, default language          | 960 ns           | 0.07×      | 0.10×  | 0.16×     |
| Plain text, German                    | 986 ns           | 0.07×      | 0.10×  | 0.16×     |
| Text with parameter, default language | 1,551 ns         | 0.14×      | 0.28×  | –         |
| Text with parameter, German           | 1,470 ns         | 1.29×      | 0.29×  | –         |

A plain text costs a German user about the same as an English one. A text with a parameter takes a set about 12 times as long on .NET, and 9 times as long with the interpreter. It's the one row where a set takes longer than `IStringLocalizer`. It takes 1.5 times as long on .NET, and 1.3 times as long with the interpreter. [What the numbers mean](#what-the-numbers-mean) explains why.

### What the numbers mean

- **Plain text.** A set takes under a tenth of the time `IStringLocalizer` takes, and allocates nothing. Idioma keeps, per culture, the set that words each text. So showing a text is a read of that table and a call of the set's own property.
- **Region.** A British user's text costs a set the same as any other, whether it comes from the British set or from English. Idioma works out once which set words a text for a culture, from what the build recorded. `IStringLocalizer` looks for British wording first on every call, so a text that comes from English takes it 140 ns and 168 bytes on .NET.
- **Text with parameter.** A set hands the values to the method of the set for the user's language. That takes about 6 ns on .NET, and allocates only the string the method makes, 48 bytes here. `IStringLocalizer` takes 66 ns and allocates 152 bytes.
- **Inline.** A plain inline text picks one of the wordings it's called with. It takes about 3.9 ns on .NET and 75 ns with the interpreter, and allocates nothing.
- **Inline text with parameter.** C# builds the wording of every language before `Inline` picks one. With three languages, it takes about 16 ns and allocates 144 bytes, three strings of 48. Its cost grows with the number of languages. For a text you show often, or one with a costly value, write a method of a set, which builds one wording.
- **Attribute.** Idioma finds a text written as an attribute by its type and the name of its member each time it's asked for. It compares the name as an object first, and `nameof(Address.City)` is the same string on every call. It takes about 5.7 ns on .NET and 110 ns with the interpreter, and allocates nothing.
- **A language that falls back, and a culture provider.** A text with a parameter runs its set's method in the culture of the language it words. That switches the thread's culture for the call where it isn't that culture already:
  - for a user whose language falls back to another, such as a German user of an app in English and Dutch
  - with `CurrentUICultureProvider` set to a culture the thread doesn't have

  The first switch in an execution context, such as the first of a request, copies the context. The next ones reuse that copy. For a German user, such a call takes about 12 times as long as for an English one ([above](#a-language-that-falls-back)). It allocates 88 bytes more than its wording, for the function that hands the set its values. A text without parameters switches nothing, so it costs the same for every user.

## Labels and enum values

The Attribute column finds a text by its type and the name of its member. Code can also find it by a property it holds, or ask for the text of a type or of a value of an enum.

Here Idioma is compared with `[Display]`, the attribute .NET itself has for the label of a property. `[Display]` isn't part of Idioma. It's the alternative, as `IStringLocalizer` is for the texts above, and it reads its wording from the same resource file. .NET reads an attribute through reflection, so a framework that shows forms reads `[Display]` once and keeps it. Both are measured.

Each row finds one label and shows it once, for a Dutch user, on .NET:

| Approach           | What's measured                                        | Time   | Allocated |
|--------------------|--------------------------------------------------------|--------|-----------|
| .NET's `[Display]` | Its attribute read once and kept                       | 35 ns  | 0 B       |
| .NET's `[Display]` | Its attribute read from the property each time         | 785 ns | 760 B     |
| Idioma             | `GetTextByMember<Order>(nameof(Order.Checkout))`       | 5.7 ns | 0 B       |
| Idioma             | `GetTextByMember(property)`, by a `PropertyInfo`       | 9.0 ns | 0 B       |
| Idioma             | `GetTextOfType<Order>()`                               | 4.5 ns | 0 B       |
| Idioma             | `status.GetText()`, an enum with `[Localized]` members | 4.2 ns | 0 B       |
| Idioma             | `status.GetText()`, a set of the enum                  | 3.6 ns | 0 B       |

What the numbers mean:

- **`[Display]`.** With its attribute kept, a label takes 35 ns, most of it the lookup in the resource file. Read from the property each time, it takes about 0.8 µs and allocates 760 bytes, because .NET creates the attribute anew on every read.
- **Idioma, a property.** A label takes 6 to 9 ns, a sixth to a quarter of what `[Display]` takes with its attribute kept. It allocates nothing, and the caller has nothing to keep.
- **Idioma, a type.** The text of a type takes 4.5 ns.
- **Idioma, a value of an enum.** `GetText()` chooses the text in a `switch`. It takes 4.2 ns for an enum with `[Localized]` members, and 3.6 ns for a set of the enum, as a text of a set does.

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
| resx: each extra language of 100 texts, as a satellite assembly          |  3.9 KB |
| Idioma, with 100 texts in the default language                           | 30.1 KB |
| Idioma: each extra language of 100 texts, as a set in the app's assembly |  1.8 KB |

Where the first and the third row come from:

| Part                                                        | resx and `IStringLocalizer` |  Idioma |
|-------------------------------------------------------------|----------------------------:|--------:|
| Its own assemblies                                          |                     11.3 KB | 16.4 KB |
| The app's assembly, with the texts and their generated code |                      4.0 KB |  8.5 KB |
| .NET's own assemblies                                       |                      3.9 KB |  5.0 KB |
| The rest, such as `dotnet.js`                               |                      0.1 KB |  0.1 KB |

What to know:

- What Idioma keeps of .NET is `System.Collections.Immutable` (1.9 KB), `System.Threading` (1.7 KB), `System.Linq` (0.8 KB) and `System.Private.CoreLib` (0.4 KB). An app that uses them itself downloads them anyway, so Idioma costs it less. resx keeps 2.4 KB of `System.Private.CoreLib`, for `ResourceManager`.
- The app's assembly holds each approach's texts in the default language, and the code generated for them. For resx, that's its resources and the class resx generates. The class stays even though `IStringLocalizer` never calls it, because Blazor doesn't trim the app's own assembly by default (`TrimMode` is `partial`). For Idioma, it's the sets, and the contract, the generated class and the registrar that the source generator writes.
- A user of resx downloads only the satellite assembly of their own language. A user of the default language downloads none. So resx and `IStringLocalizer` cost a user 19.3 KB, or 23.2 KB for a user whose language isn't the default.
- Idioma compiles the set of every language into the app's assembly, so every user downloads all of them. So an app in three languages costs every user 30.1 KB plus 2 × 1.8 KB, which is 33.7 KB.
- A set in an assembly of its own, which the app loads lazily, doesn't pay off for 100 texts. An app in two languages then costs a user of the default language 47.4 KB and a Dutch user 52.4 KB, where both sets in the app's assembly cost every user 31.9 KB.
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