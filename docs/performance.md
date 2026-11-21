# Performance

Idioma shows most texts in less time than `IStringLocalizer`, which is the approach Microsoft recommends for localizing an app. This page compares how long a text takes to show with each, on .NET and in Blazor WebAssembly. It also compares what each adds to a Blazor WebAssembly download, where `IStringLocalizer` comes with its resx files.

Other subjects have a page of their own: [language codes](language-codes.md), [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), [what the build rejects](build-errors.md), [coming from resx](coming-from-resx.md), and [advanced topics](advanced-topics.md).

## In short

- On .NET, a plain text of a set takes about 16.5 ns for a user of the default or the second language, where `IStringLocalizer` takes about 43 ns. It allocates nothing, where `IStringLocalizer` allocates 72 bytes.
- In Blazor WebAssembly, the same takes a set about 0.4 µs with Mono's interpreter, and about 0.1 µs with AOT. That's less than half of what `IStringLocalizer` takes.
- For a British user's text that comes from English, a set takes a quarter to a third of the time `IStringLocalizer` takes.
- A set takes longer than `IStringLocalizer` only for a text with a parameter, in Blazor WebAssembly with AOT. It takes about 1.1 times as long there, and about seven tenths for a British user.
- A plain inline text kept in a field takes a seventh to a third of the time `IStringLocalizer` takes. Written in markup, which calls it on every render, it takes a quarter to under half, because Idioma keeps the text the first render created. For a British user's text that comes from English, both take an eighth or less.
- A plain text written as an attribute is found by its member before it's shown. It takes about a third of the time `IStringLocalizer` takes on .NET, and about a quarter in Blazor WebAssembly. It allocates nothing.
- Idioma adds more to a Blazor WebAssembly download than resx and `IStringLocalizer` do. With 100 texts and the interpreter, Idioma adds 46.5 KB, compressed, plus 1.7 KB for each extra language. resx and `IStringLocalizer` add 17.9 KB, plus 3.8 KB for a user whose language isn't the default. With AOT, Idioma also adds about 92 KB of native code, where resx and `IStringLocalizer` add about 20 KB.

So speed rarely decides between them. A page that shows 200 texts spends about 8.7 µs on them with `IStringLocalizer` on .NET, and about 3.3 µs with Idioma's sets. With Mono's interpreter in WebAssembly, it spends about 0.20 ms with `IStringLocalizer`, and about 0.08 ms with Idioma's sets.

## What was measured

Three texts: `Checkout` (plain), `Cart` (British English rewords it as "Basket"), and `Greeting(name)` (with a parameter).

Five approaches show them:

- **IStringLocalizer**: `localizer["Checkout"]` on an `IStringLocalizer<Shop>`, from resx files (the approach Microsoft recommends). The resx files also get the class resx generates, as an app gets it from its IDE, and `IStringLocalizer<Shop>` is named after it.
- **Idioma set**: `ShopLocalization.Checkout`.
- **Inline, in a field**: `LocalizableText.Inline(...)`, created once and kept. So its text with a parameter is already worded, and isn't comparable with the other columns.
- **Inline, in markup**: the same call on every render.
- **Attribute**: `LocalizableText.GetTextByMember<ShopLabels>(nameof(ShopLabels.Checkout))`, for a property marked `[Localized]`. The text is found by its type and the name of its member on every render, as a form finds the label of a field. An attribute takes no value, so it has no text with a parameter.

The app declares `("en", "nl", "en-GB")`. "Region" is a British user.

A user whose language the app doesn't have, such as a German user, has no row. The app makes its default language their UI culture, as [Blazor](blazor.md#a-browser-language-the-app-doesnt-have) shows, so their texts take as long as for a user of the default language.

Each benchmark first checks that every approach shows the expected wording, so no approach is timed doing less work.

The numbers come from one machine, an Apple M2 Max on macOS 26.6. They ran on .NET 11.0.0 with BenchmarkDotNet 0.16.0-preview.2, and in Blazor WebAssembly on .NET 11.0.0 in a headless Chrome 158. Compare the approaches with each other rather than with your own numbers, because those depend on the machine.

On most rows, two runs of the same code differed by less than 5%. A few differed by up to 13%. So a gap of about 10% or less between two approaches may be noise. One row differed more: in some runs, the inline text in a field for the second language took 18 ns on .NET, where it took 12 to 14 ns in the others.

