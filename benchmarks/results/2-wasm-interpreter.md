Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 979 | 995 | 2724 | 1012 |
| Checkout | Idioma set | 401 | 401 | 899 | 451 |
| Checkout | Idioma inline, in a field | 193 | 225 | 204 | 226 |
| Checkout | Idioma inline, in markup | 327 | 363 | 345 | 365 |
| Checkout | Idioma attribute | 260 | 297 | 272 | 294 |
| Cart | IStringLocalizer | 961 | 982 | 968 | 988 |
| Cart | Idioma set | 401 | 406 | 870 | 450 |
| Cart | Idioma inline, in a field | 193 | 227 | 208 | 230 |
| Cart | Idioma inline, in markup | 326 | 365 | 346 | 358 |
| Cart | Idioma attribute | 261 | 298 | 277 | 295 |
| Greeting(name) | IStringLocalizer | 1546 | 1574 | 3319 | 1582 |
| Greeting(name) | Idioma set | 981 | 994 | 1552 | 2196 |
| Greeting(name) | Idioma inline, in a field | 194 | 228 | 209 | 233 |
| Greeting(name) | Idioma inline, in markup | 844 | 881 | 850 | 902 |

The loop and the call cost: en 20.5 ns, nl 20.5 ns, en-GB 20.6 ns, de 20.4 ns.
