Download sizes, with the interpreter, compressed with Brotli as a server sends them. Each approach is measured against the same app without localization, which shows the 100 texts as English strings:

| What's downloaded | resx and IStringLocalizer | Idioma |
|---|---:|---:|
| The approach, with 100 texts in the default language | 17.9 KB | 46.5 KB |
| Of that, its own assemblies | 11.3 KB | 20.3 KB |
| Of that, the app's assembly | 4.1 KB | 8.8 KB |
| Of that, .NET's own assemblies | 2.5 KB | 17.3 KB |
| Of that, the rest, such as dotnet.js | 0.1 KB | 0.1 KB |
| Each extra language of 100 texts | 3.8 KB | 1.7 KB |

The files that grow by 0.5 KB or more, with 100 texts in the default language:

| File | resx and IStringLocalizer | Idioma |
|---|---:|---:|
| CodeChops.Idioma.wasm | 0.0 KB | 20.3 KB |
| Idioma.Benchmarks.Size.wasm | 4.1 KB | 8.8 KB |
| Microsoft.Extensions.Localization.Abstractions.wasm | 2.5 KB | 0.0 KB |
| Microsoft.Extensions.Localization.wasm | 8.8 KB | 0.0 KB |
| System.Collections.Immutable.wasm | 0.1 KB | 11.6 KB |
| System.Linq.wasm | 0.0 KB | 2.0 KB |
| System.Private.CoreLib.wasm | 1.2 KB | 1.1 KB |
| System.Text.Json.wasm | 0.0 KB | 0.8 KB |
| System.Threading.wasm | 0.0 KB | 1.7 KB |

Download sizes, with AOT, compressed with Brotli as a server sends them. Each approach is measured against the same app without localization, which shows the 100 texts as English strings:

| What's downloaded | resx and IStringLocalizer | Idioma |
|---|---:|---:|
| The approach, with 100 texts in the default language | 15.3 KB | 37.3 KB |
| Of that, its own assemblies | 10.4 KB | 16.0 KB |
| Of that, the app's assembly | 3.8 KB | 6.4 KB |
| Of that, .NET's own assemblies | 1.1 KB | 14.9 KB |
| Of that, the rest, such as dotnet.js | 0.1 KB | 0.1 KB |
| Each extra language of 100 texts | 3.8 KB | 1.8 KB |
| The approach as native code in dotnet.native.wasm, before compression | 76.2 KB | 413.3 KB |
| The approach as native code, compressed | 20.2 KB | 91.7 KB |
| Each extra language as native code, before compression | 0.0 KB | 8.4 KB |
| Each extra language as native code, compressed | -4.5 KB | 2.2 KB |

The files that grow by 0.5 KB or more, with 100 texts in the default language:

| File | resx and IStringLocalizer | Idioma |
|---|---:|---:|
| CodeChops.Idioma.wasm | 0.0 KB | 16.0 KB |
| Idioma.Benchmarks.Size.wasm | 3.8 KB | 6.4 KB |
| Microsoft.Extensions.Localization.Abstractions.wasm | 2.5 KB | 0.0 KB |
| Microsoft.Extensions.Localization.wasm | 8.0 KB | 0.0 KB |
| System.Collections.Immutable.wasm | -0.1 KB | 11.3 KB |
| System.Linq.wasm | 0.1 KB | 2.0 KB |
| System.Private.CoreLib.wasm | 0.3 KB | -0.8 KB |
| System.Threading.wasm | 0.0 KB | 1.7 KB |
