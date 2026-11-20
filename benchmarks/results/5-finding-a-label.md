```

BenchmarkDotNet v0.16.0-preview.2, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
Memory: 64 GB Total, 0.33 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a

IterationCount=15  LaunchCount=1  WarmupCount=5  
Alloc Ratio=NA  

```
| Method                           | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | 
|--------------------------------- |----------:|---------:|---------:|------:|-------:|----------:|
| Display_AttributeKept            |  27.82 ns | 0.197 ns | 0.175 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 759.07 ns | 3.908 ns | 3.263 ns | 27.28 | 0.0906 |     760 B | 
| Localized_PropertyByTypeAndName  |  16.85 ns | 0.065 ns | 0.054 ns |  0.61 |      - |         - | 
| Localized_PropertyByPropertyInfo |  18.48 ns | 0.076 ns | 0.064 ns |  0.66 |      - |         - | 
| Localized_Type                   |  15.13 ns | 0.081 ns | 0.071 ns |  0.54 |      - |         - | 
| Localized_EnumValue              |  13.24 ns | 0.049 ns | 0.044 ns |  0.48 |      - |         - | 
| SetOfEnum_EnumValue              |  16.54 ns | 0.179 ns | 0.159 ns |  0.59 |      - |         - | 
