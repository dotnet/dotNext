Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:

| Parameter | Configuration |
| ---- | ---- |
| Runtime | .NET 6.0.15 (6.0.1523.11507), X64 RyuJIT AVX2 |
| LaunchCount | 1 |
| RunStrategy | Throughput |
| OS | Ubuntu 22.04.1 |
| CPU | Intel Core i7-6700HQ CPU 2.60GHz (Skylake) |
| Number of CPUs | 1 |
| Physical Cores | 4 |
| Logical Cores | 8 |
| RAM | 24 GB |

You can run benchmarks using `Bench` build configuration as follows:
```bash
cd <dotnext-clone-path>/src/DotNext.Benchmarks
dotnet run -c Bench
```

# Bitwise Hash Code
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/BitwiseHashCodeBenchmark.cs) compares performance of [BitwiseComparer&lt;T&gt;.GetHashCode](xref:DotNext.BitwiseComparer`1) and `GetHashCode` instance method for the types [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

|                                        Method |       Mean |     Error |    StdDev |
|---------------------------------------------- |-----------:|----------:|----------:|
|                            `Guid.GetHashCode` |  0.6889 ns | 0.0085 ns | 0.0076 ns |
|           `BitwiseComparer<Guid>.GetHashCode` |  6.3194 ns | 0.0223 ns | 0.0209 ns |
| `BitwiseComparer<LargeStructure>.GetHashCode` | 20.6265 ns | 0.0839 ns | 0.0785 ns |
|                  `LargeStructure.GetHashCode` | 43.5677 ns | 0.1517 ns | 0.1419 ns |


Bitwise hash code algorithm is slower than JIT optimizations introduced by .NET 6 but still convenient in complex cases.

# Bytes to Hex
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/Buffers/Text/HexConversionBenchmark.cs) demonstrates performance of extension methods declared in `DotNext.Buffers.Text.Hex` class that allows to convert arbitrary set of bytes to hexadecimal form.

|              Method |      Bytes |      Mean |     Error |    StdDev |    Median | Ratio | RatioSD |
|-------------------- |----------- |----------:|----------:|----------:|----------:|------:|--------:|
| Convert.ToHexString | 1024 bytes | 538.97 ns | 10.842 ns | 24.025 ns | 525.74 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 | 1024 bytes | 440.52 ns |  8.712 ns | 16.575 ns | 436.12 ns |  0.81 |    0.05 |
|    Hex.EncodeToUtf8 | 1024 bytes | 417.81 ns |  7.737 ns |  8.599 ns | 417.23 ns |  0.75 |    0.03 |
|                     |            |           |           |           |           |       |         |
| Convert.ToHexString |  128 bytes |  81.04 ns |  1.485 ns |  3.442 ns |  79.49 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 |  128 bytes |  62.32 ns |  0.390 ns |  0.365 ns |  62.42 ns |  0.74 |    0.05 |
|    Hex.EncodeToUtf8 |  128 bytes |  33.85 ns |  0.127 ns |  0.118 ns |  33.83 ns |  0.40 |    0.02 |
|                     |            |           |           |           |           |       |         |
| Convert.ToHexString |   16 bytes |  27.71 ns |  0.121 ns |  0.107 ns |  27.71 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 |   16 bytes |  21.71 ns |  0.091 ns |  0.076 ns |  21.70 ns |  0.78 |    0.00 |
|    Hex.EncodeToUtf8 |   16 bytes |  15.44 ns |  0.056 ns |  0.050 ns |  15.47 ns |  0.56 |    0.00 |
|                     |            |           |           |           |           |       |         |
| Convert.ToHexString |  256 bytes | 141.18 ns |  0.486 ns |  0.431 ns | 141.32 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 |  256 bytes | 108.32 ns |  0.884 ns |  0.784 ns | 108.25 ns |  0.77 |    0.01 |
|    Hex.EncodeToUtf8 |  256 bytes |  56.18 ns |  0.402 ns |  0.376 ns |  56.27 ns |  0.40 |    0.00 |
|                     |            |           |           |           |           |       |         |
| Convert.ToHexString |  512 bytes | 283.63 ns |  3.316 ns |  3.102 ns | 284.13 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 |  512 bytes | 218.35 ns |  2.575 ns |  2.409 ns | 218.68 ns |  0.77 |    0.01 |
|    Hex.EncodeToUtf8 |  512 bytes | 107.17 ns |  1.674 ns |  1.566 ns | 107.55 ns |  0.38 |    0.00 |
|                     |            |           |           |           |           |       |         |
| Convert.ToHexString |   64 bytes |  48.84 ns |  0.198 ns |  0.175 ns |  48.85 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 |   64 bytes |  38.68 ns |  0.227 ns |  0.212 ns |  38.59 ns |  0.79 |    0.01 |
|    Hex.EncodeToUtf8 |   64 bytes |  22.88 ns |  0.109 ns |  0.096 ns |  22.87 ns |  0.47 |    0.00 |

# Atomic Access to Arbitrary Value Type
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/Threading/AtomicContainerBenchmark.cs) compares performance of [Atomic&lt;T&gt;](xref:DotNext.Threading.Atomic`1) and Synchronized methods. The implementation of the benchmark contains concurrent read/write threads to ensure that lock contention is in place.

