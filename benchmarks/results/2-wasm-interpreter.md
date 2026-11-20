Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 922 | 960 | 2639 | 934 |
| Checkout | Idioma set | 410 | 412 | 988 | 456 |
| Checkout | Idioma inline, in a field | 181 | 216 | 197 | 210 |
| Checkout | Idioma inline, in markup | 300 | 338 | 325 | 333 |
| Cart | IStringLocalizer | 915 | 930 | 933 | 926 |
| Cart | Idioma set | 415 | 413 | 858 | 459 |
| Cart | Idioma inline, in a field | 182 | 214 | 196 | 210 |
| Cart | Idioma inline, in markup | 299 | 337 | 324 | 334 |
| Greeting(name) | IStringLocalizer | 1475 | 1497 | 3152 | 1477 |
| Greeting(name) | Idioma set | 974 | 974 | 1770 | 2068 |
| Greeting(name) | Idioma inline, in a field | 180 | 217 | 196 | 213 |
| Greeting(name) | Idioma inline, in markup | 829 | 825 | 815 | 848 |

The loop and the call cost: en 19.3 ns, nl 19.2 ns, en-GB 19.6 ns, de 19.6 ns.
