```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26300.9911)
AMD Ryzen AI 9 HX PRO 370 w/ Radeon 890M 2.00GHz, 1 CPU, 24 logical and 12 physical cores
Memory: 55.65 GB Total, 30.39 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

IterationCount=15  LaunchCount=1  WarmupCount=5  
Alloc Ratio=NA  

```
| Method                           | Mean      | Error     | StdDev    | Ratio | Gen0   | Allocated | 
|--------------------------------- |----------:|----------:|----------:|------:|-------:|----------:|
| Display_AttributeKept            |  36.41 ns |  1.272 ns |  1.128 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 820.02 ns | 21.028 ns | 19.669 ns | 22.54 | 0.0906 |     760 B | 
| Localized_PropertyByTypeAndName  |  16.87 ns |  0.333 ns |  0.278 ns |  0.46 |      - |         - | 
| Localized_PropertyByPropertyInfo |  18.66 ns |  0.628 ns |  0.587 ns |  0.51 |      - |         - | 
| Localized_Type                   |  14.74 ns |  0.335 ns |  0.297 ns |  0.41 |      - |         - | 
| Localized_EnumValue              |  12.88 ns |  0.331 ns |  0.310 ns |  0.35 |      - |         - | 
| SetOfEnum_EnumValue              |  15.18 ns |  0.384 ns |  0.359 ns |  0.42 |      - |         - | 
