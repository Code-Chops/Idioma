# Performance

Idioma shows most texts in less time than `IStringLocalizer`, which is the approach Microsoft recommends for localizing an app. This page compares how long a text takes to show with each, on .NET and in Blazor WebAssembly, and what each adds to a Blazor WebAssembly download.

Other subjects have a page of their own: [language codes](language-codes.md), [libraries that use Idioma](libraries-and-apps.md), [Blazor](blazor.md), [what the build rejects](build-errors.md), [coming from resx](coming-from-resx.md), and [how Idioma works](how-it-works.md).

## In short

- On .NET, a plain text of a set takes about 16 ns for a user of the default or the second language, where `IStringLocalizer` takes about 45 ns. It allocates nothing, where `IStringLocalizer` allocates about 70 bytes.
- In Blazor WebAssembly, the same takes a set about 0.4 µs with Mono's interpreter, and about 0.1 µs with AOT. That's less than half of what `IStringLocalizer` takes.
- A plain inline text kept in a field takes a sixth to a third of the time `IStringLocalizer` takes. Written in markup, which calls it on every render, it takes a quarter to two fifths, because Idioma keeps the text the first render created.
- A set takes longer than `IStringLocalizer` only for a text with parameter with WebAssembly AOT. It takes 1.3 times as long there, and about as long for a British user.
- Idioma adds 19.2 KB to a Blazor WebAssembly download, compressed, plus 1.9 KB for each extra language of 100 texts. `IStringLocalizer` adds 11.3 KB, plus 3.6 KB for a user whose language isn't the default.

So speed rarely decides between them. A page that shows 200 texts spends about 9 µs on them with `IStringLocalizer` on .NET, and about 3 µs with Idioma's sets. With Mono's interpreter in WebAssembly, it spends about 0.2 ms with `IStringLocalizer`, and about 0.08 ms with Idioma's sets.

## What was measured

Three texts: `Checkout` (plain), `Cart` (British English rewords it as "Basket"), and `Greeting(name)` (with a parameter).

Four approaches show them:

- **IStringLocalizer**: `localizer["Checkout"]` on an `IStringLocalizer<Shop>`, from resx files.
- **Idioma set**: `ShopLocalization.Checkout`.
- **Inline, in a field**: `LocalizableText.Inline(...)`, created once and kept. So its text with parameter is already worded, and isn't comparable with the other columns.
- **Inline, in markup**: the same call on every render.

The app declares `("en", "nl", "en-GB")`. "Region" is a British user.

A user whose language the app doesn't have, such as a German user, has no row. The app makes its default language their UI culture, as [Blazor](blazor.md#a-browser-language-the-app-doesnt-have) shows, so their texts take as long as for a user of the default language.

Each benchmark first checks that every approach shows the expected wording, so no approach is timed doing less work.

The numbers come from one machine, an Apple M2 Max on macOS 26.6. They ran on .NET 11.0.0 with BenchmarkDotNet 0.16.0-preview.2, and in Blazor WebAssembly on .NET 11.0.0 in a headless Chrome 158. Compare the approaches with each other rather than with your own numbers, because those depend on the machine.

On most rows, two runs of the same code differed by less than 5%. A few differed by up to 13%. So a gap of about 10% or less between two approaches may be noise. One row differed more: in some runs, the inline text in a field for the second language took 18 ns on .NET, where it took 12 to 14 ns in the others.

## Showing a text

Each benchmark shows one text once, as a render does. The `IStringLocalizer` column shows how long that takes. The other columns show their time as a factor of it, so 0.50× takes half as long. On .NET, a cell also shows the bytes allocated each time a text is shown.

### On .NET

