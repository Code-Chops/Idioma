```

BenchmarkDotNet v0.16.0-preview.2, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
Memory: 64 GB Total, 0.33 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a

IterationCount=15  LaunchCount=1  WarmupCount=5  

```
| Method                        | Categories              | Culture | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | 
|------------------------------ |------------------------ |-------- |----------:|---------:|---------:|------:|-------:|----------:|
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **de**      |  **39.34 ns** | **0.174 ns** | **0.145 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | de      |  22.28 ns | 0.156 ns | 0.146 ns |  0.57 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | de      |  15.22 ns | 0.085 ns | 0.079 ns |  0.39 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | de      |  18.81 ns | 0.076 ns | 0.067 ns |  0.48 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | de      |  16.55 ns | 0.087 ns | 0.082 ns |  0.42 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **en**      |  **38.14 ns** | **0.153 ns** | **0.128 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | en      |  15.94 ns | 0.058 ns | 0.048 ns |  0.42 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | en      |  11.66 ns | 0.044 ns | 0.039 ns |  0.31 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | en      |  15.30 ns | 0.109 ns | 0.102 ns |  0.40 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | en      |  13.11 ns | 0.050 ns | 0.047 ns |  0.34 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **en-GB**   |  **39.17 ns** | **0.148 ns** | **0.124 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | en-GB   |  29.25 ns | 0.079 ns | 0.074 ns |  0.75 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | en-GB   |  13.56 ns | 0.044 ns | 0.039 ns |  0.35 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | en-GB   |  17.68 ns | 0.200 ns | 0.177 ns |  0.45 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | en-GB   |  15.16 ns | 0.058 ns | 0.054 ns |  0.39 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **nl**      |  **39.74 ns** | **0.264 ns** | **0.247 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | nl      |  16.30 ns | 0.092 ns | 0.086 ns |  0.41 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | nl      |  13.84 ns | 0.075 ns | 0.070 ns |  0.35 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | nl      |  18.03 ns | 0.344 ns | 0.305 ns |  0.45 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | nl      |  15.35 ns | 0.086 ns | 0.076 ns |  0.39 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **de**      |  **44.39 ns** | **0.187 ns** | **0.166 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout            | Checkout                | de      |  22.52 ns | 0.217 ns | 0.203 ns |  0.51 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | de      |  15.35 ns | 0.112 ns | 0.104 ns |  0.35 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | de      |  18.79 ns | 0.061 ns | 0.054 ns |  0.42 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | de      |  16.81 ns | 0.064 ns | 0.060 ns |  0.38 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **en**      |  **41.65 ns** | **0.337 ns** | **0.298 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout            | Checkout                | en      |  16.18 ns | 0.086 ns | 0.080 ns |  0.39 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | en      |  11.78 ns | 0.023 ns | 0.019 ns |  0.28 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | en      |  15.44 ns | 0.089 ns | 0.083 ns |  0.37 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | en      |  13.43 ns | 0.041 ns | 0.034 ns |  0.32 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **en-GB**   | **136.93 ns** | **1.881 ns** | **1.759 ns** |  **1.00** | **0.0200** |     **168 B** | 
| IdiomaSet_Checkout            | Checkout                | en-GB   |  35.94 ns | 0.376 ns | 0.352 ns |  0.26 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | en-GB   |  13.61 ns | 0.046 ns | 0.041 ns |  0.10 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | en-GB   |  17.15 ns | 0.065 ns | 0.060 ns |  0.13 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | en-GB   |  15.14 ns | 0.046 ns | 0.038 ns |  0.11 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **nl**      |  **42.52 ns** | **0.139 ns** | **0.116 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout            | Checkout                | nl      |  16.06 ns | 0.061 ns | 0.057 ns |  0.38 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | nl      |  13.89 ns | 0.107 ns | 0.100 ns |  0.33 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | nl      |  17.58 ns | 0.157 ns | 0.147 ns |  0.41 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | nl      |  15.55 ns | 0.149 ns | 0.139 ns |  0.37 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **de**      |  **79.04 ns** | **0.991 ns** | **0.878 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | de      | 139.82 ns | 0.903 ns | 0.800 ns |  1.77 | 0.0305 |     256 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | de      |  15.05 ns | 0.049 ns | 0.044 ns |  0.19 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | de      |  55.01 ns | 0.265 ns | 0.222 ns |  0.70 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **en**      |  **77.54 ns** | **0.833 ns** | **0.779 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | en      |  56.12 ns | 1.062 ns | 0.941 ns |  0.72 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | en      |  11.77 ns | 0.024 ns | 0.018 ns |  0.15 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | en      |  53.00 ns | 1.144 ns | 1.070 ns |  0.68 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **en-GB**   | **183.49 ns** | **1.404 ns** | **1.244 ns** |  **1.00** | **0.0296** |     **248 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | en-GB   |  82.38 ns | 0.630 ns | 0.589 ns |  0.45 | 0.0257 |     216 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | en-GB   |  13.69 ns | 0.118 ns | 0.110 ns |  0.07 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | en-GB   |  53.76 ns | 0.304 ns | 0.284 ns |  0.29 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **nl**      |  **79.97 ns** | **0.341 ns** | **0.285 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | nl      |  55.34 ns | 0.448 ns | 0.419 ns |  0.69 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | nl      |  13.82 ns | 0.176 ns | 0.156 ns |  0.17 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | nl      |  54.72 ns | 0.819 ns | 0.766 ns |  0.68 | 0.0315 |     264 B | 
