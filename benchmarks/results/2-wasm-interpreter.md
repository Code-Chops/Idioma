Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 696 | 710 | 1983 | 723 |
| Checkout | Idioma set | 57 | 56 | 53 | 56 |
| Checkout | Idioma inline | 51 | 52 | 51 | 52 |
| Checkout | Idioma attribute | 94 | 93 | 94 | 93 |
| Cart | IStringLocalizer | 685 | 700 | 684 | 704 |
| Cart | Idioma set | 56 | 56 | 55 | 56 |
| Cart | Idioma inline | 51 | 51 | 50 | 53 |
| Cart | Idioma attribute | 94 | 100 | 93 | 93 |
| Greeting(name) | IStringLocalizer | 1164 | 1169 | 2494 | 1151 |
| Greeting(name) | Idioma set | 146 | 147 | 147 | 813 |
| Greeting(name) | Idioma inline | 318 | 326 | 317 | 329 |

The loop and the call cost: en 11.6 ns, nl 11.5 ns, en-GB 11.7 ns, de 11.6 ns.
