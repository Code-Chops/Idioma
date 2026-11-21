```

BenchmarkDotNet v0.16.0-preview.2, macOS Tahoe 26.6.2 (25G83) [Darwin 25.6.0]
Apple M2 Max, 1 CPU, 12 logical and 12 physical cores
Memory: 64 GB Total, 0.55 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a
  Job-BDGCJE : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a

IterationCount=15  LaunchCount=1  WarmupCount=5  
Alloc Ratio=NA  

```
| Method                           | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | 
|--------------------------------- |----------:|---------:|---------:|------:|-------:|----------:|
| Display_AttributeKept            |  27.63 ns | 0.161 ns | 0.142 ns |  1.00 |      - |         - | 
| Display_ReadFromProperty         | 772.28 ns | 4.841 ns | 4.528 ns | 27.95 | 0.0906 |     760 B | 
| Localized_PropertyByTypeAndName  |  16.76 ns | 0.063 ns | 0.059 ns |  0.61 |      - |         - | 
| Localized_PropertyByPropertyInfo |  19.61 ns | 0.223 ns | 0.198 ns |  0.71 |      - |         - | 
| Localized_Type                   |  15.26 ns | 0.050 ns | 0.039 ns |  0.55 |      - |         - | 
| Localized_EnumValue              |  13.16 ns | 0.048 ns | 0.040 ns |  0.48 |      - |         - | 
| SetOfEnum_EnumValue              |  16.91 ns | 0.175 ns | 0.164 ns |  0.61 |      - |         - | 
