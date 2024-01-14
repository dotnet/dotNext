Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:

| Parameter | Configuration |
| ---- | ---- |
| Runtime | .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2 |
| LaunchCount | 1 |
| RunStrategy | Throughput |
| OS | Ubuntu 22.04.3 |
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

| Method              | Bytes      | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD |
|-------------------- |----------- |----------:|----------:|----------:|----------:|------:|--------:|
| Convert.ToHexString | 1024 bytes | 674.47 ns | 13.196 ns | 12.343 ns | 674.80 ns |  1.00 |    0.00 |
| Hex.EncodeToUtf16   | 1024 bytes | 656.28 ns |  6.336 ns |  5.617 ns | 655.19 ns |  0.97 |    0.02 |
| Hex.EncodeToUtf8    | 1024 bytes | 363.58 ns |  5.733 ns |  4.788 ns | 365.53 ns |  0.54 |    0.01 |
|                     |            |           |           |           |           |       |         |
| Convert.ToHexString | 128 bytes  |  95.06 ns |  4.340 ns | 12.798 ns | 100.99 ns |  1.00 |    0.00 |
| Hex.EncodeToUtf16   | 128 bytes  |  93.22 ns |  1.932 ns |  2.645 ns |  93.51 ns |  1.15 |    0.18 |
| Hex.EncodeToUtf8    | 128 bytes  |  49.02 ns |  1.045 ns |  0.926 ns |  48.77 ns |  0.62 |    0.08 |
|                     |            |           |           |           |           |       |         |
| Convert.ToHexString | 16 bytes   |  24.21 ns |  0.971 ns |  2.800 ns |  23.64 ns |  1.00 |    0.00 |
| Hex.EncodeToUtf16   | 16 bytes   |  26.29 ns |  1.050 ns |  3.096 ns |  25.79 ns |  1.10 |    0.19 |
| Hex.EncodeToUtf8    | 16 bytes   |  20.44 ns |  0.264 ns |  0.234 ns |  20.39 ns |  0.71 |    0.04 |
|                     |            |           |           |           |           |       |         |
| Convert.ToHexString | 256 bytes  | 169.86 ns |  2.322 ns |  2.172 ns | 169.59 ns |  1.00 |    0.00 |
| Hex.EncodeToUtf16   | 256 bytes  | 147.84 ns |  1.522 ns |  1.350 ns | 147.84 ns |  0.87 |    0.01 |
| Hex.EncodeToUtf8    | 256 bytes  |  73.90 ns |  0.732 ns |  0.649 ns |  73.99 ns |  0.44 |    0.01 |
|                     |            |           |           |           |           |       |         |
| Convert.ToHexString | 512 bytes  | 316.92 ns |  6.290 ns |  6.177 ns | 316.16 ns |  1.00 |    0.00 |
| Hex.EncodeToUtf16   | 512 bytes  | 297.29 ns |  5.743 ns |  5.372 ns | 297.34 ns |  0.94 |    0.02 |
| Hex.EncodeToUtf8    | 512 bytes  | 137.94 ns |  2.712 ns |  2.537 ns | 137.76 ns |  0.44 |    0.01 |
|                     |            |           |           |           |           |       |         |
| Convert.ToHexString | 64 bytes   |  43.90 ns |  0.947 ns |  1.955 ns |  43.11 ns |  1.00 |    0.00 |
| Hex.EncodeToUtf16   | 64 bytes   |  39.29 ns |  0.125 ns |  0.111 ns |  39.23 ns |  0.88 |    0.03 |
| Hex.EncodeToUtf8    | 64 bytes   |  25.28 ns |  0.271 ns |  0.240 ns |  25.23 ns |  0.56 |    0.02 |

# Atomic Access to Arbitrary Value Type
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/Threading/AtomicContainerBenchmark.cs) compares performance of [Atomic&lt;T&gt;](xref:DotNext.Threading.Atomic`1) and Synchronized methods. The implementation of the benchmark contains concurrent read/write threads to ensure that lock contention is in place.

| Method       | Mean       | Error    | StdDev   | Median     |
|------------- |-----------:|---------:|---------:|-----------:|
| Atomic       |   589.5 us | 11.30 us | 105.0 us |   576.9 us |
| Synchronized | 1,005.4 us | 12.59 us | 117.3 us |   981.7 us |
| SpinLock     | 1,359.3 us | 65.59 us | 628.4 us | 1,573.7 us |

# File-buffering Writer
[This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/IO/FileBufferingWriterBenchmark.cs) compares performance of [FileBufferingWriteStream](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webutilities.filebufferingwritestream) from ASP.NET Core and [FileBufferingWriter](xref:DotNext.IO.FileBufferingWriter) from .NEXT library.

Both classes switching from in-memory buffer to file-based buffer during benchmark execution. Note that benchmark result highly depends on disk I/O performance. The following results were obtained using NVMe SSD.

| Method                                        | Mean       | Error    | StdDev   | Gen0     | Gen1     | Gen2     | Allocated |
|---------------------------------------------- |-----------:|---------:|---------:|---------:|---------:|---------:|----------:|
| 'FileBufferingWriter, synchronous mode'       |   934.7 us |  8.52 us |  7.97 us | 250.0000 | 249.0234 | 249.0234 |      1 MB |
| 'FileBufferingWriter, asynchronous mode'      | 1,306.9 us | 25.69 us | 44.31 us | 195.3125 | 195.3125 | 195.3125 |      1 MB |
| 'FileBufferingWriteStream, synchronouse mode' | 3,548.1 us | 30.80 us | 25.72 us | 402.3438 | 289.0625 | 250.0000 |    1.5 MB |
| 'FileBufferingWriteStream, asynchronous mode' | 4,054.6 us | 42.32 us | 39.59 us | 375.0000 | 289.0625 | 242.1875 |    1.5 MB |

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

| Method                         | Number of input tasks | Mean         | Error     | StdDev    | Ratio | RatioSD |
|------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|
| ProduceConsumeCompletionPipe   | 10         |     9.420 us | 0.0787 us | 0.0736 us |  0.65 |    0.02 |
| ProduceConsumeUnboundedChannel | 10         |    14.449 us | 0.2887 us | 0.3651 us |  1.00 |    0.00 |
|                                |            |              |           |           |       |         |
| ProduceConsumeCompletionPipe   | 100        |    75.802 us | 1.4586 us | 1.4326 us |  0.62 |    0.01 |
| ProduceConsumeUnboundedChannel | 100        |   123.375 us | 0.7581 us | 0.6720 us |  1.00 |    0.00 |
|                                |            |              |           |           |       |         |
| ProduceConsumeCompletionPipe   | 1000       |   707.746 us | 9.2224 us | 8.6266 us |  0.66 |    0.01 |
| ProduceConsumeUnboundedChannel | 1000       | 1,073.418 us | 5.2448 us | 4.9060 us |  1.00 |    0.00 |