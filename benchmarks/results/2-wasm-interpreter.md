Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 725 | 724 | 2038 | 730 |
| Checkout | Idioma set | 62 | 62 | 62 | 61 |
| Checkout | Idioma inline | 72 | 72 | 74 | 71 |
| Checkout | Idioma attribute | 113 | 113 | 113 | 112 |
| Cart | IStringLocalizer | 694 | 710 | 699 | 714 |
| Cart | Idioma set | 63 | 62 | 62 | 61 |
| Cart | Idioma inline | 73 | 74 | 74 | 75 |
| Cart | Idioma attribute | 113 | 113 | 114 | 113 |
| Greeting(name) | IStringLocalizer | 1173 | 1175 | 2585 | 1179 |
| Greeting(name) | Idioma set | 151 | 152 | 152 | 933 |
| Greeting(name) | Idioma inline | 344 | 345 | 351 | 365 |

The loop and the call cost: en 11.9 ns, nl 11.3 ns, en-GB 11.6 ns, de 11.6 ns.
