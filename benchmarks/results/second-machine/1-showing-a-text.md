```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 30.39 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=1  WarmupCount=5  

```
| Method                        | Categories              | Culture | Mean       | Error      | StdDev     | Ratio | Gen0   | Allocated | 
|------------------------------ |------------------------ |-------- |-----------:|-----------:|-----------:|------:|-------:|----------:|
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **en**      |  **45.880 ns** |  **1.1976 ns** |  **1.1202 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | en      |  15.150 ns |  0.3744 ns |  0.3319 ns |  0.33 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | en      |   9.940 ns |  0.2510 ns |  0.2348 ns |  0.22 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | en      |  12.475 ns |  0.1764 ns |  0.1650 ns |  0.27 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | en      |  11.809 ns |  0.2239 ns |  0.2094 ns |  0.26 |      - |         - | 
|                               |                         |         |            |            |            |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **en-GB**   |  **47.578 ns** |  **0.9346 ns** |  **0.8743 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | en-GB   |  27.138 ns |  0.5303 ns |  0.4960 ns |  0.57 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | en-GB   |  12.017 ns |  0.2615 ns |  0.2446 ns |  0.25 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | en-GB   |  14.959 ns |  0.3534 ns |  0.3133 ns |  0.31 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | en-GB   |  13.204 ns |  0.1265 ns |  0.1056 ns |  0.28 |      - |         - | 
|                               |                         |         |            |            |            |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **nl**      |  **46.932 ns** |  **1.1190 ns** |  **1.0467 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | nl      |  14.837 ns |  0.3331 ns |  0.3116 ns |  0.32 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | nl      |  12.332 ns |  0.3405 ns |  0.3185 ns |  0.26 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | nl      |  14.915 ns |  0.2925 ns |  0.2736 ns |  0.32 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | nl      |  13.486 ns |  0.3164 ns |  0.2960 ns |  0.29 |      - |         - | 
|                               |                         |         |            |            |            |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **en**      |  **47.238 ns** |  **1.2527 ns** |  **1.1105 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout            | Checkout                | en      |  16.979 ns |  0.3582 ns |  0.3175 ns |  0.36 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | en      |   9.755 ns |  0.2020 ns |  0.1791 ns |  0.21 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | en      |  12.617 ns |  0.3011 ns |  0.2816 ns |  0.27 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | en      |  11.541 ns |  0.2026 ns |  0.1895 ns |  0.24 |      - |         - | 
|                               |                         |         |            |            |            |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **en-GB**   | **147.892 ns** |  **2.5911 ns** |  **2.2970 ns** |  **1.00** | **0.0200** |     **168 B** | 
| IdiomaSet_Checkout            | Checkout                | en-GB   |  28.097 ns |  0.4184 ns |  0.3914 ns |  0.19 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | en-GB   |  12.412 ns |  0.2899 ns |  0.2712 ns |  0.08 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | en-GB   |  14.570 ns |  0.3661 ns |  0.3425 ns |  0.10 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | en-GB   |  12.965 ns |  0.3593 ns |  0.3185 ns |  0.09 |      - |         - | 
|                               |                         |         |            |            |            |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **nl**      |  **49.051 ns** |  **1.3646 ns** |  **1.2097 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout            | Checkout                | nl      |  14.864 ns |  0.2535 ns |  0.2371 ns |  0.30 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | nl      |  16.145 ns |  2.6636 ns |  2.4915 ns |  0.33 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | nl      |  18.546 ns |  2.2990 ns |  2.1505 ns |  0.38 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | nl      |  23.087 ns |  2.6999 ns |  2.5255 ns |  0.47 |      - |         - | 
|                               |                         |         |            |            |            |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **en**      | **107.819 ns** | **12.2924 ns** | **11.4983 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | en      |  52.459 ns | 10.1209 ns |  9.4671 ns |  0.49 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | en      |  15.676 ns |  3.0037 ns |  2.8096 ns |  0.15 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | en      |  43.825 ns |  4.5504 ns |  4.0338 ns |  0.41 | 0.0315 |     264 B | 
|                               |                         |         |            |            |            |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **en-GB**   | **198.550 ns** | **16.8291 ns** | **15.7420 ns** |  **1.00** | **0.0296** |     **248 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | en-GB   |  78.332 ns |  9.4764 ns |  8.8643 ns |  0.40 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | en-GB   |  15.613 ns |  3.2436 ns |  3.0340 ns |  0.08 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | en-GB   |  61.459 ns |  8.4372 ns |  7.8922 ns |  0.31 | 0.0315 |     264 B | 
|                               |                         |         |            |            |            |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **nl**      | **103.224 ns** | **19.4458 ns** | **18.1896 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | nl      |  62.663 ns |  9.3497 ns |  8.7457 ns |  0.63 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | nl      |  13.150 ns |  1.9894 ns |  1.6612 ns |  0.13 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | nl      |  37.873 ns |  0.9664 ns |  0.9039 ns |  0.38 | 0.0315 |     264 B | 
