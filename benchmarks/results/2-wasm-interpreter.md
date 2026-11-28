Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 769 | 836 | 2094 | 787 |
| Checkout | Idioma set | 45 | 54 | 40 | 39 |
| Checkout | Idioma inline | 54 | 53 | 52 | 52 |
| Checkout | Idioma attribute | 108 | 96 | 99 | 100 |
| Cart | IStringLocalizer | 721 | 721 | 707 | 713 |
| Cart | Idioma set | 38 | 39 | 37 | 38 |
| Cart | Idioma inline | 57 | 53 | 52 | 51 |
| Cart | Idioma attribute | 97 | 96 | 101 | 100 |
| Greeting(name) | IStringLocalizer | 1190 | 1180 | 2553 | 1170 |
| Greeting(name) | Idioma set | 133 | 135 | 136 | 135 |
| Greeting(name) | Idioma inline | 322 | 334 | 325 | 327 |

The loop and the call cost: en 12.8 ns, nl 12.6 ns, en-GB 12.3 ns, de 11.8 ns.
