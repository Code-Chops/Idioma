Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 723 | 721 | 2030 | 715 |
| Checkout | Idioma set | 63 | 59 | 61 | 65 |
| Checkout | Idioma inline | 73 | 72 | 70 | 73 |
| Checkout | Idioma attribute | 112 | 111 | 110 | 113 |
| Cart | IStringLocalizer | 703 | 700 | 712 | 697 |
| Cart | Idioma set | 63 | 59 | 60 | 63 |
| Cart | Idioma inline | 73 | 72 | 72 | 73 |
| Cart | Idioma attribute | 110 | 111 | 113 | 111 |
| Greeting(name) | IStringLocalizer | 1164 | 1158 | 2588 | 1156 |
| Greeting(name) | Idioma set | 149 | 149 | 147 | 918 |
| Greeting(name) | Idioma inline | 342 | 339 | 338 | 345 |

The loop and the call cost: en 12.2 ns, nl 11.8 ns, en-GB 11.9 ns, de 11.7 ns.
