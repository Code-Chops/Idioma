```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 29.83 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-ROROYX : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  IterationTime=100ms  LaunchCount=3  
WarmupCount=5  Alloc Ratio=NA  

```
| Method                           | Mean       | Error     | StdDev     | Ratio | Gen0   | Allocated | 
|--------------------------------- |-----------:|----------:|-----------:|------:|-------:|----------:|
| Display_AttributeKept            |  34.767 ns | 0.2290 ns |  0.4071 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 793.133 ns | 7.5892 ns | 13.8773 ns | 22.82 | 0.0881 |     760 B | 
| Localized_PropertyByTypeAndName  |   6.399 ns | 0.0292 ns |  0.0518 ns |  0.18 |      - |         - | 
| Localized_PropertyByPropertyInfo |   9.940 ns | 0.0857 ns |  0.1567 ns |  0.29 |      - |         - | 
| Localized_Type                   |   5.262 ns | 0.0242 ns |  0.0430 ns |  0.15 |      - |         - | 
| Localized_EnumValue              |   4.924 ns | 0.0280 ns |  0.0506 ns |  0.14 |      - |         - | 
| SetOfEnum_EnumValue              |   3.724 ns | 0.1312 ns |  0.2465 ns |  0.11 |      - |         - | 
