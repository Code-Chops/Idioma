Blazor WebAssembly, Mono interpreter: .NET 11.0.0, in Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/158.0.0.0 Safari/537.36.

Showing one text, in nanoseconds, after the cost of the loop and the call is taken off:

| Text | Approach | en | nl | en-GB | de |
|---|---|---:|---:|---:|---:|
| Checkout | IStringLocalizer | 933 | 945 | 2658 | 961 |
| Checkout | Idioma set | 422 | 419 | 971 | 460 |
| Checkout | Idioma inline, in a field | 188 | 221 | 197 | 214 |
| Checkout | Idioma inline, in markup | 305 | 344 | 324 | 337 |
| Checkout | Idioma attribute | 256 | 283 | 263 | 275 |
| Cart | IStringLocalizer | 914 | 955 | 906 | 912 |
| Cart | Idioma set | 410 | 420 | 841 | 463 |
| Cart | Idioma inline, in a field | 179 | 217 | 197 | 214 |
| Cart | Idioma inline, in markup | 308 | 352 | 324 | 332 |
| Cart | Idioma attribute | 246 | 282 | 262 | 273 |
| Greeting(name) | IStringLocalizer | 1547 | 1547 | 3177 | 1565 |
| Greeting(name) | Idioma set | 1013 | 964 | 1808 | 2074 |
| Greeting(name) | Idioma inline, in a field | 185 | 219 | 196 | 215 |
| Greeting(name) | Idioma inline, in markup | 836 | 825 | 941 | 839 |

The loop and the call cost: en 19.8 ns, nl 19.9 ns, en-GB 19.8 ns, de 19.6 ns.
