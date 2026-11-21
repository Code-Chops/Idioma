```

BenchmarkDotNet v0.16.0-preview.2, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
Memory: 64 GB Total, 0.59 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a

IterationCount=15  LaunchCount=1  WarmupCount=5  
Alloc Ratio=NA  

```
| Method                           | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | 
|--------------------------------- |----------:|---------:|---------:|------:|-------:|----------:|
| Display_AttributeKept            |  27.40 ns | 0.175 ns | 0.146 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 746.47 ns | 6.758 ns | 5.991 ns | 27.24 | 0.0906 |     760 B | 
| Localized_PropertyByTypeAndName  |  16.90 ns | 0.056 ns | 0.050 ns |  0.62 |      - |         - | 
| Localized_PropertyByPropertyInfo |  19.45 ns | 0.116 ns | 0.103 ns |  0.71 |      - |         - | 
| Localized_Type                   |  15.11 ns | 0.044 ns | 0.039 ns |  0.55 |      - |         - | 
| Localized_EnumValue              |  13.31 ns | 0.037 ns | 0.035 ns |  0.49 |      - |         - | 
| SetOfEnum_EnumValue              |  16.96 ns | 0.061 ns | 0.054 ns |  0.62 |      - |         - | 
