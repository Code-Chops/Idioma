# Performance

Idioma shows most texts in less time than `IStringLocalizer`, which is the approach Microsoft recommends for localizing an app. This page compares how long a text takes to show with each, on .NET and in Blazor WebAssembly, and what each adds to a Blazor WebAssembly download.

Other subjects have a page of their own: [language codes](language-codes.md), [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), [what the build rejects](build-errors.md), [coming from resx](coming-from-resx.md), and [how Idioma works](how-it-works.md).

## In short

- On .NET, a plain text of a set takes about 16 ns for a user of the default or the second language, where `IStringLocalizer` takes about 42 ns. It allocates nothing, where `IStringLocalizer` allocates about 70 bytes.
- In Blazor WebAssembly, the same takes a set about 0.4 µs with Mono's interpreter, and about 0.1 µs with AOT. That's less than half of what `IStringLocalizer` takes.
- A plain inline text kept in a field takes a sixth to a third of the time `IStringLocalizer` takes. Written in markup, which calls it on every render, it takes a quarter to two fifths, because Idioma keeps the text the first render created.
- A plain text written as an attribute is found by its member before it's shown. It takes about a third of the time `IStringLocalizer` takes on .NET, and about a quarter in Blazor WebAssembly. It allocates nothing.
- A set takes longer than `IStringLocalizer` only for a text with parameter with WebAssembly AOT. It takes about 1.25 times as long there, and about as long for a British user.
- Idioma adds 19.4 KB to a Blazor WebAssembly download, compressed, plus 1.8 KB for each extra language of 100 texts. `IStringLocalizer` adds 11.3 KB, plus 3.6 KB for a user whose language isn't the default.

So speed rarely decides between them. A page that shows 200 texts spends about 8.5 µs on them with `IStringLocalizer` on .NET, and about 3 µs with Idioma's sets. With Mono's interpreter in WebAssembly, it spends about 0.2 ms with `IStringLocalizer`, and about 0.08 ms with Idioma's sets.

## What was measured

Three texts: `Checkout` (plain), `Cart` (British English rewords it as "Basket"), and `Greeting(name)` (with a parameter).

Five approaches show them:

- **IStringLocalizer**: `localizer["Checkout"]` on an `IStringLocalizer<Shop>`, from resx files (the alternative to this library).
- **Idioma set**: `ShopLocalization.Checkout`.
- **Inline, in a field**: `LocalizableText.Inline(...)`, created once and kept. So its text with parameter is already worded, and isn't comparable with the other columns.
- **Inline, in markup**: the same call on every render.
- **Attribute**: `LocalizableText.GetTextByMember<ShopLabels>(nameof(ShopLabels.Checkout))`, for a property marked `[Localized]`. The text is found by its type and the name of its member on every render, as a form finds the label of a field. An attribute takes no value, so it has no text with parameter.

The app declares `("en", "nl", "en-GB")`. "Region" is a British user.

A user whose language the app doesn't have, such as a German user, has no row. The app makes its default language their UI culture, as [Blazor](blazor.md#a-browser-language-the-app-doesnt-have) shows, so their texts take as long as for a user of the default language.

Each benchmark first checks that every approach shows the expected wording, so no approach is timed doing less work.

The numbers come from one machine, an Apple M2 Max on macOS 26.6. They ran on .NET 11.0.0 with BenchmarkDotNet 0.16.0-preview.2, and in Blazor WebAssembly on .NET 11.0.0 in a headless Chrome 158. Compare the approaches with each other rather than with your own numbers, because those depend on the machine.

On most rows, two runs of the same code differed by less than 5%. A few differed by up to 13%. So a gap of about 10% or less between two approaches may be noise. One row differed more: in some runs, the inline text in a field for the second language took 18 ns on .NET, where it took 12 to 14 ns in the others.

## Showing a text

Each benchmark shows one text once, as a render does. The `IStringLocalizer` column shows how long that takes. The other columns show their time as a factor of it, so 0.50× takes half as long. On .NET, a cell also shows the bytes allocated each time a text is shown.

### On .NET

| Text, user's culture                     | IStringLocalizer |   Idioma set | Inline, in a field | Inline, in markup |    Attribute |
|------------------------------------------|-----------------:|-------------:|-------------------:|------------------:|-------------:|
| Plain text, default language             |      42 ns, 72 B |   0.39×, 0 B |         0.28×, 0 B |        0.37×, 0 B |   0.32×, 0 B |
| Plain text, second language              |      43 ns, 72 B |   0.38×, 0 B |         0.33×, 0 B |        0.41×, 0 B |   0.37×, 0 B |
| Plain text, region                       |    137 ns, 168 B |   0.26×, 0 B |         0.10×, 0 B |        0.13×, 0 B |   0.11×, 0 B |
| Reworded text, region                    |      39 ns, 72 B |   0.75×, 0 B |         0.35×, 0 B |        0.45×, 0 B |   0.39×, 0 B |
| Text with parameter, default language    |     78 ns, 152 B | 0.72×, 168 B |         0.15×, 0 B |      0.68×, 264 B |            – |
| Text with parameter, second language     |     80 ns, 152 B | 0.69×, 168 B |         0.17×, 0 B |      0.68×, 264 B |            – |
| Text with parameter, region              |    183 ns, 248 B | 0.45×, 216 B |         0.07×, 0 B |      0.29×, 264 B |            – |

