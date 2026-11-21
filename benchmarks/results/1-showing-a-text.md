```

BenchmarkDotNet v0.16.0-preview.2, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
Memory: 64 GB Total, 0.59 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a

IterationCount=15  LaunchCount=1  WarmupCount=5  

```
| Method                        | Categories              | Culture | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | 
|------------------------------ |------------------------ |-------- |----------:|---------:|---------:|------:|-------:|----------:|
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **de**      |  **40.14 ns** | **0.264 ns** | **0.234 ns** |  **1.00** | **0.0076** |      **64 B** | 
| ResxClass_Cart                | Cart (en-GB rewords it) | de      |  27.79 ns | 0.135 ns | 0.120 ns |  0.69 |      - |         - | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | de      |  22.68 ns | 0.097 ns | 0.091 ns |  0.57 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | de      |  15.64 ns | 0.205 ns | 0.192 ns |  0.39 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | de      |  19.98 ns | 0.651 ns | 0.577 ns |  0.50 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | de      |  16.76 ns | 0.064 ns | 0.056 ns |  0.42 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **en**      |  **38.75 ns** | **0.164 ns** | **0.137 ns** |  **1.00** | **0.0076** |      **64 B** | 
| ResxClass_Cart                | Cart (en-GB rewords it) | en      |  27.92 ns | 0.080 ns | 0.071 ns |  0.72 |      - |         - | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | en      |  16.61 ns | 0.067 ns | 0.062 ns |  0.43 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | en      |  11.81 ns | 0.024 ns | 0.021 ns |  0.30 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | en      |  15.45 ns | 0.075 ns | 0.066 ns |  0.40 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | en      |  13.29 ns | 0.090 ns | 0.080 ns |  0.34 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **en-GB**   |  **39.59 ns** | **0.139 ns** | **0.124 ns** |  **1.00** | **0.0086** |      **72 B** | 
| ResxClass_Cart                | Cart (en-GB rewords it) | en-GB   |  27.34 ns | 0.106 ns | 0.099 ns |  0.69 |      - |         - | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | en-GB   |  31.62 ns | 0.186 ns | 0.174 ns |  0.80 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | en-GB   |  13.71 ns | 0.040 ns | 0.037 ns |  0.35 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | en-GB   |  17.62 ns | 0.080 ns | 0.074 ns |  0.45 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | en-GB   |  15.34 ns | 0.081 ns | 0.076 ns |  0.39 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Cart**                | **Cart (en-GB rewords it)** | **nl**      |  **40.13 ns** | **0.211 ns** | **0.198 ns** |  **1.00** | **0.0076** |      **64 B** | 
| ResxClass_Cart                | Cart (en-GB rewords it) | nl      |  27.89 ns | 0.057 ns | 0.054 ns |  0.70 |      - |         - | 
| IdiomaSet_Cart                | Cart (en-GB rewords it) | nl      |  16.54 ns | 0.062 ns | 0.058 ns |  0.41 |      - |         - | 
| IdiomaInlineInField_Cart      | Cart (en-GB rewords it) | nl      |  13.94 ns | 0.044 ns | 0.039 ns |  0.35 |      - |         - | 
| IdiomaInlineInMarkup_Cart     | Cart (en-GB rewords it) | nl      |  17.72 ns | 0.066 ns | 0.062 ns |  0.44 |      - |         - | 
| IdiomaAttribute_Cart          | Cart (en-GB rewords it) | nl      |  15.52 ns | 0.061 ns | 0.048 ns |  0.39 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **de**      |  **42.37 ns** | **0.190 ns** | **0.168 ns** |  **1.00** | **0.0086** |      **72 B** | 
| ResxClass_Checkout            | Checkout                | de      |  27.80 ns | 0.076 ns | 0.071 ns |  0.66 |      - |         - | 
| IdiomaSet_Checkout            | Checkout                | de      |  22.67 ns | 0.078 ns | 0.073 ns |  0.54 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | de      |  15.57 ns | 0.177 ns | 0.166 ns |  0.37 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | de      |  19.68 ns | 0.652 ns | 0.610 ns |  0.46 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | de      |  16.70 ns | 0.049 ns | 0.044 ns |  0.39 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **en**      |  **41.05 ns** | **0.219 ns** | **0.205 ns** |  **1.00** | **0.0086** |      **72 B** | 
| ResxClass_Checkout            | Checkout                | en      |  27.67 ns | 0.073 ns | 0.065 ns |  0.67 |      - |         - | 
| IdiomaSet_Checkout            | Checkout                | en      |  16.51 ns | 0.075 ns | 0.067 ns |  0.40 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | en      |  11.79 ns | 0.050 ns | 0.047 ns |  0.29 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | en      |  15.45 ns | 0.067 ns | 0.063 ns |  0.38 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | en      |  13.27 ns | 0.048 ns | 0.045 ns |  0.32 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **en-GB**   | **133.99 ns** | **0.826 ns** | **0.732 ns** |  **1.00** | **0.0200** |     **168 B** | 
| ResxClass_Checkout            | Checkout                | en-GB   | 122.27 ns | 0.707 ns | 0.590 ns |  0.91 | 0.0105 |      88 B | 
| IdiomaSet_Checkout            | Checkout                | en-GB   |  33.88 ns | 0.125 ns | 0.117 ns |  0.25 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | en-GB   |  13.71 ns | 0.032 ns | 0.027 ns |  0.10 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | en-GB   |  17.32 ns | 0.087 ns | 0.081 ns |  0.13 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | en-GB   |  15.28 ns | 0.067 ns | 0.056 ns |  0.11 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Checkout**            | **Checkout**                | **nl**      |  **43.73 ns** | **0.181 ns** | **0.169 ns** |  **1.00** | **0.0086** |      **72 B** | 
| ResxClass_Checkout            | Checkout                | nl      |  27.81 ns | 0.145 ns | 0.129 ns |  0.64 |      - |         - | 
| IdiomaSet_Checkout            | Checkout                | nl      |  16.57 ns | 0.058 ns | 0.055 ns |  0.38 |      - |         - | 
| IdiomaInlineInField_Checkout  | Checkout                | nl      |  13.96 ns | 0.024 ns | 0.018 ns |  0.32 |      - |         - | 
| IdiomaInlineInMarkup_Checkout | Checkout                | nl      |  17.56 ns | 0.074 ns | 0.069 ns |  0.40 |      - |         - | 
| IdiomaAttribute_Checkout      | Checkout                | nl      |  15.58 ns | 0.089 ns | 0.083 ns |  0.36 |      - |         - | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **de**      |  **78.83 ns** | **0.432 ns** | **0.337 ns** |  **1.00** | **0.0181** |     **152 B** | 
| ResxClass_Greeting            | Greeting(name)          | de      |  53.77 ns | 0.293 ns | 0.260 ns |  0.68 | 0.0057 |      48 B | 
| IdiomaSet_Greeting            | Greeting(name)          | de      | 145.79 ns | 0.845 ns | 0.791 ns |  1.85 | 0.0305 |     256 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | de      |  15.59 ns | 0.289 ns | 0.270 ns |  0.20 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | de      |  56.11 ns | 0.403 ns | 0.357 ns |  0.71 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **en**      |  **78.95 ns** | **0.228 ns** | **0.202 ns** |  **1.00** | **0.0181** |     **152 B** | 
| ResxClass_Greeting            | Greeting(name)          | en      |  53.88 ns | 0.345 ns | 0.322 ns |  0.68 | 0.0057 |      48 B | 
| IdiomaSet_Greeting            | Greeting(name)          | en      |  55.99 ns | 0.264 ns | 0.246 ns |  0.71 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | en      |  11.85 ns | 0.142 ns | 0.118 ns |  0.15 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | en      |  53.05 ns | 0.402 ns | 0.376 ns |  0.67 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **en-GB**   | **176.16 ns** | **3.279 ns** | **2.738 ns** |  **1.00** | **0.0296** |     **248 B** | 
| ResxClass_Greeting            | Greeting(name)          | en-GB   | 144.42 ns | 1.153 ns | 1.022 ns |  0.82 | 0.0162 |     136 B | 
| IdiomaSet_Greeting            | Greeting(name)          | en-GB   |  76.38 ns | 0.273 ns | 0.213 ns |  0.43 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | en-GB   |  13.90 ns | 0.098 ns | 0.087 ns |  0.08 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | en-GB   |  54.19 ns | 0.168 ns | 0.149 ns |  0.31 | 0.0315 |     264 B | 
|                               |                         |         |           |          |          |       |        |           | 
| **Localizer_Greeting**            | **Greeting(name)**          | **nl**      |  **81.39 ns** | **0.527 ns** | **0.493 ns** |  **1.00** | **0.0181** |     **152 B** | 
| ResxClass_Greeting            | Greeting(name)          | nl      |  54.18 ns | 0.657 ns | 0.583 ns |  0.67 | 0.0057 |      48 B | 
| IdiomaSet_Greeting            | Greeting(name)          | nl      |  56.04 ns | 0.295 ns | 0.230 ns |  0.69 | 0.0200 |     168 B | 
| IdiomaInlineInField_Greeting  | Greeting(name)          | nl      |  13.88 ns | 0.086 ns | 0.080 ns |  0.17 |      - |         - | 
| IdiomaInlineInMarkup_Greeting | Greeting(name)          | nl      |  54.66 ns | 0.386 ns | 0.342 ns |  0.67 | 0.0315 |     264 B | 
