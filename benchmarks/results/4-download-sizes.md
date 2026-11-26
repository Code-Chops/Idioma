Download sizes, with the interpreter, compressed with Brotli as a server sends them. Each approach is measured against the same app without localization, which shows the 100 texts as English strings:

| What's downloaded | resx and IStringLocalizer | Idioma |
|---|---:|---:|
| The approach, with 100 texts in the default language | 19.3 KB | 34.7 KB |
| Of that, its own assemblies | 11.3 KB | 18.6 KB |
| Of that, the app's assembly | 4.0 KB | 9.4 KB |
| Of that, .NET's own assemblies | 3.9 KB | 6.7 KB |
| Of that, the rest, such as dotnet.js | 0.1 KB | 0.1 KB |
| Each extra language of 100 texts | 3.8 KB | 1.8 KB |

The files that grow by 0.5 KB or more, with 100 texts in the default language:

| File | resx and IStringLocalizer | Idioma |
|---|---:|---:|
| CodeChops.Idioma.wasm | 0.0 KB | 18.6 KB |
| Idioma.Benchmarks.Size.wasm | 4.0 KB | 9.4 KB |
| Microsoft.Extensions.Localization.Abstractions.wasm | 2.5 KB | 0.0 KB |
| Microsoft.Extensions.Localization.wasm | 8.8 KB | 0.0 KB |
| System.Collections.Immutable.wasm | 0.1 KB | 2.8 KB |
| System.Linq.wasm | 0.1 KB | 0.8 KB |
| System.Private.CoreLib.wasm | 2.4 KB | 1.1 KB |
| System.Threading.wasm | 0.0 KB | 1.7 KB |

Idioma in two languages, with the Dutch set in an assembly of its own that the app loads lazily, so only a Dutch user downloads it. Measured against the same app without localization:

| What's downloaded | Both sets in the app's assembly | The Dutch set loaded lazily |
|---|---:|---:|
| By a user of the default language | 36.5 KB | 51.5 KB |
| By a Dutch user | 36.5 KB | 56.6 KB |
| Of that, the Dutch set | 1.8 KB | 5.1 KB |
