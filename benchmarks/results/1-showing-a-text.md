```

BenchmarkDotNet v0.16.0-preview.2, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
Memory: 64 GB Total, 0.55 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a

IterationCount=15  LaunchCount=1  WarmupCount=5  

```
| Method                        | Categories              | Culture | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | 
|------------------------------ |------------------------ |-------- |----------:|---------:|---------:|------:|-------:|----------:|
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **en**      |  **39.54 ns** | **0.312 ns** | **0.277 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | en      |  16.84 ns | 0.165 ns | 0.155 ns |  0.43 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | en      |  11.97 ns | 0.093 ns | 0.083 ns |  0.30 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | en      |  15.27 ns | 0.082 ns | 0.073 ns |  0.39 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | en      |  13.13 ns | 0.058 ns | 0.054 ns |  0.33 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **en-GB**   |  **39.41 ns** | **0.470 ns** | **0.439 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | en-GB   |  31.02 ns | 0.229 ns | 0.203 ns |  0.79 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | en-GB   |  13.63 ns | 0.064 ns | 0.057 ns |  0.35 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | en-GB   |  17.55 ns | 0.069 ns | 0.064 ns |  0.45 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | en-GB   |  15.63 ns | 0.184 ns | 0.172 ns |  0.40 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **nl**      |  **40.93 ns** | **0.351 ns** | **0.329 ns** |  **1.00** | **0.0076** |      **64 B** | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | nl      |  16.59 ns | 0.046 ns | 0.041 ns |  0.41 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | nl      |  14.07 ns | 0.122 ns | 0.109 ns |  0.34 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | nl      |  17.75 ns | 0.122 ns | 0.114 ns |  0.43 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | nl      |  15.65 ns | 0.062 ns | 0.055 ns |  0.38 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **en**      |  **43.16 ns** | **0.228 ns** | **0.202 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout            | Checkout                | en      |  16.53 ns | 0.132 ns | 0.124 ns |  0.38 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | en      |  11.79 ns | 0.086 ns | 0.081 ns |  0.27 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | en      |  15.29 ns | 0.061 ns | 0.055 ns |  0.35 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | en      |  13.32 ns | 0.080 ns | 0.075 ns |  0.31 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **en-GB**   | **136.29 ns** | **1.379 ns** | **1.223 ns** |  **1.00** | **0.0200** |     **168 B** | 
| IdiomaSet_Checkout            | Checkout                | en-GB   |  33.45 ns | 0.208 ns | 0.195 ns |  0.25 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | en-GB   |  13.74 ns | 0.150 ns | 0.140 ns |  0.10 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | en-GB   |  17.35 ns | 0.086 ns | 0.076 ns |  0.13 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | en-GB   |  15.32 ns | 0.061 ns | 0.051 ns |  0.11 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **nl**      |  **43.78 ns** | **0.235 ns** | **0.220 ns** |  **1.00** | **0.0086** |      **72 B** | 
| IdiomaSet_Checkout            | Checkout                | nl      |  16.48 ns | 0.066 ns | 0.059 ns |  0.38 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | nl      |  14.00 ns | 0.053 ns | 0.047 ns |  0.32 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | nl      |  17.55 ns | 0.086 ns | 0.080 ns |  0.40 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | nl      |  15.40 ns | 0.102 ns | 0.090 ns |  0.35 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **en**      |  **77.28 ns** | **0.391 ns** | **0.366 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | en      |  55.54 ns | 0.204 ns | 0.191 ns |  0.72 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | en      |  11.79 ns | 0.028 ns | 0.024 ns |  0.15 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | en      |  52.06 ns | 0.147 ns | 0.138 ns |  0.67 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **en-GB**   | **172.57 ns** | **0.902 ns** | **0.753 ns** |  **1.00** | **0.0296** |     **248 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | en-GB   |  74.60 ns | 0.227 ns | 0.189 ns |  0.43 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | en-GB   |  13.63 ns | 0.058 ns | 0.052 ns |  0.08 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | en-GB   |  54.30 ns | 0.248 ns | 0.219 ns |  0.31 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **nl**      |  **78.69 ns** | **0.351 ns** | **0.329 ns** |  **1.00** | **0.0181** |     **152 B** | 
| IdiomaSet_Greeting            | Greeting(name)          | nl      |  55.54 ns | 0.473 ns | 0.443 ns |  0.71 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | nl      |  13.85 ns | 0.104 ns | 0.092 ns |  0.18 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | nl      |  54.19 ns | 0.256 ns | 0.214 ns |  0.69 | 0.0315 |     264 B | 
