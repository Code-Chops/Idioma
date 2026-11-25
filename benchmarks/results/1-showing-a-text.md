```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 25.91 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=1  WarmupCount=5  

```
| Method                   | Categories              | Culture | Mean       | Error     | StdDev    | Ratio | Gen0   | Allocated | 
|------------------------- |------------------------ |-------- |-----------:|----------:|----------:|------:|-------:|----------:|
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **de**      |  **44.686 ns** | **0.2310 ns** | **0.2048 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | de      |   3.513 ns | 0.0275 ns | 0.0244 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | de      |   4.559 ns | 0.0221 ns | 0.0207 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | de      |   6.429 ns | 0.0463 ns | 0.0411 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en**      |  **43.820 ns** | **0.1199 ns** | **0.1063 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en      |   3.551 ns | 0.0241 ns | 0.0213 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en      |   4.655 ns | 0.0319 ns | 0.0299 ns |  0.11 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en      |   6.526 ns | 0.0453 ns | 0.0424 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en-GB**   |  **44.677 ns** | **0.2356 ns** | **0.2089 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en-GB   |   3.500 ns | 0.0177 ns | 0.0166 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en-GB   |   4.581 ns | 0.0264 ns | 0.0247 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en-GB   |   6.466 ns | 0.0245 ns | 0.0218 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **nl**      |  **44.582 ns** | **0.2061 ns** | **0.1721 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | nl      |   3.545 ns | 0.0221 ns | 0.0184 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | nl      |   4.768 ns | 0.0262 ns | 0.0232 ns |  0.11 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | nl      |   6.644 ns | 0.0324 ns | 0.0287 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **de**      |  **45.389 ns** | **0.1648 ns** | **0.1376 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | de      |   3.707 ns | 0.0217 ns | 0.0203 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | de      |   4.627 ns | 0.0199 ns | 0.0186 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | de      |   6.504 ns | 0.0532 ns | 0.0498 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en**      |  **45.470 ns** | **0.4135 ns** | **0.3665 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | en      |   3.528 ns | 0.0341 ns | 0.0319 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en      |   5.012 ns | 0.2681 ns | 0.2508 ns |  0.11 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en      |   7.045 ns | 0.3756 ns | 0.3513 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en-GB**   | **148.052 ns** | **2.4330 ns** | **2.2758 ns** |  **1.00** | **0.0200** |     **168 B** | 
| IdiomaSet_Checkout       | Checkout                | en-GB   |   3.755 ns | 0.0732 ns | 0.0612 ns |  0.03 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en-GB   |   4.977 ns | 0.2644 ns | 0.2344 ns |  0.03 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en-GB   |   6.842 ns | 0.2846 ns | 0.2376 ns |  0.05 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **nl**      |  **47.250 ns** | **0.5768 ns** | **0.5113 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | nl      |   3.882 ns | 0.1720 ns | 0.1525 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | nl      |   5.026 ns | 0.2667 ns | 0.2495 ns |  0.11 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | nl      |   6.754 ns | 0.0903 ns | 0.0800 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **de**      |  **72.274 ns** | **3.1025 ns** | **2.5907 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | de      |  50.848 ns | 0.9768 ns | 0.8659 ns |  0.70 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | de      |  17.610 ns | 0.5798 ns | 0.5140 ns |  0.24 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en**      |  **70.224 ns** | **1.5636 ns** | **1.3861 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en      |   6.347 ns | 0.0474 ns | 0.0444 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en      |  16.522 ns | 0.1570 ns | 0.1468 ns |  0.24 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en-GB**   | **164.701 ns** | **0.8062 ns** | **0.6732 ns** |  **1.00** | **0.0296** |     **248 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en-GB   |   6.250 ns | 0.0514 ns | 0.0455 ns |  0.04 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en-GB   |  16.275 ns | 0.0783 ns | 0.0654 ns |  0.10 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **nl**      |  **68.546 ns** | **0.6915 ns** | **0.6468 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | nl      |   6.242 ns | 0.0159 ns | 0.0133 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | nl      |  16.848 ns | 0.1665 ns | 0.1476 ns |  0.25 | 0.0172 |     144 B | 
