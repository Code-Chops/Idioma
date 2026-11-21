Download sizes, with the interpreter, compressed with Brotli as a server sends them. Each approach is measured against the same app without localization, which shows the 100 texts as English strings:

| What's downloaded | resx and IStringLocalizer | Idioma |
|---|---:|---:|
| The approach, with 100 texts in the default language | 19.2 KB | 46.2 KB |
| Of that, its own assemblies | 11.3 KB | 20.9 KB |
| Of that, the app's assembly | 3.9 KB | 8.5 KB |
| Of that, .NET's own assemblies | 3.9 KB | 16.6 KB |
| Of that, the rest, such as dotnet.js | 0.1 KB | 0.1 KB |
| Each extra language of 100 texts | 3.8 KB | 1.7 KB |

The files that grow by 0.5 KB or more, with 100 texts in the default language:

| File | resx and IStringLocalizer | Idioma |
|---|---:|---:|
| CodeChops.Idioma.wasm | 0.0 KB | 20.9 KB |
| Idioma.Benchmarks.Size.wasm | 3.9 KB | 8.5 KB |
| Microsoft.Extensions.Localization.Abstractions.wasm | 2.5 KB | 0.0 KB |
| Microsoft.Extensions.Localization.wasm | 8.8 KB | 0.0 KB |
| System.Collections.Immutable.wasm | 0.1 KB | 11.6 KB |
| System.Linq.wasm | 0.1 KB | 2.1 KB |
| System.Private.CoreLib.wasm | 2.4 KB | 0.4 KB |
| System.Text.Json.wasm | 0.0 KB | 0.8 KB |
| System.Threading.wasm | 0.0 KB | 1.7 KB |
