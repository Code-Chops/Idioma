# Performance

Idioma shows a text in less time than `IStringLocalizer`, the approach Microsoft recommends for localizing an app. This page compares how long a text takes to show with each, on .NET and in Blazor WebAssembly. It also compares what each adds to a Blazor WebAssembly download.

Other subjects have a page of their own, listed in the [README](../README.md#documentation).

## In short

- A plain text of a set takes less than a tenth of the time `IStringLocalizer` takes, on .NET and with Mono's interpreter in WebAssembly, and allocates nothing. A text with a parameter takes about a tenth, and allocates only the string it returns.
- A text with a parameter takes longer for a user whose language falls back, such as a German user of an app in English and Dutch. It still takes less time than `IStringLocalizer`.
- A plain inline text takes a tenth of the time or less, and a text written as an attribute a seventh.
- Idioma adds more to a download. For 100 texts, it adds 30.6 KB and 1.9 KB for each extra language. resx and `IStringLocalizer` add 19.3 KB, and 3.8 KB for a user whose language isn't the default.

So speed rarely decides between them. A page that shows 200 texts spends about 9 µs on them with `IStringLocalizer` on .NET, and under 1 µs with Idioma's sets. With Mono's interpreter, that's about 0.14 ms and 0.01 ms.

## What was measured

Three texts: `Checkout` (plain), `Cart` (British English rewords it as "Basket"), and `Greeting(name)` (with a parameter). Four approaches show them:

- **IStringLocalizer**: `localizer["Checkout"]` on an `IStringLocalizer<Shop>`, from resx files and the class resx generates for them.
- **Idioma set**: `ShopLocalization.Checkout`.
- **Inline**: `LocalizedText.Inline(...)`, called each time the text is shown, as markup calls it on every render.
- **Attribute**: `LocalizedText.GetTextByMember<ShopLabels>(nameof(ShopLabels.Checkout))` for a property marked `[Localized]`, as a form finds the label of a field. An attribute takes no value, so it has no text with a parameter.

The app declares `("en", "nl", "en-GB")`. "Region" is a British user. "Fallback" is a German user, who sees English, the default language.

Each benchmark first checks that every approach shows the expected wording, so none is timed doing less work.

The numbers come from one run on an AMD Ryzen AI 9 HX PRO 370 on Windows 11, with .NET 11.0.0, BenchmarkDotNet 0.16.0-preview.2 and a headless Chrome 158. [Its tables](../benchmarks/results) are kept. Compare the factors rather than the nanoseconds, because those depend on the machine and on how busy it is. On .NET, each benchmark ran in three processes, because its time varies more from one process to the next than within one.

## Showing a text

Each benchmark shows one text once, as a render does. The `IStringLocalizer` column shows how long that takes. The other columns show their time as a factor of it, so 0.50× takes half as long. On .NET, a cell also shows the bytes allocated.

### On .NET

| Text, user's culture                  | IStringLocalizer | Idioma set   | Inline       | Attribute  |
|---------------------------------------|------------------|--------------|--------------|------------|
| Plain text, default language          | 46 ns, 72 B      | 0.07×, 0 B   | 0.10×, 0 B   | 0.14×, 0 B |
| Plain text, second language           | 47 ns, 72 B      | 0.07×, 0 B   | 0.10×, 0 B   | 0.14×, 0 B |
| Plain text, region                    | 144 ns, 168 B    | 0.02×, 0 B   | 0.03×, 0 B   | 0.05×, 0 B |
| Reworded text, region                 | 45 ns, 72 B      | 0.07×, 0 B   | 0.10×, 0 B   | 0.14×, 0 B |
| Plain text, fallback                  | 46 ns, 72 B      | 0.07×, 0 B   | 0.10×, 0 B   | 0.14×, 0 B |
| Text with parameter, default language | 68 ns, 152 B     | 0.09×, 48 B  | 0.24×, 144 B | –          |
| Text with parameter, second language  | 68 ns, 152 B     | 0.09×, 48 B  | 0.24×, 144 B | –          |
| Text with parameter, region           | 165 ns, 248 B    | 0.04×, 48 B  | 0.10×, 144 B | –          |
| Text with parameter, fallback         | 69 ns, 152 B     | 0.60×, 48 B  | 0.25×, 144 B | –          |

### In Blazor WebAssembly

Blazor WebAssembly runs .NET code on Mono's interpreter by default. Only the interpreter was measured, not AOT (`RunAOTCompilation`).

The browser has no BenchmarkDotNet, so the app times each text itself. Each time is the median of nine rounds of at least 25 ms, after a warm-up, without the cost of the loop. The first page load is dropped, because Chrome hadn't optimized the WebAssembly yet.

| Text, user's culture                  | IStringLocalizer | Idioma set | Inline | Attribute |
|---------------------------------------|------------------|------------|--------|-----------|
| Plain text, default language          | 696 ns           | 0.08×      | 0.07×  | 0.14×     |
| Plain text, second language           | 710 ns           | 0.08×      | 0.07×  | 0.13×     |
| Plain text, region                    | 1,983 ns         | 0.03×      | 0.03×  | 0.05×     |
| Reworded text, region                 | 684 ns           | 0.08×      | 0.07×  | 0.14×     |
| Plain text, fallback                  | 723 ns           | 0.08×      | 0.07×  | 0.13×     |
| Text with parameter, default language | 1,164 ns         | 0.13×      | 0.27×  | –         |
| Text with parameter, second language  | 1,169 ns         | 0.13×      | 0.28×  | –         |
| Text with parameter, region           | 2,494 ns         | 0.06×      | 0.13×  | –         |
| Text with parameter, fallback         | 1,151 ns         | 0.71×      | 0.29×  | –         |

The times in the browser vary more between runs than those on .NET, most of all on a machine that is still warm from other work. In three runs of this version, a plain text took 50 to 58 ns with a set, and 680 to 960 ns with `IStringLocalizer`. The table is from a run on a machine at rest.

### What the numbers mean

- **Plain text.** Idioma keeps, per culture, the set that words each text. So showing a text reads that table and calls the set's property, which allocates nothing.
- **Region.** A British user's text costs a set the same as any other, because Idioma works out once which set words it. `IStringLocalizer` looks for British wording first on every call. So a text that comes from English takes it three times as long, about 145 ns and 168 bytes on .NET.
- **Text with parameter.** Idioma hands the values to the method of the set for the user's language. That takes about 6 ns on .NET, and allocates only the string the method makes, 48 bytes here.
- **Fallback.** A text with a parameter runs its method in the culture of the language that words it. A German user sees English, so the call switches the thread's culture to English. That takes about seven times as long as for an English user on .NET, and five and a half times with the interpreter. It's still three fifths of what `IStringLocalizer` takes on .NET, and seven tenths with the interpreter. A plain text switches nothing.
- **Inline.** A plain inline text picks one of the wordings it's called with. That takes about 4.6 ns on .NET, and allocates nothing.
- **Inline text with parameter.** C# builds the wording of every language before `Inline` picks one. With three languages, that takes about 16 ns and allocates 144 bytes, and it grows with each language. For a text you show often, write a method of a set, which builds one wording.
- **Attribute.** Idioma finds the text by its type and the name of its member on each call. It compares the name as an object first, and `nameof(Address.City)` gives the same string every time. That takes about 6.4 ns on .NET, and allocates nothing.

The first switch in an execution context, such as the first of a request, copies the context. The next ones reuse that copy. A Blazor app can avoid the switch by making its default language the UI culture of a German user, as [Blazor](blazor.md#a-browser-language-the-app-doesnt-have) shows.

## Labels and enum values

Idioma can also find a label by its `PropertyInfo`, and give the text of a type or of a value of an enum. Here it's compared with .NET's `[Display]` attribute, which reads its wording from the same resource file. .NET reads an attribute through reflection, so a framework that shows forms reads `[Display]` once and keeps it. Both ways are measured.

Each row finds one label and shows it once, for a Dutch user, on .NET:

| Approach           | What's measured                                        | Time   | Allocated |
|--------------------|--------------------------------------------------------|--------|-----------|
| .NET's `[Display]` | Its attribute read once and kept                       | 36 ns  | 0 B       |
| .NET's `[Display]` | Its attribute read from the property each time         | 820 ns | 760 B     |
| Idioma             | `GetTextByMember<Order>(nameof(Order.Checkout))`       | 6.4 ns | 0 B       |
| Idioma             | `GetTextByMember(property)`, by a `PropertyInfo`       | 9.6 ns | 0 B       |
| Idioma             | `GetTextOfType<Order>()`                               | 5.2 ns | 0 B       |
| Idioma             | `status.GetText()`, an enum with `[Localized]` members | 4.8 ns | 0 B       |
| Idioma             | `status.GetText()`, a set of the enum                  | 3.4 ns | 0 B       |

- **`[Display]`.** With its attribute kept, a label takes 36 ns, most of it the lookup in the resource file. Read each time, it takes about 0.8 µs and allocates 760 bytes, because .NET creates the attribute anew on every read.
- **Idioma.** A label takes 3 to 10 ns, less than a third of what a kept `[Display]` takes. It allocates nothing, and the caller has nothing to keep. An enum's `GetText()` first asks whether a set of another assembly gives the enum its texts, and else chooses the text in a `switch`.

These rows weren't measured in Blazor WebAssembly.

## Download size in Blazor WebAssembly

Each size is what an approach adds to the same app without localization, which shows the 100 texts as English strings. It's with Mono's interpreter, compressed with Brotli as a server sends it. AOT adds more, because it also compiles code into `dotnet.native.wasm`.

| What's downloaded                      | resx and `IStringLocalizer` |                 Idioma |
|----------------------------------------|----------------------------:|-----------------------:|
| Its own assemblies                     |                     11.3 KB |                15.5 KB |
| Its texts and their generated code     |                      4.0 KB |                 9.5 KB |
| What it keeps of .NET's own assemblies |                      3.9 KB |                 5.6 KB |
| The rest, such as `dotnet.js`          |                      0.1 KB |                 0.1 KB |
| **100 texts in the default language**  |                 **19.3 KB** |            **30.6 KB** |
| Each extra language                    |  3.8 KB, only for its users | 1.9 KB, for every user |

- A user of resx downloads only the satellite assembly of their own language. Every user of Idioma downloads the set of every language. So in an app in English and Dutch, a Dutch user downloads 23.1 KB with resx and 32.5 KB with Idioma.
- A set in an assembly of its own, which the app loads lazily, doesn't pay off for 100 texts. An English user then downloads 47.2 KB, and a Dutch user 52.3 KB.
- Most of what Idioma keeps of .NET is `System.Collections.Immutable` (2.4 KB), `System.Threading` (1.7 KB), `System.Linq` (0.8 KB) and `System.Private.CoreLib` (0.4 KB). An app that uses them itself downloads them anyway.

## Run it yourself

The benchmarks are in [`benchmarks`](../benchmarks). One script runs them all in about 12 minutes, and writes the tables to `benchmarks/results`:

```shell
./benchmarks/run.sh
```

- It needs Chrome. Set `CHROME` to its path when it's installed somewhere else.
- The AOT steps need the `wasm-tools` workload: `dotnet workload install wasm-tools`. Without it, the script skips those steps.

To compare two versions of your code, run only the .NET benchmarks you need, with `--quick`. That's one process per benchmark, with fewer iterations, so each takes a few seconds:

```shell
dotnet run -c Release --project benchmarks/Idioma.Benchmarks -- --quick --filter '*IdiomaSet*'
```

Its numbers vary more than those of a full run, so the tables in `benchmarks/results` come from the script.
