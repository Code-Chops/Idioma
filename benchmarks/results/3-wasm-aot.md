Blazor WebAssembly, Mono AOT: .NET 11.0.0, in Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 236 | 242 | 588 | 239 |
| Checkout | Idioma set | 102 | 103 | 232 | 120 |
| Checkout | Idioma inline, in a field | 35 | 42 | 46 | 50 |
| Checkout | Idioma inline, in markup | 61 | 69 | 75 | 75 |
| Checkout | Idioma attribute | 53 | 60 | 66 | 69 |
| Cart | IStringLocalizer | 238 | 243 | 235 | 236 |
| Cart | Idioma set | 95 | 101 | 165 | 118 |
| Cart | Idioma inline, in a field | 36 | 44 | 47 | 51 |
| Cart | Idioma inline, in markup | 59 | 68 | 71 | 73 |
| Cart | Idioma attribute | 54 | 61 | 63 | 69 |
| Greeting(name) | IStringLocalizer | 336 | 342 | 701 | 336 |
| Greeting(name) | Idioma set | 422 | 419 | 717 | 726 |
| Greeting(name) | Idioma inline, in a field | 36 | 43 | 47 | 49 |
| Greeting(name) | Idioma inline, in markup | 208 | 215 | 217 | 221 |

The loop and the call cost: en 8.5 ns, nl 8.6 ns, en-GB 8.5 ns, de 8.5 ns.
