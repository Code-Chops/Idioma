```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 31.78 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=1  WarmupCount=5  
Alloc Ratio=NA  

```
| Method                           | Mean       | Error     | StdDev    | Ratio | Gen0   | Allocated | 
|--------------------------------- |-----------:|----------:|----------:|------:|-------:|----------:|
| Display_AttributeKept            |  35.058 ns | 0.1072 ns | 0.0950 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 783.281 ns | 7.7398 ns | 7.2399 ns | 22.34 | 0.0906 |     760 B | 
| Localized_PropertyByTypeAndName  |   6.356 ns | 0.0114 ns | 0.0107 ns |  0.18 |      - |         - | 
| Localized_PropertyByPropertyInfo |   9.753 ns | 0.0567 ns | 0.0502 ns |  0.28 |      - |         - | 
| Localized_Type                   |   5.196 ns | 0.0421 ns | 0.0394 ns |  0.15 |      - |         - | 
| Localized_EnumValue              |   4.655 ns | 0.0082 ns | 0.0069 ns |  0.13 |      - |         - | 
| SetOfEnum_EnumValue              |   3.608 ns | 0.0189 ns | 0.0177 ns |  0.10 |      - |         - | 
