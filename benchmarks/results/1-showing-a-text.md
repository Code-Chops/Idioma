```

BenchmarkDotNet v0.16.0-preview.2, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
Memory: 64 GB Total, 2.36 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a

IterationCount=15  LaunchCount=1  WarmupCount=5  

```
| Method                        | Categories              | Culture | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | 
|------------------------------ |------------------------ |-------- |----------:|---------:|---------:|------:|-------:|----------:|
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **de**      |  **39.69 ns** | **0.212 ns** | **0.199 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | de      |  22.08 ns | 0.202 ns | 0.189 ns |  0.56 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | de      |  14.99 ns | 0.121 ns | 0.107 ns |  0.38 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | de      |  18.61 ns | 0.061 ns | 0.051 ns |  0.47 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **en**      |  **38.11 ns** | **0.164 ns** | **0.154 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | en      |  16.48 ns | 0.320 ns | 0.283 ns |  0.43 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | en      |  11.60 ns | 0.027 ns | 0.024 ns |  0.30 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | en      |  15.13 ns | 0.041 ns | 0.038 ns |  0.40 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **en-GB**   |  **38.94 ns** | **0.197 ns** | **0.184 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | en-GB   |  28.99 ns | 0.171 ns | 0.151 ns |  0.74 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | en-GB   |  13.47 ns | 0.028 ns | 0.026 ns |  0.35 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | en-GB   |  17.24 ns | 0.066 ns | 0.062 ns |  0.44 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **nl**      |  **39.48 ns** | **0.152 ns** | **0.127 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | nl      |  15.94 ns | 0.060 ns | 0.056 ns |  0.40 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | nl      |  13.66 ns | 0.057 ns | 0.048 ns |  0.35 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | nl      |  17.34 ns | 0.119 ns | 0.106 ns |  0.44 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **de**      |  **43.20 ns** | **0.590 ns** | **0.552 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout            | Checkout                | de      |  22.02 ns | 0.175 ns | 0.155 ns |  0.51 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | de      |  15.20 ns | 0.138 ns | 0.107 ns |  0.35 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | de      |  18.49 ns | 0.077 ns | 0.060 ns |  0.43 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **en**      |  **45.87 ns** | **0.514 ns** | **0.455 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout            | Checkout                | en      |  16.30 ns | 0.355 ns | 0.332 ns |  0.36 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | en      |  11.68 ns | 0.044 ns | 0.039 ns |  0.25 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | en      |  15.21 ns | 0.081 ns | 0.071 ns |  0.33 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **en-GB**   | **132.73 ns** | **0.520 ns** | **0.486 ns** |  **1.00** | **0.0200** |     **168 B** | 
| IdiomaSet_Checkout            | Checkout                | en-GB   |  34.54 ns | 0.184 ns | 0.172 ns |  0.26 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | en-GB   |  13.46 ns | 0.044 ns | 0.039 ns |  0.10 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | en-GB   |  17.00 ns | 0.062 ns | 0.055 ns |  0.13 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **nl**      |  **44.06 ns** | **0.126 ns** | **0.112 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout            | Checkout                | nl      |  15.97 ns | 0.102 ns | 0.096 ns |  0.36 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | nl      |  13.78 ns | 0.100 ns | 0.088 ns |  0.31 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | nl      |  17.24 ns | 0.043 ns | 0.039 ns |  0.39 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **de**      |  **77.39 ns** | **0.363 ns** | **0.340 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | de      | 140.44 ns | 0.429 ns | 0.380 ns |  1.81 | 0.0305 |     256 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | de      |  15.07 ns | 0.059 ns | 0.055 ns |  0.19 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | de      |  55.18 ns | 0.095 ns | 0.085 ns |  0.71 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **en**      |  **75.42 ns** | **0.357 ns** | **0.298 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | en      |  55.40 ns | 0.454 ns | 0.380 ns |  0.73 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | en      |  11.81 ns | 0.088 ns | 0.083 ns |  0.16 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | en      |  51.76 ns | 0.136 ns | 0.113 ns |  0.69 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **en-GB**   | **174.62 ns** | **4.352 ns** | **3.858 ns** |  **1.00** | **0.0296** |     **248 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | en-GB   |  82.66 ns | 1.963 ns | 1.639 ns |  0.47 | 0.0257 |     216 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | en-GB   |  13.64 ns | 0.128 ns | 0.113 ns |  0.08 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | en-GB   |  53.90 ns | 0.162 ns | 0.152 ns |  0.31 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **nl**      |  **78.24 ns** | **0.231 ns** | **0.205 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | nl      |  55.16 ns | 0.092 ns | 0.082 ns |  0.71 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | nl      |  13.86 ns | 0.036 ns | 0.030 ns |  0.18 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | nl      |  54.14 ns | 0.158 ns | 0.148 ns |  0.69 | 0.0315 |     264 B | 
