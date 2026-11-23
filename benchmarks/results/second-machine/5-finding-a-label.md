```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 28.84 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=1  WarmupCount=5  
Alloc Ratio=NA  

```
| Method                           | Mean       | Error      | StdDev     | Ratio | Gen0   | Allocated | 
|--------------------------------- |-----------:|-----------:|-----------:|------:|-------:|----------:|
| Display_AttributeKept            |  34.666 ns |  0.2505 ns |  0.2220 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 785.448 ns | 13.3242 ns | 11.8116 ns | 22.66 | 0.0906 |     760 B | 
| Localized_PropertyByTypeAndName  |   5.680 ns |  0.0344 ns |  0.0305 ns |  0.16 |      - |         - | 
| Localized_PropertyByPropertyInfo |   8.953 ns |  0.1412 ns |  0.1252 ns |  0.26 |      - |         - | 
| Localized_Type                   |   4.538 ns |  0.0462 ns |  0.0409 ns |  0.13 |      - |         - | 
| Localized_EnumValue              |   4.163 ns |  0.0158 ns |  0.0148 ns |  0.12 |      - |         - | 
| SetOfEnum_EnumValue              |   3.551 ns |  0.0309 ns |  0.0289 ns |  0.10 |      - |         - | 
