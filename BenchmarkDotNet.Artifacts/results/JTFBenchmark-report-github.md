```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.17763.7131/1809/October2018Update/Redstone5)
Intel Xeon Platinum 8488C, 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.404
  [Host]     : .NET 8.0.14 (8.0.1425.11118), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 8.0.14 (8.0.1425.11118), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI


```
| Method                                      | Mean        | Error    | StdDev   | Allocated |
|-------------------------------------------- |------------:|---------:|---------:|----------:|
| AsyncAllTheWay                              |   100.58 ms | 0.116 ms | 0.109 ms |  26.81 KB |
| WithSingleJTF                               |   100.48 ms | 0.219 ms | 0.205 ms |  42.93 KB |
| WithJTFMultipleContextNested                | 3,013.26 ms | 2.663 ms | 2.491 ms | 204.57 KB |
| WithJTFAndFactoryUtilNested                 | 3,014.15 ms | 2.282 ms | 2.135 ms | 199.33 KB |
| SyncOverAsync1                              |   103.08 ms | 1.341 ms | 1.317 ms |  31.98 KB |
| SyncOverAsync2                              |   102.08 ms | 1.254 ms | 1.173 ms |  31.52 KB |
| WithJTFDeadlockAvoidance                    |    10.81 ms | 0.121 ms | 0.113 ms |  86.22 KB |
| SyncOverAsyncWithDeadlockPotentialRewritten |    10.73 ms | 0.170 ms | 0.159 ms |  20.89 KB |