### In Blazor WebAssembly

Blazor WebAssembly runs .NET code on the Mono runtime by default, with an interpreter. Publishing with `RunAOTCompilation` compiles it to WebAssembly ahead of time (AOT) instead, which makes the download bigger.

The browser has no BenchmarkDotNet, so the app times each text itself. Each time is the median of nine rounds of at least 25 ms, after a warm-up, without the cost of the loop. These tables show no allocations. Each table's first page is loaded twice, and its first result dropped, because the first page of a fresh browser ran before Chrome had optimized the WebAssembly.

With Mono's interpreter:

| Text, user's culture                     | IStringLocalizer | Idioma set | Inline, in a field | Inline, in markup | Attribute |
|------------------------------------------|-----------------:|-----------:|-------------------:|------------------:|----------:|
| Plain text, default language             |           933 ns |      0.45× |              0.20× |             0.33× |     0.27× |
| Plain text, second language              |           945 ns |      0.44× |              0.23× |             0.36× |     0.30× |
| Plain text, region                       |         2,658 ns |      0.37× |              0.07× |             0.12× |     0.10× |
| Reworded text, region                    |           906 ns |      0.93× |              0.22× |             0.36× |     0.29× |
| Text with parameter, default language    |         1,547 ns |      0.65× |              0.12× |             0.54× |         – |
| Text with parameter, second language     |         1,547 ns |      0.62× |              0.14× |             0.53× |         – |
| Text with parameter, region              |         3,177 ns |      0.57× |              0.06× |             0.30× |         – |

With Mono's AOT:

| Text, user's culture                     | IStringLocalizer | Idioma set | Inline, in a field | Inline, in markup | Attribute |
|------------------------------------------|-----------------:|-----------:|-------------------:|------------------:|----------:|
| Plain text, default language             |           236 ns |      0.43× |              0.15× |             0.26× |     0.22× |
| Plain text, second language              |           242 ns |      0.43× |              0.17× |             0.29× |     0.25× |
| Plain text, region                       |           588 ns |      0.39× |              0.08× |             0.13× |     0.11× |
| Reworded text, region                    |           235 ns |      0.70× |              0.20× |             0.30× |     0.27× |
| Text with parameter, default language    |           336 ns |      1.26× |              0.11× |             0.62× |         – |
| Text with parameter, second language     |           342 ns |      1.23× |              0.13× |             0.63× |         – |
| Text with parameter, region              |           701 ns |      1.02× |              0.07× |             0.31× |         – |

### What the numbers mean

- **Plain text, default and second language.** A set takes a third to under half of the time `IStringLocalizer` takes, and allocates nothing. The registry works out once per culture which of its sets serve it, so each text looks only in those.
- **Plain text, region.** A British user's text comes from English, and `IStringLocalizer` is at its slowest there. On .NET, it takes 137 ns and allocates 168 bytes each time a text is shown. A set takes a quarter to half of that. An inline text takes about a tenth of it, in a field or in markup.
- **Reworded text, region.** A set takes about three quarters of the time `IStringLocalizer` takes on .NET and with AOT, and nearly as long with Mono's interpreter. Idioma checks which texts the British set words itself, as the build recorded them, so it reads only the wording it shows.
- **Text with parameter.** A set creates the text on each call, and the set's method words it. On .NET, that takes about 30% less time than `IStringLocalizer`, and allocates 168 bytes where `IStringLocalizer` allocates 152. With Mono's interpreter, it takes about a third less. With AOT, it takes about 1.25 times as long. For a British user, a set takes about half the time `IStringLocalizer` takes on .NET and with the interpreter, and about as long with AOT. There, it allocates 216 bytes.
- **Inline, in a field.** An inline text kept in a field takes a sixth to a third of the time `IStringLocalizer` takes, and allocates nothing. Its wording only has to be found for the user's culture.
- **Inline, in markup.** Markup calls an inline text on every render. Idioma keeps the plain inline texts it creates, up to 1,024 of them. A string literal is the same object on every render, so a render finds the text an earlier render created by comparing the strings as objects. A plain inline text then takes a quarter to two fifths of the time `IStringLocalizer` takes, and allocates nothing.
- **Inline text with parameter, in markup.** Its wording is a new string on every render, so Idioma creates the text again each time. It takes half to three quarters of the time `IStringLocalizer` takes, and a quarter to a third for a British user. It allocates 264 bytes, where `IStringLocalizer` allocates 152. It words every language as it's created, so its cost grows with the number of languages. Idioma doesn't keep such a text, because no later render could find it. So a value written into the wording, such as a user's name, doesn't stay in memory after its text is gone. There's one exception. Wording that your code built earlier and that outlived a garbage collection before the call is kept like a literal, in one of the 1,024 places.
- **Attribute.** Generated code keeps a text written as an attribute, and Idioma finds it by its type and the name of its member each time it's asked for. It compares the name as an object first, and `nameof(Address.City)` is the same string on every call. So the lookup adds little to what an inline text in a field takes: about 2 ns on .NET, 70 ns with Mono's interpreter, and 20 ns with AOT. It takes about a third of the time `IStringLocalizer` takes on .NET, and about a quarter in Blazor WebAssembly. It allocates nothing.

