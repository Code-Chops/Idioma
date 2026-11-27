```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 30.4 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-ROROYX : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  IterationTime=100ms  LaunchCount=3  
WarmupCount=5  Alloc Ratio=NA  

```
| Method                           | Mean       | Error      | StdDev     | Ratio | Gen0   | Allocated | 
|--------------------------------- |-----------:|-----------:|-----------:|------:|-------:|----------:|
| Display_AttributeKept            |  35.574 ns |  0.2558 ns |  0.4678 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 816.914 ns | 15.5982 ns | 28.1268 ns | 22.97 | 0.0874 |     760 B | 
| Localized_PropertyByTypeAndName  |   6.423 ns |  0.1112 ns |  0.1947 ns |  0.18 |      - |         - | 
| Localized_PropertyByPropertyInfo |   9.602 ns |  0.0400 ns |  0.0711 ns |  0.27 |      - |         - | 
| Localized_Type                   |   5.176 ns |  0.0298 ns |  0.0538 ns |  0.15 |      - |         - | 
| Localized_EnumValue              |   4.763 ns |  0.0189 ns |  0.0336 ns |  0.13 |      - |         - | 
| SetOfEnum_EnumValue              |   3.445 ns |  0.0171 ns |  0.0308 ns |  0.10 |      - |         - | 