| Text, user's culture                     | IStringLocalizer |   Idioma set | Inline, in a field | Inline, in markup |
|------------------------------------------|-----------------:|-------------:|-------------------:|------------------:|
| Plain text, default language             |      46 ns, 72 B |   0.36×, 0 B |         0.25×, 0 B |        0.33×, 0 B |
| Plain text, second language              |      44 ns, 72 B |   0.36×, 0 B |         0.31×, 0 B |        0.39×, 0 B |
| Plain text, region                       |    133 ns, 168 B |   0.26×, 0 B |         0.10×, 0 B |        0.13×, 0 B |
| Reworded text, region                    |      39 ns, 72 B |   0.74×, 0 B |         0.35×, 0 B |        0.44×, 0 B |
| Text with parameter, default language    |     75 ns, 152 B | 0.73×, 168 B |         0.16×, 0 B |      0.69×, 264 B |
| Text with parameter, second language     |     78 ns, 152 B | 0.71×, 168 B |         0.18×, 0 B |      0.69×, 264 B |
| Text with parameter, region              |    175 ns, 248 B | 0.47×, 216 B |         0.08×, 0 B |      0.31×, 264 B |

### In Blazor WebAssembly

Blazor WebAssembly runs .NET code on the Mono runtime by default, with an interpreter. Publishing with `RunAOTCompilation` compiles it to WebAssembly ahead of time (AOT) instead, which makes the download bigger.

The browser has no BenchmarkDotNet, so the app times each text itself. Each time is the median of nine rounds of at least 25 ms, after a warm-up, without the cost of the loop. These tables show no allocations. Each table's first page is loaded twice, and its first result dropped, because the first page of a fresh browser ran before Chrome had optimized the WebAssembly.

With Mono's interpreter:

| Text, user's culture                     | IStringLocalizer | Idioma set | Inline, in a field | Inline, in markup |
|------------------------------------------|-----------------:|-----------:|-------------------:|------------------:|
| Plain text, default language             |           922 ns |      0.44× |              0.20× |             0.33× |
| Plain text, second language              |           960 ns |      0.43× |              0.23× |             0.35× |
| Plain text, region                       |         2,639 ns |      0.37× |              0.07× |             0.12× |
| Reworded text, region                    |           933 ns |      0.92× |              0.21× |             0.35× |
| Text with parameter, default language    |         1,475 ns |      0.66× |              0.12× |             0.56× |
| Text with parameter, second language     |         1,497 ns |      0.65× |              0.14× |             0.55× |
| Text with parameter, region              |         3,152 ns |      0.56× |              0.06× |             0.26× |

With Mono's AOT:

| Text, user's culture                     | IStringLocalizer | Idioma set | Inline, in a field | Inline, in markup |
|------------------------------------------|-----------------:|-----------:|-------------------:|------------------:|
| Plain text, default language             |           236 ns |      0.39× |              0.14× |             0.26× |
| Plain text, second language              |           242 ns |      0.38× |              0.17× |             0.29× |
| Plain text, region                       |           581 ns |      0.40× |              0.08× |             0.13× |
| Reworded text, region                    |           235 ns |      0.74× |              0.20× |             0.31× |
| Text with parameter, default language    |           338 ns |      1.25× |              0.11× |             0.61× |
| Text with parameter, second language     |           339 ns |      1.26× |              0.13× |             0.63× |
| Text with parameter, region              |           681 ns |      1.04× |              0.07× |             0.32× |

### What the numbers mean

