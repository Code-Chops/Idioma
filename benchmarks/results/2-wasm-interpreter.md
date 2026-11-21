Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 949 | 986 | 2716 | 973 |
| Checkout | resx class | 457 | 467 | 2132 | 476 |
| Checkout | Idioma set | 380 | 382 | 849 | 423 |
| Checkout | Idioma inline, in a field | 178 | 220 | 198 | 208 |
| Checkout | Idioma inline, in markup | 305 | 347 | 321 | 337 |
| Checkout | Idioma attribute | 241 | 281 | 260 | 275 |
| Cart | IStringLocalizer | 918 | 956 | 983 | 937 |
| Cart | resx class | 435 | 446 | 434 | 456 |
| Cart | Idioma set | 379 | 377 | 823 | 421 |
| Cart | Idioma inline, in a field | 179 | 215 | 198 | 209 |
| Cart | Idioma inline, in markup | 307 | 341 | 326 | 339 |
| Cart | Idioma attribute | 241 | 276 | 262 | 275 |
| Greeting(name) | IStringLocalizer | 1518 | 1491 | 3252 | 1490 |
| Greeting(name) | resx class | 913 | 925 | 2581 | 910 |
| Greeting(name) | Idioma set | 946 | 973 | 1489 | 2052 |
| Greeting(name) | Idioma inline, in a field | 179 | 216 | 195 | 210 |
| Greeting(name) | Idioma inline, in markup | 813 | 900 | 811 | 836 |

The loop and the call cost: en 19.6 ns, nl 19.9 ns, en-GB 19.7 ns, de 19.8 ns.
