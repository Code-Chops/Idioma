Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 711 | 727 | 2003 | 740 |
| Checkout | Idioma set | 66 | 69 | 63 | 66 |
| Checkout | Idioma inline | 71 | 72 | 71 | 74 |
| Checkout | Idioma attribute | 110 | 113 | 109 | 112 |
| Cart | IStringLocalizer | 693 | 708 | 689 | 724 |
| Cart | Idioma set | 66 | 66 | 62 | 65 |
| Cart | Idioma inline | 71 | 70 | 71 | 74 |
| Cart | Idioma attribute | 109 | 110 | 109 | 112 |
| Greeting(name) | IStringLocalizer | 1176 | 1157 | 2504 | 1210 |
| Greeting(name) | Idioma set | 159 | 157 | 158 | 876 |
| Greeting(name) | Idioma inline | 338 | 337 | 339 | 371 |

The loop and the call cost: en 11.6 ns, nl 11.6 ns, en-GB 11.6 ns, de 11.6 ns.
