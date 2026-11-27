```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 30.4 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-ROROYX : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  IterationTime=100ms  LaunchCount=3  
WarmupCount=5  

```
| Method                   | Categories              | Culture | Mean       | Error     | StdDev    | Ratio | Gen0   | Allocated | 
|------------------------- |------------------------ |-------- |-----------:|----------:|----------:|------:|-------:|----------:|
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **de**      |  **45.166 ns** | **0.4798 ns** | **0.8893 ns** |  **1.00** | **0.0075** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | de      |   3.277 ns | 0.0231 ns | 0.0405 ns |  0.07 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | de      |   4.546 ns | 0.0354 ns | 0.0648 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | de      |   6.674 ns | 0.2005 ns | 0.3564 ns |  0.15 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en**      |  **44.940 ns** | **0.4867 ns** | **0.8777 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en      |   3.307 ns | 0.0539 ns | 0.0944 ns |  0.07 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en      |   4.605 ns | 0.0584 ns | 0.1054 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en      |   6.342 ns | 0.0390 ns | 0.0713 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **en-GB**   |  **44.655 ns** | **0.3191 ns** | **0.5835 ns** |  **1.00** | **0.0084** |      **72 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | en-GB   |   3.239 ns | 0.0199 ns | 0.0354 ns |  0.07 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | en-GB   |   4.464 ns | 0.0149 ns | 0.0272 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | en-GB   |   6.336 ns | 0.0362 ns | 0.0644 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Cart**           | **Cart (en-GB rewords it)** | **nl**      |  **45.077 ns** | **0.2682 ns** | **0.4697 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart           | Cart (en-GB rewords it) | nl      |   3.284 ns | 0.0125 ns | 0.0226 ns |  0.07 |      - |         - | 
| IdiomaInline_Cart        | Cart (en-GB rewords it) | nl      |   4.506 ns | 0.0169 ns | 0.0300 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Cart     | Cart (en-GB rewords it) | nl      |   6.418 ns | 0.0315 ns | 0.0575 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **de**      |  **46.463 ns** | **0.2295 ns** | **0.4138 ns** |  **1.00** | **0.0083** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | de      |   3.311 ns | 0.0563 ns | 0.1002 ns |  0.07 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | de      |   4.543 ns | 0.0407 ns | 0.0743 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | de      |   6.353 ns | 0.0190 ns | 0.0327 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en**      |  **46.117 ns** | **0.3068 ns** | **0.5532 ns** |  **1.00** | **0.0083** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | en      |   3.273 ns | 0.0521 ns | 0.0927 ns |  0.07 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en      |   4.549 ns | 0.0454 ns | 0.0808 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en      |   6.418 ns | 0.0351 ns | 0.0633 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **en-GB**   | **144.407 ns** | **1.0624 ns** | **1.9158 ns** |  **1.00** | **0.0200** |     **168 B** | 
| IdiomaSet_Checkout       | Checkout                | en-GB   |   3.254 ns | 0.0194 ns | 0.0349 ns |  0.02 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | en-GB   |   4.592 ns | 0.0532 ns | 0.0999 ns |  0.03 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | en-GB   |   6.543 ns | 0.1235 ns | 0.2195 ns |  0.05 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Checkout**       | **Checkout**                | **nl**      |  **47.100 ns** | **0.4656 ns** | **0.8396 ns** |  **1.00** | **0.0084** |      **72 B** | 
| IdiomaSet_Checkout       | Checkout                | nl      |   3.230 ns | 0.0226 ns | 0.0414 ns |  0.07 |      - |         - | 
| IdiomaInline_Checkout    | Checkout                | nl      |   4.599 ns | 0.1059 ns | 0.1937 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout | Checkout                | nl      |   6.458 ns | 0.0457 ns | 0.0824 ns |  0.14 |      - |         - | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **de**      |  **69.496 ns** | **0.7064 ns** | **1.2917 ns** |  **1.00** | **0.0177** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | de      |  41.341 ns | 0.4126 ns | 0.7647 ns |  0.60 | 0.0054 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | de      |  17.418 ns | 0.6484 ns | 1.1692 ns |  0.25 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en**      |  **68.304 ns** | **0.6233 ns** | **1.1239 ns** |  **1.00** | **0.0176** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en      |   6.218 ns | 0.0428 ns | 0.0738 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en      |  16.263 ns | 0.0820 ns | 0.1539 ns |  0.24 | 0.0171 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **en-GB**   | **164.881 ns** | **0.7373 ns** | **1.2717 ns** |  **1.00** | **0.0285** |     **248 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | en-GB   |   6.161 ns | 0.0271 ns | 0.0509 ns |  0.04 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | en-GB   |  16.321 ns | 0.1403 ns | 0.2635 ns |  0.10 | 0.0172 |     144 B | 
|                          |                         |         |            |           |           |       |        |           | 
| **Localizer_Greeting**       | **Greeting(name)**          | **nl**      |  **68.480 ns** | **0.5635 ns** | **0.9721 ns** |  **1.00** | **0.0176** |     **152 B** | 
| IdiomaSet_Greeting       | Greeting(name)          | nl      |   6.103 ns | 0.0240 ns | 0.0456 ns |  0.09 | 0.0057 |      48 B | 
| IdiomaInline_Greeting    | Greeting(name)          | nl      |  16.154 ns | 0.0960 ns | 0.1803 ns |  0.24 | 0.0171 |     144 B | 