## Showing a text

Each benchmark shows one text once, as a render does. The `IStringLocalizer` column shows how long that takes. The other columns show their time as a factor of it, so 0.50× takes half as long. On .NET, a cell also shows the bytes allocated each time a text is shown.

### On .NET

| Text, user's culture                  | IStringLocalizer | Idioma set   | Inline, in a field | Inline, in markup | Attribute  |
|---------------------------------------|------------------|--------------|--------------------|-------------------|------------|
| Plain text, default language          | 43 ns, 72 B      | 0.38×, 0 B   | 0.27×, 0 B         | 0.35×, 0 B        | 0.31×, 0 B |
| Plain text, second language           | 44 ns, 72 B      | 0.38×, 0 B   | 0.32×, 0 B         | 0.40×, 0 B        | 0.35×, 0 B |
| Plain text, region                    | 136 ns, 168 B    | 0.25×, 0 B   | 0.10×, 0 B         | 0.13×, 0 B        | 0.11×, 0 B |
| Reworded text, region                 | 39 ns, 72 B      | 0.79×, 0 B   | 0.35×, 0 B         | 0.45×, 0 B        | 0.40×, 0 B |
| Text with parameter, default language | 77 ns, 152 B     | 0.72×, 168 B | 0.15×, 0 B         | 0.67×, 264 B      | –          |
| Text with parameter, second language  | 79 ns, 152 B     | 0.71×, 168 B | 0.18×, 0 B         | 0.69×, 264 B      | –          |
| Text with parameter, region           | 173 ns, 248 B    | 0.43×, 168 B | 0.08×, 0 B         | 0.31×, 264 B      | –          |

### In Blazor WebAssembly

Blazor WebAssembly runs .NET code on the Mono runtime by default, with an interpreter. Publishing with `RunAOTCompilation` compiles it to WebAssembly ahead of time (AOT) instead, which makes the download bigger.

The browser has no BenchmarkDotNet, so the app times each text itself. Each time is the median of nine rounds of at least 25 ms, after a warm-up, without the cost of the loop. These tables show no allocations. Each table's first page is loaded twice, and its first result dropped, because the first page of a fresh browser ran before Chrome had optimized the WebAssembly.

With Mono's interpreter:

| Text, user's culture                  | IStringLocalizer | Idioma set | Inline, in a field | Inline, in markup | Attribute |
|---------------------------------------|------------------|------------|--------------------|-------------------|-----------|
| Plain text, default language          | 979 ns           | 0.41×      | 0.20×              | 0.33×             | 0.27×     |
| Plain text, second language           | 995 ns           | 0.40×      | 0.23×              | 0.36×             | 0.30×     |
| Plain text, region                    | 2,724 ns         | 0.33×      | 0.07×              | 0.13×             | 0.10×     |
| Reworded text, region                 | 968 ns           | 0.90×      | 0.21×              | 0.36×             | 0.29×     |
| Text with parameter, default language | 1,546 ns         | 0.63×      | 0.13×              | 0.55×             | –         |
| Text with parameter, second language  | 1,574 ns         | 0.63×      | 0.14×              | 0.56×             | –         |
| Text with parameter, region           | 3,319 ns         | 0.47×      | 0.06×              | 0.26×             | –         |

With Mono's AOT:

| Text, user's culture                  | IStringLocalizer | Idioma set | Inline, in a field | Inline, in markup | Attribute |
|---------------------------------------|------------------|------------|--------------------|-------------------|-----------|
| Plain text, default language          | 266 ns           | 0.38×      | 0.14×              | 0.26×             | 0.21×     |
| Plain text, second language           | 280 ns           | 0.37×      | 0.16×              | 0.28×             | 0.23×     |
| Plain text, region                    | 696 ns           | 0.28×      | 0.07×              | 0.12×             | 0.10×     |
| Reworded text, region                 | 271 ns           | 0.72×      | 0.19×              | 0.31×             | 0.25×     |
| Text with parameter, default language | 446 ns           | 1.12×      | 0.09×              | 0.64×             | –         |
| Text with parameter, second language  | 458 ns           | 1.08×      | 0.10×              | 0.63×             | –         |
| Text with parameter, region           | 869 ns           | 0.71×      | 0.06×              | 0.33×             | –         |

