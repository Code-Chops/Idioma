```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 26.44 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-LVEFGF : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=3  WarmupCount=5  
Alloc Ratio=NA  

```
| Method                           | Mean       | Error     | StdDev     | Ratio | Gen0   | Allocated | 
|--------------------------------- |-----------:|----------:|-----------:|------:|-------:|----------:|
| Display_AttributeKept            |  34.995 ns | 0.1264 ns |  0.2342 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 800.223 ns | 8.1375 ns | 14.6737 ns | 22.87 | 0.0906 |     760 B | 
| Localized_PropertyByTypeAndName  |   6.460 ns | 0.0431 ns |  0.0788 ns |  0.18 |      - |         - | 
| Localized_PropertyByPropertyInfo |  10.069 ns | 0.2123 ns |  0.3935 ns |  0.29 |      - |         - | 
| Localized_Type                   |   5.255 ns | 0.0273 ns |  0.0506 ns |  0.15 |      - |         - | 
| Localized_EnumValue              |   4.954 ns | 0.0304 ns |  0.0540 ns |  0.14 |      - |         - | 
| SetOfEnum_EnumValue              |   3.675 ns | 0.0165 ns |  0.0302 ns |  0.11 |      - |         - | 
