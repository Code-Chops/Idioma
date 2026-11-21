Blazor WebAssembly, Mono AOT: .NET 11.0.0, in Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 246 | 247 | 590 | 251 |
| Checkout | resx class | 107 | 106 | 451 | 103 |
| Checkout | Idioma set | 100 | 101 | 182 | 117 |
| Checkout | Idioma inline, in a field | 35 | 43 | 46 | 47 |
| Checkout | Idioma inline, in markup | 63 | 69 | 75 | 74 |
| Checkout | Idioma attribute | 54 | 62 | 66 | 70 |
| Cart | IStringLocalizer | 235 | 244 | 240 | 241 |
| Cart | resx class | 104 | 104 | 101 | 103 |
| Cart | Idioma set | 101 | 101 | 172 | 115 |
| Cart | Idioma inline, in a field | 35 | 42 | 48 | 47 |
| Cart | Idioma inline, in markup | 62 | 69 | 72 | 74 |
| Cart | Idioma attribute | 50 | 59 | 65 | 65 |
| Greeting(name) | IStringLocalizer | 351 | 358 | 698 | 338 |
| Greeting(name) | resx class | 209 | 204 | 555 | 205 |
| Greeting(name) | Idioma set | 435 | 426 | 531 | 730 |
| Greeting(name) | Idioma inline, in a field | 35 | 41 | 45 | 48 |
| Greeting(name) | Idioma inline, in markup | 215 | 217 | 216 | 221 |

The loop and the call cost: en 8.8 ns, nl 8.8 ns, en-GB 8.8 ns, de 9.0 ns.