- **Plain text, default and second language.** A set takes a third to under half of the time `IStringLocalizer` takes, and allocates nothing. The registry works out once per culture which of its sets serve it, so each text looks only in those.
- **Plain text, region.** A British user's text comes from English, and `IStringLocalizer` is at its slowest there. On .NET, it takes 133 ns and allocates 168 bytes each time a text is shown. A set takes a quarter to half of that. An inline text takes about a tenth of it, in a field or in markup.
- **Reworded text, region.** A set takes about three quarters of the time `IStringLocalizer` takes on .NET and with AOT, and nearly as long with Mono's interpreter. Idioma checks which texts the British set words itself, as the build recorded them, so it reads only the wording it shows.
- **Text with parameter.** A set creates the text on each call, and the set's method words it. On .NET, that takes about 30% less time than `IStringLocalizer`, and allocates 168 bytes where `IStringLocalizer` allocates 152. With Mono's interpreter, it takes about a third less. With AOT, it takes 1.3 times as long. For a British user, a set takes about half the time `IStringLocalizer` takes on .NET and with the interpreter, and about as long with AOT. There, it allocates 216 bytes.
- **Inline, in a field.** An inline text kept in a field takes a sixth to a third of the time `IStringLocalizer` takes, and allocates nothing. Its wording only has to be found for the user's culture.
- **Inline, in markup.** Markup calls an inline text on every render. Idioma keeps the plain inline texts it creates, up to 1,024 of them. A string literal is the same object on every render, so a render finds the text an earlier render created by comparing the strings as objects. A plain inline text then takes a quarter to two fifths of the time `IStringLocalizer` takes, and allocates nothing.
- **Inline text with parameter, in markup.** Its wording is a new string on every render, so Idioma creates the text again each time. It takes half to three quarters of the time `IStringLocalizer` takes, and a quarter to a third for a British user. It allocates 264 bytes, where `IStringLocalizer` allocates 152. It words every language as it's created, so its cost grows with the number of languages. Idioma doesn't keep such a text, because no later render could find it. So a value written into the wording, such as a user's name, doesn't stay in memory after its text is gone. There's one exception. Wording that your code built earlier and that outlived a garbage collection before the call is kept like a literal, in one of the 1,024 places.

## Download size in Blazor WebAssembly

Each approach adds a base to the download, plus a part for each language beyond the default one. The sizes are for 100 texts a shop shows, on Mono, compressed with Brotli as a server sends them:

| What's downloaded                                                            | Interpreter |     AOT |
|------------------------------------------------------------------------------|------------:|--------:|
| `IStringLocalizer`: `Microsoft.Extensions.Localization` and its abstractions |     11.3 KB | 10.4 KB |
| `IStringLocalizer`: each extra language, as a satellite assembly             |      3.6 KB |  3.6 KB |
| Idioma: `CodeChops.Idioma`                                                   |     19.2 KB | 15.2 KB |
| Idioma: each extra language, as a set in the app's assembly                  |      1.9 KB |  1.8 KB |

What to know:

- A user of `IStringLocalizer` downloads only the satellite assembly of their own language. A user of the default language downloads none, because its resx wording is in the app's own assembly. So with the interpreter, `IStringLocalizer` costs a user 11.3 KB, or 14.9 KB for a user whose language isn't the default.
- Idioma compiles the set of every language into the app's assembly, so every user downloads all of them. So with the interpreter, an app in three languages costs every user 19.2 KB plus 2 × 1.9 KB, which is 23.0 KB.
- With AOT, a set also becomes native code, which adds 8.2 KB to `dotnet.native.wasm` before compression. That file is 3.6 MB compressed, and Brotli's result for it moves by kilobytes when a little of it changes. So the table can't show what that code adds after compression.
- Both approaches keep the default language's 100 texts in the app's own assembly, which the table leaves out. Idioma also generates a contract and a class for the texts there.
- `CodeChops.Idioma` is as large as an app that uses sets keeps it, because the trimmer leaves out the rest. An app that also writes inline texts keeps a little more of it.
- The sizes come from an app made to measure them: 100 shop texts in English and Dutch, published once with Dutch and once without.

## Run it yourself

The benchmarks are in [`benchmarks`](../benchmarks). One script runs them all, and writes the tables to `benchmarks/results`:

```shell
./benchmarks/run.sh
```

What to know:

- It takes about 20 minutes.
- It needs Chrome. Set `CHROME` to its path when it's installed somewhere else.
- The AOT steps need the `wasm-tools` workload: `dotnet workload install wasm-tools`. Without it, the script skips those steps.