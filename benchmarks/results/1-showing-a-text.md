```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 31.78 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=1  WarmupCount=5  

```
| Method                   | Categories              | Culture | Mean       | Error     | StdDev    | Ratio | Gen0   | Allocated | 
|------------------------- |------------------------ |-------- |-----------:|----------:|----------:|------:|-------:|----------:|
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **de**      |  **43.720 ns** | **0.0733 ns** | **0.0612 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | de      |   3.519 ns | 0.0184 ns | 0.0163 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | de      |   4.590 ns | 0.0181 ns | 0.0160 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | de      |   6.431 ns | 0.0293 ns | 0.0274 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en**      |  **43.647 ns** | **0.1739 ns** | **0.1452 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en      |   3.467 ns | 0.0173 ns | 0.0162 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en      |   4.531 ns | 0.0091 ns | 0.0076 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en      |   6.546 ns | 0.0136 ns | 0.0106 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en-GB**   |  **44.709 ns** | **0.2249 ns** | **0.2104 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en-GB   |   3.513 ns | 0.0177 ns | 0.0147 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en-GB   |   4.553 ns | 0.0241 ns | 0.0225 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en-GB   |   6.411 ns | 0.0874 ns | 0.0730 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **nl**      |  **44.043 ns** | **0.1699 ns** | **0.1506 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | nl      |   3.508 ns | 0.0144 ns | 0.0120 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | nl      |   4.597 ns | 0.0099 ns | 0.0087 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | nl      |   6.428 ns | 0.0128 ns | 0.0100 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **de**      |  **45.447 ns** | **0.5018 ns** | **0.4694 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | de      |   3.555 ns | 0.0075 ns | 0.0062 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | de      |   4.555 ns | 0.0204 ns | 0.0180 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | de      |   7.138 ns | 0.0456 ns | 0.0427 ns |  0.16 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en**      |  **45.566 ns** | **0.1927 ns** | **0.1708 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | en      |   3.620 ns | 0.0232 ns | 0.0217 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en      |   4.571 ns | 0.0371 ns | 0.0347 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en      |   6.365 ns | 0.0295 ns | 0.0276 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en-GB**   | **139.887 ns** | **0.6186 ns** | **0.5786 ns** |  **1.00** | **0.0200** |     **168 B** | 
| IdiomaSet_Checkout       | Checkout                | en-GB   |   3.487 ns | 0.0276 ns | 0.0258 ns |  0.02 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en-GB   |   4.604 ns | 0.0118 ns | 0.0099 ns |  0.03 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en-GB   |   6.628 ns | 0.0307 ns | 0.0273 ns |  0.05 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **nl**      |  **46.069 ns** | **0.0957 ns** | **0.0799 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | nl      |   3.449 ns | 0.0060 ns | 0.0051 ns |  0.07 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | nl      |   4.947 ns | 0.0075 ns | 0.0062 ns |  0.11 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | nl      |   6.362 ns | 0.0322 ns | 0.0301 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **de**      |  **67.615 ns** | **0.2736 ns** | **0.2285 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | de      |  71.625 ns | 0.3769 ns | 0.3341 ns |  1.06 | 0.0162 |     136 B | 
| IdiomaInline_Greeting    | Greeting(name)          | de      |  16.207 ns | 0.0736 ns | 0.0652 ns |  0.24 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en**      |  **66.588 ns** | **0.1753 ns** | **0.1554 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en      |   6.208 ns | 0.0420 ns | 0.0393 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en      |  15.990 ns | 0.0580 ns | 0.0485 ns |  0.24 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en-GB**   | **161.045 ns** | **0.6699 ns** | **0.6266 ns** |  **1.00** | **0.0296** |     **248 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en-GB   |   6.292 ns | 0.0361 ns | 0.0338 ns |  0.04 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en-GB   |  16.687 ns | 0.1824 ns | 0.1706 ns |  0.10 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **nl**      |  **69.686 ns** | **0.3097 ns** | **0.2745 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | nl      |   6.400 ns | 0.0235 ns | 0.0220 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | nl      |  16.510 ns | 0.0666 ns | 0.0520 ns |  0.24 | 0.0172 |     144 B | 
