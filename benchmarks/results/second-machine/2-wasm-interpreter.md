Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/157.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB |
|---|---|---:|---:|---:|
| Checkout | IStringLocalizer | 940 | 760 | 2873 |
| Checkout | Idioma set | 292 | 365 | 759 |
| Checkout | Idioma inline, in a field | 198 | 168 | 215 |
| Checkout | Idioma inline, in markup | 353 | 267 | 286 |
| Checkout | Idioma attribute | 249 | 204 | 190 |
| Cart | IStringLocalizer | 1119 | 824 | 769 |
| Cart | Idioma set | 460 | 345 | 594 |
| Cart | Idioma inline, in a field | 138 | 218 | 146 |
| Cart | Idioma inline, in markup | 312 | 386 | 244 |
| Cart | Idioma attribute | 256 | 275 | 183 |
| Greeting(name) | IStringLocalizer | 1298 | 1606 | 3711 |
| Greeting(name) | Idioma set | 751 | 767 | 1982 |
| Greeting(name) | Idioma inline, in a field | 132 | 163 | 145 |
| Greeting(name) | Idioma inline, in markup | 652 | 841 | 692 |

The loop and the call cost: en 19.1 ns, nl 14.2 ns, en-GB 13.8 ns.