## Labels and enum values

The Attribute column finds a text by its type and the name of its member. Code can also find it by a property it holds, or ask for the text of a type or of a value of an enum.

Here Idioma is compared with `[Display]`, the attribute .NET itself has for the label of a property. `[Display]` isn't part of Idioma. It's the alternative, as `IStringLocalizer` is for the texts above, and it reads its wording from the same resource file. .NET reads an attribute through reflection, so a framework that shows forms reads `[Display]` once and keeps it. Both are measured.

Each row finds one label and shows it once, for a Dutch user, on .NET:

| Approach           | What's measured                                        |   Time | Allocated |
|--------------------|--------------------------------------------------------|-------:|----------:|
| .NET's `[Display]` | Its attribute read once and kept                       |  28 ns |       0 B |
| .NET's `[Display]` | Its attribute read from the property each time         | 759 ns |     760 B |
| Idioma             | `GetTextByMember<Order>(nameof(Order.Checkout))`       |  17 ns |       0 B |
| Idioma             | `GetTextByMember(property)`, by a `PropertyInfo`       |  18 ns |       0 B |
| Idioma             | `GetTextOfType<Order>()`                               |  15 ns |       0 B |
| Idioma             | `status.GetText()`, an enum with `[Localized]` members |  13 ns |       0 B |
| Idioma             | `status.GetText()`, a set of the enum                  |  17 ns |       0 B |

What the numbers mean:

- **`[Display]`.** With its attribute kept, a label takes 28 ns, most of it the lookup in the resource file. Read from the property each time, it takes about 0.8 µs and allocates 760 bytes, because .NET creates the attribute anew on every read.
- **Idioma, a property.** A label takes 17 to 18 ns, about three fifths of what `[Display]` takes with its attribute kept. It allocates nothing, and the caller has nothing to keep.
- **Idioma, a type.** The text of a type takes 15 ns.
- **Idioma, a value of an enum.** `GetText()` chooses the text in a `switch`. It takes 13 ns for an enum with `[Localized]` members, which is what showing an inline text takes. It takes 17 ns for a set of the enum, as a text of a set does.

These rows weren't measured in Blazor WebAssembly.

## Download size in Blazor WebAssembly

Each approach adds a base to the download, plus a part for each language beyond the default one. The sizes are for 100 texts a shop shows, on Mono, compressed with Brotli as a server sends them:

| What's downloaded                                                            | Interpreter |     AOT |
|------------------------------------------------------------------------------|------------:|--------:|
| `IStringLocalizer`: `Microsoft.Extensions.Localization` and its abstractions |     11.3 KB | 10.4 KB |
| `IStringLocalizer`: each extra language, as a satellite assembly             |      3.6 KB |  3.6 KB |
| Idioma: `CodeChops.Idioma`                                                   |     19.4 KB | 15.3 KB |
| Idioma: each extra language, as a set in the app's assembly                  |      1.8 KB |  1.7 KB |

What to know:

- A user of `IStringLocalizer` downloads only the satellite assembly of their own language. A user of the default language downloads none, because its resx wording is in the app's own assembly. So with the interpreter, `IStringLocalizer` costs a user 11.3 KB, or 14.9 KB for a user whose language isn't the default.
- Idioma compiles the set of every language into the app's assembly, so every user downloads all of them. So with the interpreter, an app in three languages costs every user 19.4 KB plus 2 × 1.8 KB, which is 23.0 KB.
- With AOT, a set also becomes native code, which adds 8.2 KB to `dotnet.native.wasm` before compression. That file is 3.6 MB compressed, and Brotli's result for it moves by kilobytes when a little of it changes. So the table can't show what that code adds after compression.
- Both approaches keep the default language's 100 texts in the app's own assembly, which the table leaves out. Idioma also generates a contract and a class for the texts there.
- `CodeChops.Idioma` is as large as an app that uses sets keeps it, because the trimmer leaves out the rest. An app that also writes inline texts or attributes keeps a little more of it.
- The sizes come from an app made to measure them: 100 shop texts in English and Dutch, published once with Dutch and once without.

## Run it yourself

The benchmarks are in [`benchmarks`](../benchmarks). One script runs them all, and writes the tables to `benchmarks/results`:

```shell
./benchmarks/run.sh
```

What to know:

- It takes about 25 minutes.
- It needs Chrome. Set `CHROME` to its path when it's installed somewhere else.
- The AOT steps need the `wasm-tools` workload: `dotnet workload install wasm-tools`. Without it, the script skips those steps.