|       Method |       Mean |    Error |    StdDev |     Median |
|------------- |-----------:|---------:|----------:|-----------:|
|       Atomic |   431.0 us |  8.38 us |  78.53 us |   424.8 us |
| Synchronized |   921.7 us | 10.16 us |  95.34 us |   904.9 us |
|     SpinLock | 2,084.9 us | 58.95 us | 561.14 us | 2,074.8 us |

# File-buffering Writer
[This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/IO/FileBufferingWriterBenchmark.cs) compares performance of [FileBufferingWriteStream](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webutilities.filebufferingwritestream) from ASP.NET Core and [FileBufferingWriter](xref:DotNext.IO.FileBufferingWriter) from .NEXT library.

Both classes switching from in-memory buffer to file-based buffer during benchmark execution. Note that benchmark result highly depends on disk I/O performance. The following results were obtained using NVMe SSD.

|                                        Method |       Mean |    Error |    StdDev |     Gen0 |     Gen1 |     Gen2 | Allocated |
|---------------------------------------------- |-----------:|---------:|----------:|---------:|---------:|---------:|----------:|
|       'FileBufferingWriter, synchronous mode' |   975.4 us |  9.21 us |   8.61 us | 250.0000 | 249.0234 | 249.0234 |      1 MB |
|      'FileBufferingWriter, asynchronous mode' | 1,472.5 us | 29.41 us |  55.95 us | 148.4375 | 148.4375 | 148.4375 |      1 MB |
| 'FileBufferingWriteStream, synchronouse mode' | 3,993.1 us | 62.67 us |  55.55 us | 484.3750 | 343.7500 | 273.4375 |   1.88 MB |
| 'FileBufferingWriteStream, asynchronous mode' | 5,053.0 us | 97.63 us | 130.34 us | 468.7500 | 367.1875 | 265.6250 |   1.88 MB |

`FileBufferingWriter` is a winner in synchronous scenario because it has native support for synchronous mode in contrast to `FileBufferingWriteStream`.

