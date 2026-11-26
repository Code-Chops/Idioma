Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 761 | 748 | 2116 | 750 |
| Checkout | Idioma set | 63 | 60 | 59 | 94 |
| Checkout | Idioma inline | 76 | 80 | 72 | 72 |
| Checkout | Idioma attribute | 117 | 109 | 109 | 112 |
| Cart | IStringLocalizer | 782 | 727 | 727 | 720 |
| Cart | Idioma set | 62 | 65 | 60 | 58 |
| Cart | Idioma inline | 71 | 70 | 76 | 70 |
| Cart | Idioma attribute | 114 | 114 | 111 | 108 |
| Greeting(name) | IStringLocalizer | 1190 | 1220 | 3135 | 1175 |
| Greeting(name) | Idioma set | 141 | 143 | 142 | 880 |
| Greeting(name) | Idioma inline | 417 | 335 | 344 | 338 |

The loop and the call cost: en 12.4 ns, nl 11.7 ns, en-GB 11.9 ns, de 11.5 ns.
