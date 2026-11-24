```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 28.84 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=1  WarmupCount=5  

```
| Method                   | Categories              | Culture | Mean       | Error     | StdDev    | Ratio | Gen0   | Allocated | 
|------------------------- |------------------------ |-------- |-----------:|----------:|----------:|------:|-------:|----------:|
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en**      |  **44.157 ns** | **0.1224 ns** | **0.1022 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en      |   3.378 ns | 0.0070 ns | 0.0058 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en      |   3.932 ns | 0.0358 ns | 0.0299 ns |  0.09 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en      |   5.682 ns | 0.0415 ns | 0.0368 ns |  0.13 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en-GB**   |  **43.830 ns** | **0.2352 ns** | **0.2085 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en-GB   |   3.380 ns | 0.0280 ns | 0.0262 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en-GB   |   3.906 ns | 0.0425 ns | 0.0398 ns |  0.09 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en-GB   |   5.664 ns | 0.0110 ns | 0.0103 ns |  0.13 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **nl**      |  **44.367 ns** | **0.3778 ns** | **0.3534 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | nl      |   3.348 ns | 0.0130 ns | 0.0102 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | nl      |   3.829 ns | 0.0087 ns | 0.0077 ns |  0.09 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | nl      |   5.625 ns | 0.0267 ns | 0.0250 ns |  0.13 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en**      |  **45.511 ns** | **0.1067 ns** | **0.0891 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | en      |   3.426 ns | 0.0171 ns | 0.0143 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en      |   3.946 ns | 0.0243 ns | 0.0227 ns |  0.09 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en      |   5.640 ns | 0.0496 ns | 0.0464 ns |  0.12 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en-GB**   | **139.780 ns** | **0.4889 ns** | **0.3817 ns** |  **1.00** | **0.0200** |     **168 B** | 
| IdiomaSet_Checkout       | Checkout                | en-GB   |   3.380 ns | 0.0242 ns | 0.0215 ns |  0.02 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en-GB   |   3.863 ns | 0.0092 ns | 0.0086 ns |  0.03 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en-GB   |   5.613 ns | 0.0261 ns | 0.0244 ns |  0.04 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **nl**      |  **45.763 ns** | **0.1246 ns** | **0.1040 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | nl      |   3.412 ns | 0.0208 ns | 0.0185 ns |  0.07 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | nl      |   3.910 ns | 0.0095 ns | 0.0079 ns |  0.09 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | nl      |   5.603 ns | 0.0157 ns | 0.0139 ns |  0.12 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en**      |  **66.347 ns** | **0.5192 ns** | **0.4603 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en      |   6.038 ns | 0.0248 ns | 0.0232 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en      |  15.711 ns | 0.1168 ns | 0.1093 ns |  0.24 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en-GB**   | **162.637 ns** | **0.6852 ns** | **0.6410 ns** |  **1.00** | **0.0296** |     **248 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en-GB   |   6.140 ns | 0.0454 ns | 0.0402 ns |  0.04 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en-GB   |  15.707 ns | 0.2257 ns | 0.2111 ns |  0.10 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **nl**      |  **67.866 ns** | **1.0027 ns** | **0.8373 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | nl      |   6.158 ns | 0.1315 ns | 0.1166 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | nl      |  15.436 ns | 0.0975 ns | 0.0912 ns |  0.23 | 0.0172 |     144 B | 