# Various Buffer Types
[This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/Buffers/MemoryStreamingBenchmark.cs) demonstrates the performance of write operation and memory consumption of the following types:
* [MemoryStream](https://docs.microsoft.com/en-us/dotnet/api/system.io.memorystream)
* [RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream)
* [SparseBufferWriter&lt;byte&gt;](xref:DotNext.Buffers.SparseBufferWriter`1)
* [PoolingArrayBufferWriter&lt;byte&gt;](xref:DotNext.Buffers.PoolingArrayBufferWriter`1)
* [FileBufferingWriter](xref:DotNext.IO.FileBufferingWriter)

|                        Method | TotalCount |            Mean |         Error |        StdDev | Ratio | RatioSD |     Gen0 |     Gen1 |     Gen2 | Allocated | Alloc Ratio |
|------------------------------ |----------- |----------------:|--------------:|--------------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
|                  MemoryStream |        100 |        67.96 ns |      0.603 ns |      0.564 ns |  1.00 |    0.00 |   0.1097 |        - |        - |     344 B |        1.00 |
|      SparseBufferWriter<byte> |        100 |       249.14 ns |      1.903 ns |      1.780 ns |  3.67 |    0.04 |   0.0610 |        - |        - |     192 B |        0.56 |
| PoolingArrayBufferWriter<byte> |        100 |       290.32 ns |      2.758 ns |      2.580 ns |  4.27 |    0.06 |   0.0787 |        - |        - |     248 B |        0.72 |
|        RecyclableMemoryStream |        100 |     1,859.87 ns |     17.869 ns |     16.715 ns | 27.37 |    0.37 |   0.0839 |        - |        - |     264 B |        0.77 |
|           FileBufferingWriter |        100 |     2,901.26 ns |     19.012 ns |     15.876 ns | 42.64 |    0.37 |   0.0801 |        - |        - |     256 B |        0.74 |
|                               |            |                 |               |               |       |         |          |          |          |           |             |
|                  MemoryStream |       1000 |       126.12 ns |      1.070 ns |      0.949 ns |  1.00 |    0.00 |   0.3467 |        - |        - |    1088 B |        1.00 |
|      SparseBufferWriter<byte> |       1000 |       274.55 ns |      2.455 ns |      2.297 ns |  2.18 |    0.03 |   0.0610 |        - |        - |     192 B |        0.18 |
| PoolingArrayBufferWriter<byte> |       1000 |       523.82 ns |      2.064 ns |      1.724 ns |  4.15 |    0.03 |   0.0782 |        - |        - |     248 B |        0.23 |
|        RecyclableMemoryStream |       1000 |     1,863.53 ns |     11.036 ns |     10.323 ns | 14.78 |    0.14 |   0.0839 |        - |        - |     264 B |        0.24 |
|           FileBufferingWriter |       1000 |     2,935.07 ns |     14.906 ns |     13.943 ns | 23.28 |    0.22 |   0.0801 |        - |        - |     256 B |        0.24 |
|                               |            |                 |               |               |       |         |          |          |          |           |             |
|      SparseBufferWriter<byte> |      10000 |       780.10 ns |      7.312 ns |      6.106 ns |  0.30 |    0.00 |   0.0811 |        - |        - |     256 B |       0.008 |
| PoolingArrayBufferWriter<byte> |      10000 |     1,553.56 ns |     18.831 ns |     17.614 ns |  0.60 |    0.01 |   0.0782 |        - |        - |     248 B |       0.008 |
|        RecyclableMemoryStream |      10000 |     2,330.93 ns |     28.111 ns |     31.246 ns |  0.90 |    0.02 |   0.0839 |        - |        - |     264 B |       0.009 |
|                  MemoryStream |      10000 |     2,587.38 ns |     47.665 ns |     39.802 ns |  1.00 |    0.00 |   9.8343 |        - |        - |   30880 B |       1.000 |
|           FileBufferingWriter |      10000 |     3,823.11 ns |     22.341 ns |     19.805 ns |  1.48 |    0.02 |   0.0763 |        - |        - |     256 B |       0.008 |
|                               |            |                 |               |               |       |         |          |          |          |           |             |
|      SparseBufferWriter<byte> |     100000 |     4,594.99 ns |     36.754 ns |     28.695 ns |  0.05 |    0.00 |   0.1373 |        - |        - |     448 B |       0.002 |
|        RecyclableMemoryStream |     100000 |     7,296.87 ns |    141.390 ns |    211.626 ns |  0.08 |    0.00 |   0.0839 |        - |        - |     264 B |       0.001 |
| PoolingArrayBufferWriter<byte> |     100000 |     9,591.01 ns |    190.970 ns |    187.558 ns |  0.11 |    0.00 |   0.0763 |        - |        - |     248 B |       0.001 |
|                  MemoryStream |     100000 |    87,218.01 ns |    725.528 ns |    678.660 ns |  1.00 |    0.00 |  41.6260 |  41.6260 |  41.6260 |  260356 B |       1.000 |
|           FileBufferingWriter |     100000 |    87,383.44 ns |  1,727.693 ns |  4,172.573 ns |  0.98 |    0.04 |   0.1221 |        - |        - |     568 B |       0.002 |
|                               |            |                 |               |               |       |         |          |          |          |           |             |
|      SparseBufferWriter<byte> |    1000000 |    50,406.77 ns |    994.858 ns |  1,916.754 ns |  0.04 |    0.00 |   0.1831 |        - |        - |     640 B |       0.000 |
|        RecyclableMemoryStream |    1000000 |    57,416.33 ns |  1,143.944 ns |  1,361.784 ns |  0.04 |    0.00 |   0.2441 |        - |        - |     896 B |       0.000 |
| PoolingArrayBufferWriter<byte> |    1000000 |    82,694.54 ns |    624.704 ns |    833.962 ns |  0.06 |    0.00 |        - |        - |        - |     248 B |       0.000 |
|           FileBufferingWriter |    1000000 |   514,998.40 ns |  9,839.591 ns | 10,936.679 ns |  0.39 |    0.02 |        - |        - |        - |     569 B |       0.000 |
|                  MemoryStream |    1000000 | 1,299,526.59 ns | 24,277.962 ns | 39,204.383 ns |  1.00 |    0.00 | 498.0469 | 498.0469 | 498.0469 | 2095745 B |       1.000 |

# TypeMap
[TypeMap&lt;TValue&gt;](xref:DotNext.Collections.Specialized.TypeMap`1) and [ConcurrentTypeMap&lt;TValue&gt;](xref:DotNext.Collections.Specialized.ConcurrentTypeMap`1) are specialized dictionaries where the keys are represented by the types passed as generic arguments. The are optimized in a way to be more performant than classic [Dictionary&lt;Type,TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2) and [ConcurrentDictionary&lt;Type,TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2). [This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/Collections/Specialized/TypeMapBenchmark.cs) demonstrates efficiency of the specialized collections:

|                                    Method |      Mean |     Error |    StdDev |
|------------------------------------------ |----------:|----------:|----------:|
|              'TypeMap, Set + TryGetValue' |  3.647 ns | 0.0260 ns | 0.0243 ns |
|           'Dictionary, Set + TryGetValue' | 33.777 ns | 0.1140 ns | 0.1010 ns |
|    'ConcurrentTypeMap, Set + TryGetValue' | 21.542 ns | 0.1108 ns | 0.0982 ns |
| 'ConcurrentDictionary, Set + TryGetValue' | 59.608 ns | 0.3600 ns | 0.3191 ns |
|             'ConcurrentTypeMap, GetOrAdd' | 10.249 ns | 0.0435 ns | 0.0407 ns |
|          'ConcurrentDictionary, GetOrAdd' | 16.019 ns | 0.0739 ns | 0.0692 ns |

# TaskCompletionPipe
[This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/Threading/Tasks/ChannelVersusPipeBenchmark.cs) demonstrates efficiency of [Task Completion Pipe](./features/threading/taskpipe.md) versus [async channels](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.channel-1) from .NET. Pipe sorts the submitted tasks in order of their completion in time. The fastest result is available immediately for the consumer, while the channel needs to wait for completion of the task and only then add it to the queue.

|                         Method | Number of input tasks |        Mean |     Error |    StdDev | Ratio | RatioSD |
|------------------------------- |---------------------- |------------:|----------:|----------:|------:|--------:|
|   ProduceConsumeCompletionPipe |                    10 |    11.86 us |  0.234 us |  0.313 us |  0.64 |    0.06 |
| ProduceConsumeUnboundedChannel |                    10 |    17.97 us |  0.418 us |  1.201 us |  1.00 |    0.00 |
|                                |                       |             |           |           |       |         |
|   ProduceConsumeCompletionPipe |                   100 |    83.09 us |  1.502 us |  1.254 us |  0.57 |    0.01 |
| ProduceConsumeUnboundedChannel |                   100 |   145.54 us |  2.836 us |  2.785 us |  1.00 |    0.00 |
|                                |                       |             |           |           |       |         |
|   ProduceConsumeCompletionPipe |                  1000 |   798.83 us | 13.276 us | 12.418 us |  0.64 |    0.01 |
| ProduceConsumeUnboundedChannel |                  1000 | 1,255.55 us | 17.538 us | 15.547 us |  1.00 |    0.00 |