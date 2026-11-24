Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 704 | 723 | 1969 | 715 |
| Checkout | Idioma set | 64 | 58 | 57 | 60 |
| Checkout | Idioma inline | 74 | 74 | 73 | 73 |
| Checkout | Idioma attribute | 110 | 112 | 110 | 109 |
| Cart | IStringLocalizer | 683 | 703 | 675 | 685 |
| Cart | Idioma set | 61 | 58 | 57 | 60 |
| Cart | Idioma inline | 74 | 74 | 74 | 73 |
| Cart | Idioma attribute | 110 | 112 | 111 | 109 |
| Greeting(name) | IStringLocalizer | 1160 | 1175 | 2460 | 1177 |
| Greeting(name) | Idioma set | 151 | 149 | 149 | 1581 |
| Greeting(name) | Idioma inline | 344 | 344 | 343 | 343 |

The loop and the call cost: en 11.6 ns, nl 11.4 ns, en-GB 11.4 ns, de 11.4 ns.
