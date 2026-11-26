```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 29.83 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-ROROYX : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  IterationTime=100ms  LaunchCount=3  
WarmupCount=5  

```
| Method                   | Categories              | Culture | Mean       | Error     | StdDev     | Ratio | Gen0   | Allocated | 
|------------------------- |------------------------ |-------- |-----------:|----------:|-----------:|------:|-------:|----------:|
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **de**      |  **45.053 ns** | **0.2973 ns** |  **0.5284 ns** |  **1.00** | **0.0072** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | de      |   3.366 ns | 0.0177 ns |  0.0329 ns |  0.07 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | de      |   4.708 ns | 0.0745 ns |  0.1363 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | de      |   6.434 ns | 0.0320 ns |  0.0578 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en**      |  **44.034 ns** | **0.3453 ns** |  **0.6315 ns** |  **1.00** | **0.0074** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en      |   3.338 ns | 0.0181 ns |  0.0321 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en      |   4.614 ns | 0.0225 ns |  0.0388 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en      |   6.521 ns | 0.0404 ns |  0.0719 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en-GB**   |  **45.011 ns** | **0.2273 ns** |  **0.4156 ns** |  **1.00** | **0.0085** |      **72 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en-GB   |   3.369 ns | 0.0144 ns |  0.0260 ns |  0.07 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en-GB   |   4.725 ns | 0.0647 ns |  0.1198 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en-GB   |   6.500 ns | 0.0408 ns |  0.0736 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **nl**      |  **44.718 ns** | **0.3104 ns** |  **0.5676 ns** |  **1.00** | **0.0075** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | nl      |   3.366 ns | 0.0164 ns |  0.0299 ns |  0.08 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | nl      |   4.579 ns | 0.0176 ns |  0.0318 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | nl      |   6.570 ns | 0.1170 ns |  0.2079 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **de**      |  **45.965 ns** | **0.2148 ns** |  **0.3927 ns** |  **1.00** | **0.0083** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | de      |   3.347 ns | 0.0192 ns |  0.0341 ns |  0.07 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | de      |   4.662 ns | 0.0553 ns |  0.0983 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | de      |   6.474 ns | 0.0361 ns |  0.0651 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en**      |  **46.235 ns** | **0.2110 ns** |  **0.3858 ns** |  **1.00** | **0.0083** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | en      |   3.399 ns | 0.0152 ns |  0.0282 ns |  0.07 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en      |   4.733 ns | 0.0913 ns |  0.1692 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en      |   6.580 ns | 0.1048 ns |  0.1863 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en-GB**   | **148.232 ns** | **5.8876 ns** | **10.7659 ns** |  **1.00** | **0.0196** |     **168 B** | 
| IdiomaSet_Checkout       | Checkout                | en-GB   |   3.510 ns | 0.1241 ns |  0.2237 ns |  0.02 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en-GB   |   4.674 ns | 0.0737 ns |  0.1329 ns |  0.03 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en-GB   |   6.624 ns | 0.1533 ns |  0.2764 ns |  0.04 |      - |         - | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **nl**      |  **45.739 ns** | **0.2117 ns** |  **0.3817 ns** |  **1.00** | **0.0083** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | nl      |   3.548 ns | 0.1244 ns |  0.2211 ns |  0.08 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | nl      |   4.650 ns | 0.0262 ns |  0.0472 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | nl      |   6.533 ns | 0.0355 ns |  0.0649 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **de**      |  **69.534 ns** | **0.7420 ns** |  **1.3379 ns** |  **1.00** | **0.0180** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | de      |  46.891 ns | 0.3048 ns |  0.5418 ns |  0.67 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | de      |  16.495 ns | 0.0881 ns |  0.1675 ns |  0.24 | 0.0171 |     144 B | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en**      |  **67.697 ns** | **0.5653 ns** |  **1.0194 ns** |  **1.00** | **0.0178** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en      |   6.364 ns | 0.0455 ns |  0.0844 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en      |  16.316 ns | 0.1164 ns |  0.2214 ns |  0.24 | 0.0172 |     144 B | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en-GB**   | **164.109 ns** | **1.2776 ns** |  **2.4308 ns** |  **1.00** | **0.0294** |     **248 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en-GB   |   6.404 ns | 0.0436 ns |  0.0820 ns |  0.04 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en-GB   |  16.401 ns | 0.0992 ns |  0.1840 ns |  0.10 | 0.0172 |     144 B | 
|                          |                         |         |            |           |            |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **nl**      |  **69.161 ns** | **0.6227 ns** |  **1.1069 ns** |  **1.00** | **0.0178** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | nl      |   6.396 ns | 0.0211 ns |  0.0385 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | nl      |  16.763 ns | 0.2184 ns |  0.4047 ns |  0.24 | 0.0171 |     144 B | 
