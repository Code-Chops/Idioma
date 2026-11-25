```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 25.91 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=1  WarmupCount=5  
Alloc Ratio=NA  

```
| Method                           | Mean       | Error     | StdDev    | Ratio | Gen0   | Allocated | 
|--------------------------------- |-----------:|----------:|----------:|------:|-------:|----------:|
| Display_AttributeKept            |  34.650 ns | 0.2397 ns | 0.2242 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 784.637 ns | 3.0870 ns | 2.5778 ns | 22.65 | 0.0906 |     760 B | 
| Localized_PropertyByTypeAndName  |   6.331 ns | 0.0461 ns | 0.0385 ns |  0.18 |      - |         - | 
| Localized_PropertyByPropertyInfo |   9.598 ns | 0.0321 ns | 0.0285 ns |  0.28 |      - |         - | 
| Localized_Type                   |   5.146 ns | 0.0046 ns | 0.0039 ns |  0.15 |      - |         - | 
| Localized_EnumValue              |   4.745 ns | 0.0162 ns | 0.0143 ns |  0.14 |      - |         - | 
| SetOfEnum_EnumValue              |   3.676 ns | 0.0143 ns | 0.0111 ns |  0.11 |      - |         - | 
