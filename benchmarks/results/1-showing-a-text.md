```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 28.38 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=1  WarmupCount=5  

```
| Method                   | Categories              | Culture | Mean       | Error     | StdDev    | Ratio | Gen0   | Allocated | 
|------------------------- |------------------------ |-------- |-----------:|----------:|----------:|------:|-------:|----------:|
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **de**      |  **44.244 ns** | **0.3380 ns** | **0.3162 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | de      |   3.487 ns | 0.0265 ns | 0.0248 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | de      |   4.533 ns | 0.0107 ns | 0.0095 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | de      |   6.703 ns | 0.0262 ns | 0.0245 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en**      |  **43.013 ns** | **0.1166 ns** | **0.1034 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en      |   3.499 ns | 0.0222 ns | 0.0185 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en      |   4.595 ns | 0.0183 ns | 0.0153 ns |  0.11 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en      |   6.818 ns | 0.0113 ns | 0.0094 ns |  0.16 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en-GB**   |  **43.589 ns** | **0.2059 ns** | **0.1825 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en-GB   |   3.485 ns | 0.0291 ns | 0.0272 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en-GB   |   4.526 ns | 0.0035 ns | 0.0029 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en-GB   |   6.434 ns | 0.0174 ns | 0.0154 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **nl**      |  **44.303 ns** | **0.1020 ns** | **0.0796 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | nl      |   3.535 ns | 0.0140 ns | 0.0124 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | nl      |   4.657 ns | 0.0256 ns | 0.0227 ns |  0.11 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | nl      |   6.373 ns | 0.0362 ns | 0.0338 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **de**      |  **45.174 ns** | **0.1683 ns** | **0.1574 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | de      |   3.616 ns | 0.0296 ns | 0.0277 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | de      |   4.609 ns | 0.0228 ns | 0.0214 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | de      |   6.424 ns | 0.0559 ns | 0.0523 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en**      |  **44.549 ns** | **0.3274 ns** | **0.2902 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | en      |   3.449 ns | 0.0083 ns | 0.0073 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en      |   4.551 ns | 0.0298 ns | 0.0279 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en      |   6.408 ns | 0.0543 ns | 0.0508 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en-GB**   | **141.714 ns** | **0.6274 ns** | **0.5869 ns** |  **1.00** | **0.0200** |     **168 B** | 
| IdiomaSet_Checkout       | Checkout                | en-GB   |   3.699 ns | 0.0210 ns | 0.0196 ns |  0.03 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en-GB   |   4.653 ns | 0.0253 ns | 0.0236 ns |  0.03 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en-GB   |   6.745 ns | 0.0386 ns | 0.0342 ns |  0.05 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **nl**      |  **45.404 ns** | **0.1192 ns** | **0.0995 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | nl      |   3.519 ns | 0.0212 ns | 0.0198 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | nl      |   4.612 ns | 0.0189 ns | 0.0177 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | nl      |   6.462 ns | 0.0078 ns | 0.0061 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **de**      |  **67.737 ns** | **0.1807 ns** | **0.1602 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | de      |  49.547 ns | 0.1645 ns | 0.1459 ns |  0.73 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | de      |  16.039 ns | 0.0696 ns | 0.0617 ns |  0.24 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en**      |  **65.906 ns** | **0.2761 ns** | **0.2583 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en      |   6.194 ns | 0.0462 ns | 0.0433 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en      |  16.284 ns | 0.0823 ns | 0.0730 ns |  0.25 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en-GB**   | **162.629 ns** | **0.3057 ns** | **0.2710 ns** |  **1.00** | **0.0296** |     **248 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en-GB   |   6.253 ns | 0.0125 ns | 0.0111 ns |  0.04 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en-GB   |  16.182 ns | 0.1330 ns | 0.1244 ns |  0.10 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **nl**      |  **68.485 ns** | **0.4267 ns** | **0.3991 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | nl      |   6.216 ns | 0.0167 ns | 0.0148 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | nl      |  16.161 ns | 0.0928 ns | 0.0823 ns |  0.24 | 0.0172 |     144 B | 
