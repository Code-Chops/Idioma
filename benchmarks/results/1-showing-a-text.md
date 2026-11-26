```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 26.44 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-LVEFGF : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=3  WarmupCount=5  

```
| Method                   | Categories              | Culture | Mean       | Error     | StdDev    | Ratio | Gen0   | Allocated | 
|------------------------- |------------------------ |-------- |-----------:|----------:|----------:|------:|-------:|----------:|
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **de**      |  **44.637 ns** | **0.2091 ns** | **0.3824 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | de      |   3.537 ns | 0.0200 ns | 0.0370 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | de      |   4.627 ns | 0.0246 ns | 0.0450 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | de      |   6.434 ns | 0.0258 ns | 0.0466 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en**      |  **44.436 ns** | **0.2081 ns** | **0.3858 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en      |   3.526 ns | 0.0186 ns | 0.0344 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en      |   4.641 ns | 0.0111 ns | 0.0208 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en      |   6.645 ns | 0.1008 ns | 0.1844 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en-GB**   |  **44.755 ns** | **0.1637 ns** | **0.3035 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en-GB   |   3.518 ns | 0.0121 ns | 0.0224 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en-GB   |   4.736 ns | 0.0498 ns | 0.0935 ns |  0.11 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en-GB   |   6.458 ns | 0.0266 ns | 0.0493 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **nl**      |  **44.902 ns** | **0.1724 ns** | **0.3195 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | nl      |   3.514 ns | 0.0121 ns | 0.0225 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | nl      |   4.670 ns | 0.0130 ns | 0.0240 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | nl      |   6.493 ns | 0.0362 ns | 0.0671 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **de**      |  **46.273 ns** | **0.1874 ns** | **0.3519 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | de      |   3.672 ns | 0.0253 ns | 0.0476 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | de      |   4.777 ns | 0.1104 ns | 0.2019 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | de      |   6.437 ns | 0.0263 ns | 0.0468 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en**      |  **45.776 ns** | **0.1460 ns** | **0.2595 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | en      |   3.711 ns | 0.1016 ns | 0.1833 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en      |   4.655 ns | 0.0273 ns | 0.0506 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en      |   6.482 ns | 0.0330 ns | 0.0621 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en-GB**   | **141.913 ns** | **0.6712 ns** | **1.1756 ns** |  **1.00** | **0.0200** |     **168 B** | 
| IdiomaSet_Checkout       | Checkout                | en-GB   |   3.638 ns | 0.0315 ns | 0.0568 ns |  0.03 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en-GB   |   4.602 ns | 0.0135 ns | 0.0257 ns |  0.03 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en-GB   |   6.535 ns | 0.0317 ns | 0.0596 ns |  0.05 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **nl**      |  **45.767 ns** | **0.1627 ns** | **0.3015 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | nl      |   3.604 ns | 0.0274 ns | 0.0500 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | nl      |   4.598 ns | 0.0138 ns | 0.0248 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | nl      |   6.506 ns | 0.0190 ns | 0.0356 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **de**      |  **67.978 ns** | **0.3263 ns** | **0.6209 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | de      |  49.122 ns | 0.2452 ns | 0.4484 ns |  0.72 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | de      |  16.261 ns | 0.1023 ns | 0.1871 ns |  0.24 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en**      |  **67.954 ns** | **0.3938 ns** | **0.7493 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en      |   6.332 ns | 0.0684 ns | 0.1285 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en      |  17.151 ns | 0.5246 ns | 0.9854 ns |  0.25 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en-GB**   | **163.877 ns** | **0.3778 ns** | **0.7003 ns** |  **1.00** | **0.0296** |     **248 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en-GB   |   7.024 ns | 0.3126 ns | 0.5872 ns |  0.04 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en-GB   |  17.613 ns | 0.3738 ns | 0.6741 ns |  0.11 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **nl**      |  **73.900 ns** | **1.4298 ns** | **2.6855 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | nl      |   6.595 ns | 0.0654 ns | 0.1180 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | nl      |  17.467 ns | 0.2357 ns | 0.4189 ns |  0.24 | 0.0172 |     144 B | 
