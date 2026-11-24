Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/157.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB |
|---|---|---:|---:|---:|
| Checkout | IStringLocalizer | 678 | 697 | 1970 |
| Checkout | Idioma set | 61 | 61 | 59 |
| Checkout | Idioma inline | 73 | 77 | 76 |
| Checkout | Idioma attribute | 110 | 112 | 110 |
| Cart | IStringLocalizer | 658 | 676 | 686 |
| Cart | Idioma set | 61 | 61 | 58 |
| Cart | Idioma inline | 73 | 77 | 76 |
| Cart | Idioma attribute | 110 | 112 | 111 |
| Greeting(name) | IStringLocalizer | 1112 | 1125 | 2490 |
| Greeting(name) | Idioma set | 148 | 148 | 147 |
| Greeting(name) | Idioma inline | 341 | 339 | 346 |

The loop and the call cost: en 11.8 ns, nl 11.3 ns, en-GB 11.6 ns.