### What the numbers mean

- **Plain text, default and second language.** A set takes about two fifths of the time `IStringLocalizer` takes, and allocates nothing. The registry works out once per culture which of its sets serve it, so each text looks only in those.
- **Plain text, region.** A British user's text comes from English. `IStringLocalizer` looks for British wording first, so it's at its slowest there. On .NET, it takes 136 ns and allocates 168 bytes each time a text is shown. A set takes a quarter to a third of that, and allocates nothing. An inline text takes an eighth or less of it, in a field or in markup.
- **Reworded text, region.** A set takes three quarters to four fifths of the time `IStringLocalizer` takes on .NET and with AOT, and nine tenths with Mono's interpreter. Idioma checks which texts the British set words itself, as the build recorded them, so it reads only the wording it shows.
- **Text with parameter.** A set creates the text on each call, and the set's method words it. On .NET and with Mono's interpreter, that takes 30 to 40% less time than `IStringLocalizer`. With AOT, it takes about 1.1 times as long. A set allocates 168 bytes, where `IStringLocalizer` allocates 152. For a British user, a set takes under half the time `IStringLocalizer` takes on .NET and with the interpreter, and about seven tenths with AOT.
- **Inline, in a field.** An inline text kept in a field takes a seventh to a third of the time `IStringLocalizer` takes, and a tenth or less for a British user's text that comes from English. It allocates nothing. Its wording only has to be found for the user's culture.
- **Inline, in markup.** Markup calls an inline text on every render. Idioma keeps the plain inline texts it creates, up to 1,024 of them, each shorter than 3,900 characters. A string literal is the same object on every render, so a render finds the text an earlier render created by comparing the strings as objects. A plain inline text then takes a quarter to under half of the time `IStringLocalizer` takes, and an eighth for a British user's text that comes from English. It allocates nothing.
- **Inline text with parameter, in markup.** Its wording is a new string on every render, so Idioma creates the text again each time. It takes half to seven tenths of the time `IStringLocalizer` takes, and a quarter to a third for a British user. It allocates 264 bytes, where `IStringLocalizer` allocates 152. It words every language as it's created, so its cost grows with the number of languages. Idioma doesn't keep such a text, because no later render could find it. So a value written into the wording, such as a user's name, doesn't stay in memory after its text is gone. There's one exception. Wording that your code built earlier and that outlived a garbage collection before the call is kept like a literal, in one of the 1,024 places.
- **Attribute.** Generated code keeps a text written as an attribute, and Idioma finds it by its type and the name of its member each time it's asked for. It compares the name as an object first, and `nameof(Address.City)` is the same string on every call. So the lookup adds little to what an inline text in a field takes: under 2 ns on .NET, 70 ns with Mono's interpreter, and 20 ns with AOT. It takes about a third of the time `IStringLocalizer` takes on .NET, and about a quarter in Blazor WebAssembly. It allocates nothing.

## Labels and enum values

The Attribute column finds a text by its type and the name of its member. Code can also find it by a property it holds, or ask for the text of a type or of a value of an enum.

Here Idioma is compared with `[Display]`, the attribute .NET itself has for the label of a property. `[Display]` isn't part of Idioma. It's the alternative, as `IStringLocalizer` is for the texts above, and it reads its wording from the same resource file. .NET reads an attribute through reflection, so a framework that shows forms reads `[Display]` once and keeps it. Both are measured.

Each row finds one label and shows it once, for a Dutch user, on .NET:

| Approach           | What's measured                                        | Time   | Allocated |
|--------------------|--------------------------------------------------------|--------|-----------|
| .NET's `[Display]` | Its attribute read once and kept                       | 28 ns  | 0 B       |
| .NET's `[Display]` | Its attribute read from the property each time         | 772 ns | 760 B     |
| Idioma             | `GetTextByMember<Order>(nameof(Order.Checkout))`       | 17 ns  | 0 B       |
| Idioma             | `GetTextByMember(property)`, by a `PropertyInfo`       | 20 ns  | 0 B       |
| Idioma             | `GetTextOfType<Order>()`                               | 15 ns  | 0 B       |
| Idioma             | `status.GetText()`, an enum with `[Localized]` members | 13 ns  | 0 B       |
| Idioma             | `status.GetText()`, a set of the enum                  | 17 ns  | 0 B       |

