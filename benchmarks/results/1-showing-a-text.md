```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 30.17 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-ROROYX : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  IterationTime=100ms  LaunchCount=3  
WarmupCount=5  

```
| Method                   | Categories              | Culture | Mean       | Error     | StdDev    | Ratio | Gen0   | Allocated | 
|------------------------- |------------------------ |-------- |-----------:|----------:|----------:|------:|-------:|----------:|
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **de**      |  **45.356 ns** | **0.3756 ns** | **0.6869 ns** |  **1.00** | **0.0072** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | de      |   2.989 ns | 0.0322 ns | 0.0571 ns |  0.07 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | de      |   4.638 ns | 0.0219 ns | 0.0388 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | de      |   6.557 ns | 0.0313 ns | 0.0571 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en**      |  **44.545 ns** | **0.3037 ns** | **0.5629 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en      |   2.959 ns | 0.0302 ns | 0.0545 ns |  0.07 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en      |   4.624 ns | 0.0191 ns | 0.0339 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en      |   6.852 ns | 0.0964 ns | 0.1738 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en-GB**   |  **44.838 ns** | **0.2142 ns** | **0.3636 ns** |  **1.00** | **0.0082** |      **72 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en-GB   |   2.998 ns | 0.0389 ns | 0.0692 ns |  0.07 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en-GB   |   4.662 ns | 0.0214 ns | 0.0386 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en-GB   |   6.628 ns | 0.0298 ns | 0.0537 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **nl**      |  **45.184 ns** | **0.2856 ns** | **0.5365 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | nl      |   3.001 ns | 0.0160 ns | 0.0289 ns |  0.07 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | nl      |   4.651 ns | 0.0229 ns | 0.0414 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | nl      |   6.540 ns | 0.0312 ns | 0.0562 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **de**      |  **46.134 ns** | **0.2852 ns** | **0.4996 ns** |  **1.00** | **0.0083** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | de      |   2.945 ns | 0.0398 ns | 0.0748 ns |  0.06 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | de      |   4.680 ns | 0.0293 ns | 0.0528 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | de      |   6.759 ns | 0.1102 ns | 0.2015 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en**      |  **46.534 ns** | **0.1751 ns** | **0.2974 ns** |  **1.00** | **0.0085** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | en      |   3.021 ns | 0.0180 ns | 0.0320 ns |  0.06 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en      |   4.698 ns | 0.0267 ns | 0.0475 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en      |   6.631 ns | 0.0313 ns | 0.0572 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en-GB**   | **142.566 ns** | **0.8585 ns** | **1.5699 ns** |  **1.00** | **0.0198** |     **168 B** | 
| IdiomaSet_Checkout       | Checkout                | en-GB   |   2.995 ns | 0.0146 ns | 0.0259 ns |  0.02 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en-GB   |   4.653 ns | 0.0313 ns | 0.0557 ns |  0.03 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en-GB   |   6.544 ns | 0.0285 ns | 0.0514 ns |  0.05 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **nl**      |  **46.167 ns** | **0.2859 ns** | **0.5156 ns** |  **1.00** | **0.0084** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | nl      |   3.015 ns | 0.0257 ns | 0.0470 ns |  0.07 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | nl      |   4.724 ns | 0.0332 ns | 0.0623 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | nl      |   6.677 ns | 0.0504 ns | 0.0921 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **de**      |  **68.551 ns** | **0.5507 ns** | **0.9789 ns** |  **1.00** | **0.0176** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | de      |   6.097 ns | 0.0323 ns | 0.0591 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | de      |  16.640 ns | 0.1174 ns | 0.2176 ns |  0.24 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en**      |  **67.365 ns** | **0.4415 ns** | **0.7961 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en      |   6.112 ns | 0.0319 ns | 0.0600 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en      |  16.433 ns | 0.0898 ns | 0.1708 ns |  0.24 | 0.0171 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en-GB**   | **165.320 ns** | **1.1086 ns** | **2.0822 ns** |  **1.00** | **0.0280** |     **248 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en-GB   |   6.690 ns | 0.5694 ns | 1.0411 ns |  0.04 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en-GB   |  16.845 ns | 0.2695 ns | 0.4720 ns |  0.10 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **nl**      |  **70.923 ns** | **1.9415 ns** | **3.6466 ns** |  **1.00** | **0.0178** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | nl      |   6.146 ns | 0.0466 ns | 0.0864 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | nl      |  16.476 ns | 0.1617 ns | 0.2956 ns |  0.23 | 0.0171 |     144 B | 
