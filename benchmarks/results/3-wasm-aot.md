Blazor WebAssembly, Mono AOT: .NET 11.0.0, in Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 236 | 242 | 581 | 239 |
| Checkout | Idioma set | 93 | 92 | 232 | 116 |
| Checkout | Idioma inline, in a field | 34 | 41 | 46 | 48 |
| Checkout | Idioma inline, in markup | 61 | 69 | 74 | 78 |
| Cart | IStringLocalizer | 238 | 239 | 235 | 236 |
| Cart | Idioma set | 98 | 93 | 174 | 118 |
| Cart | Idioma inline, in a field | 36 | 43 | 48 | 51 |
| Cart | Idioma inline, in markup | 60 | 67 | 73 | 77 |
| Greeting(name) | IStringLocalizer | 338 | 339 | 681 | 340 |
| Greeting(name) | Idioma set | 424 | 428 | 711 | 732 |
| Greeting(name) | Idioma inline, in a field | 36 | 45 | 48 | 50 |
| Greeting(name) | Idioma inline, in markup | 206 | 213 | 217 | 222 |

The loop and the call cost: en 8.5 ns, nl 8.4 ns, en-GB 8.5 ns, de 8.4 ns.
