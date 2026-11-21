Blazor WebAssembly, Mono AOT: .NET 11.0.0, in Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB |
|---|---|---:|---:|---:|
| Checkout | IStringLocalizer | 266 | 280 | 696 |
| Checkout | Idioma set | 100 | 104 | 193 |
| Checkout | Idioma inline, in a field | 37 | 45 | 49 |
| Checkout | Idioma inline, in markup | 68 | 78 | 83 |
| Checkout | Idioma attribute | 57 | 64 | 69 |
| Cart | IStringLocalizer | 277 | 288 | 271 |
| Cart | Idioma set | 103 | 107 | 195 |
| Cart | Idioma inline, in a field | 38 | 45 | 52 |
| Cart | Idioma inline, in markup | 70 | 78 | 84 |
| Cart | Idioma attribute | 55 | 62 | 68 |
| Greeting(name) | IStringLocalizer | 446 | 458 | 869 |
| Greeting(name) | Idioma set | 500 | 494 | 618 |
| Greeting(name) | Idioma inline, in a field | 38 | 45 | 51 |
| Greeting(name) | Idioma inline, in markup | 286 | 289 | 289 |

The loop and the call cost: en 9.0 ns, nl 9.0 ns, en-GB 9.0 ns.
