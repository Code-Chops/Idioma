```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 30.17 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-ROROYX : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  IterationTime=100ms  LaunchCount=3  
WarmupCount=5  Alloc Ratio=NA  

```
| Method                           | Mean       | Error     | StdDev     | Ratio | Gen0   | Allocated | 
|--------------------------------- |-----------:|----------:|-----------:|------:|-------:|----------:|
| Display_AttributeKept            |  34.814 ns | 0.2669 ns |  0.4812 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 797.193 ns | 6.2300 ns | 11.2340 ns | 22.90 | 0.0893 |     760 B | 
| Localized_PropertyByTypeAndName  |   6.494 ns | 0.0419 ns |  0.0766 ns |  0.19 |      - |         - | 
| Localized_PropertyByPropertyInfo |  10.116 ns | 0.0649 ns |  0.1187 ns |  0.29 |      - |         - | 
| Localized_Type                   |   5.335 ns | 0.0261 ns |  0.0456 ns |  0.15 |      - |         - | 
| Localized_EnumValue              |   5.001 ns | 0.0427 ns |  0.0769 ns |  0.14 |      - |         - | 
| SetOfEnum_EnumValue              |   3.156 ns | 0.0205 ns |  0.0369 ns |  0.09 |      - |         - | 