What the numbers mean:

- **`[Display]`.** With its attribute kept, a label takes 28 ns, most of it the lookup in the resource file. Read from the property each time, it takes about 0.8 µs and allocates 760 bytes, because .NET creates the attribute anew on every read.
- **Idioma, a property.** A label takes 17 to 20 ns, about two thirds of what `[Display]` takes with its attribute kept. It allocates nothing, and the caller has nothing to keep.
- **Idioma, a type.** The text of a type takes 15 ns.
- **Idioma, a value of an enum.** `GetText()` chooses the text in a `switch`. It takes 13 ns for an enum with `[Localized]` members, which is what showing an inline text takes. It takes 17 ns for a set of the enum, as a text of a set does.

These rows weren't measured in Blazor WebAssembly.

## Download size in Blazor WebAssembly

Each approach is measured against the same app without localization, which shows the same 100 shop texts as English strings. So an approach's size is everything it adds to that app:

- its own assemblies
- its texts and the code generated for them, in the app's assembly
- what it keeps of .NET's own assemblies, which Blazor trims out of an app that doesn't use them

For `IStringLocalizer`, that includes its resx files and the class resx generates. The sizes are on Mono, compressed with Brotli as a server sends them:

| What's downloaded                                                        | Interpreter |     AOT |
|--------------------------------------------------------------------------|------------:|--------:|
| resx and `IStringLocalizer`, with 100 texts in the default language      |     17.9 KB | 15.3 KB |
| resx: each extra language of 100 texts, as a satellite assembly          |      3.8 KB |  3.8 KB |
| Idioma, with 100 texts in the default language                           |     46.5 KB | 37.3 KB |
| Idioma: each extra language of 100 texts, as a set in the app's assembly |      1.7 KB |  1.8 KB |

Where the first and the third row come from, with the interpreter:

| Part                                                        | resx and `IStringLocalizer` |  Idioma |
|-------------------------------------------------------------|----------------------------:|--------:|
| Its own assemblies                                          |                     11.3 KB | 20.3 KB |
| The app's assembly, with the texts and their generated code |                      4.1 KB |  8.8 KB |
| .NET's own assemblies                                       |                      2.5 KB | 17.3 KB |
| The rest, such as `dotnet.js`                               |                      0.1 KB |  0.1 KB |

What to know:

- Most of what Idioma keeps of .NET is `System.Collections.Immutable`, at 11.6 KB. The rest is `System.Linq` (2.0 KB), `System.Threading` (1.7 KB), `System.Private.CoreLib` (1.1 KB) and `System.Text.Json` (0.8 KB). An app that uses them itself downloads them anyway, so Idioma costs it less. resx keeps 1.2 KB of `System.Private.CoreLib`, for `ResourceManager`.
- The app's assembly holds each approach's texts in the default language, and the code generated for them. For resx, that's its resources and the class resx generates. The class stays even though `IStringLocalizer` never calls it, because Blazor doesn't trim the app's own assembly by default (`TrimMode` is `partial`). For Idioma, it's the sets, and the contract, the generated class and the registrar that the source generator writes.
- A user of resx downloads only the satellite assembly of their own language. A user of the default language downloads none. So with the interpreter, resx and `IStringLocalizer` cost a user 17.9 KB, or 21.7 KB for a user whose language isn't the default.
- Idioma compiles the set of every language into the app's assembly, so every user downloads all of them. So with the interpreter, an app in three languages costs every user 46.5 KB plus 2 × 1.7 KB, which is 49.9 KB.
- With AOT, code also becomes native code in `dotnet.native.wasm`, which the tables leave out. Idioma adds 413 KB to that file before compression, and about 92 KB after. resx and `IStringLocalizer` add 76 KB before compression, and about 20 KB after. Each extra language adds 8.4 KB of Idioma's native code before compression.
- That file is 3.4 MB compressed, and Brotli's result for it moves by about 5 KB between publishes of the same code. So its compressed sizes are rough. With AOT, an app in two languages then costs every user about 130 KB with Idioma, and a Dutch user about 40 KB with resx and `IStringLocalizer`.
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
