```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 28.38 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=1  WarmupCount=5  
Alloc Ratio=NA  

```
| Method                           | Mean       | Error      | StdDev     | Ratio | Gen0   | Allocated | 
|--------------------------------- |-----------:|-----------:|-----------:|------:|-------:|----------:|
| Display_AttributeKept            |  34.521 ns |  0.1875 ns |  0.1754 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 824.931 ns | 38.1703 ns | 35.7045 ns | 23.90 | 0.0906 |     760 B | 
| Localized_PropertyByTypeAndName  |   6.293 ns |  0.0193 ns |  0.0171 ns |  0.18 |      - |         - | 
| Localized_PropertyByPropertyInfo |   9.571 ns |  0.0451 ns |  0.0421 ns |  0.28 |      - |         - | 
| Localized_Type                   |   5.144 ns |  0.0119 ns |  0.0111 ns |  0.15 |      - |         - | 
| Localized_EnumValue              |   4.743 ns |  0.0323 ns |  0.0287 ns |  0.14 |      - |         - | 
| SetOfEnum_EnumValue              |   3.600 ns |  0.0079 ns |  0.0070 ns |  0.10 |      - |         - | 
