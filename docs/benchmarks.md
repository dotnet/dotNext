Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:

| Parameter | Configuration                                             |
| ---- |-----------------------------------------------------------|
| Runtime | .NET 10.0.0 (10.0.0, 10.0.25.52411), X64 RyuJIT x86-64-v3 |
| LaunchCount | 1                                                         |
| RunStrategy | Throughput                                                |
| OS | Ubuntu 24.04.3                                            |
| CPU | Intel Core Ultra 9 275HX 0.80GHz                          |
| Number of CPUs | 1                                                         |
| Physical Cores | 24                                                        |
| Logical Cores | 24                                                        |
| RAM | 64 GB                                                     |

You can run benchmarks using `Release` build configuration as follows:
```bash
cd <dotnext-clone-path>/src/DotNext.Benchmarks
dotnet run -c Release
```

# Bitwise Hash Code
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/BitwiseHashCodeBenchmark.cs) compares performance of [BitwiseComparer&lt;T&gt;.GetHashCode](xref:DotNext.BitwiseComparer`1) and `GetHashCode` instance method for the types [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method                                      | Mean       | Error     | StdDev    | Median     |
|-------------------------------------------- |-----------:|----------:|----------:|-----------:|
| Guid.GetHashCode                            |  0.0003 ns | 0.0008 ns | 0.0007 ns |  0.0000 ns |
| BitwiseComparer<Guid>.GetHashCode           |  2.5075 ns | 0.0606 ns | 0.0567 ns |  2.5228 ns |
| BitwiseComparer<LargeStructure>.GetHashCode |  7.9992 ns | 0.0674 ns | 0.0630 ns |  8.0176 ns |
| LargeStructure.GetHashCode                  | 25.8393 ns | 0.2648 ns | 0.2477 ns | 25.8916 ns |


Bitwise hash code algorithm is slower than JIT optimizations introduced by .NET 6 but still convenient in complex cases.

# Atomic Access to Arbitrary Value Type
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/Threading/AtomicContainerBenchmark.cs) compares performance of [Atomic&lt;T&gt;](xref:DotNext.Threading.Atomic`1) and Synchronized methods. The implementation of the benchmark contains concurrent read/write threads to ensure that lock contention is in place.

| Method       | Mean       | Error    | StdDev    | Median     |
|------------- |-----------:|---------:|----------:|-----------:|
| Atomic       |   307.0 us | 11.08 us |  96.53 us |   293.1 us |
| Synchronized |   831.3 us | 24.76 us | 233.87 us |   801.6 us |
| SpinLock     | 1,056.5 us | 48.50 us | 453.64 us | 1,288.6 us |

# File-buffering Writer
[This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/IO/FileBufferingWriterBenchmark.cs) compares performance of [FileBufferingWriteStream](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webutilities.filebufferingwritestream) from ASP.NET Core and [FileBufferingWriter](xref:DotNext.IO.FileBufferingWriter) from .NEXT library.

Both classes switching from in-memory buffer to file-based buffer during benchmark execution. Note that benchmark result highly depends on disk I/O performance. The following results were obtained using NVMe SSD.

| Method                                        | Mean       | Error    | StdDev   | Gen0     | Gen1     | Gen2     | Allocated |
|---------------------------------------------- |-----------:|---------:|---------:|---------:|---------:|---------:|----------:|
| 'FileBufferingWriter, synchronous mode'       |   384.8 us |  5.10 us |  4.78 us | 249.5117 | 249.5117 | 249.5117 |      1 MB |
| 'FileBufferingWriter, asynchronous mode'      |   474.6 us | 15.28 us | 44.81 us | 198.7305 | 198.7305 | 198.7305 |      1 MB |
| 'FileBufferingWriteStream, synchronouse mode' | 1,791.7 us | 34.71 us | 56.06 us | 246.0938 | 246.0938 | 246.0938 |   1.01 MB |
| 'FileBufferingWriteStream, asynchronous mode' | 1,886.5 us | 36.64 us | 54.84 us | 246.0938 | 246.0938 | 246.0938 |   1.01 MB |

`FileBufferingWriter` is a winner in synchronous scenario because it has native support for synchronous mode in contrast to `FileBufferingWriteStream`.

# Various Buffer Types
[This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/Buffers/MemoryStreamingBenchmark.cs) demonstrates the performance of write operation and memory consumption of the following types:
* [MemoryStream](https://docs.microsoft.com/en-us/dotnet/api/system.io.memorystream)
* [RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream)
* [SparseBufferWriter&lt;byte&gt;](xref:DotNext.Buffers.SparseBufferWriter`1)
* [PoolingArrayBufferWriter&lt;byte&gt;](xref:DotNext.Buffers.PoolingArrayBufferWriter`1)
* [FileBufferingWriter](xref:DotNext.IO.FileBufferingWriter)

| Method                         | TotalCount | Mean          | Error        | StdDev       | Median        | Ratio | RatioSD | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|------------------------------- |----------- |--------------:|-------------:|-------------:|--------------:|------:|--------:|---------:|---------:|---------:|----------:|------------:|
| MemoryStream                   | 100        |      17.16 ns |     0.227 ns |     0.212 ns |      17.20 ns |  1.00 |    0.02 |   0.0183 |        - |        - |     344 B |        1.00 |
| PoolingArrayBufferWriter<byte> | 100        |      63.55 ns |     1.293 ns |     3.647 ns |      61.75 ns |  3.70 |    0.22 |   0.0166 |        - |        - |     312 B |        0.91 |
| SparseBufferWriter<byte>       | 100        |     118.60 ns |     2.352 ns |     2.416 ns |     118.55 ns |  6.91 |    0.16 |   0.0139 |        - |        - |     264 B |        0.77 |
| RecyclableMemoryStream         | 100        |     341.81 ns |     3.508 ns |     2.929 ns |     342.14 ns | 19.92 |    0.29 |   0.0148 |        - |        - |     280 B |        0.81 |
| FileBufferingWriter            | 100        |     835.43 ns |     9.755 ns |     9.125 ns |     831.13 ns | 48.69 |    0.78 |   0.0286 |        - |        - |     552 B |        1.60 |
|                                |            |               |              |              |               |       |         |          |          |          |           |             |
| MemoryStream                   | 1000       |      42.55 ns |     0.767 ns |     1.478 ns |      42.23 ns |  1.00 |    0.05 |   0.0578 |   0.0002 |        - |    1088 B |        1.00 |
| PoolingArrayBufferWriter<byte> | 1000       |      96.78 ns |     0.266 ns |     0.236 ns |      96.78 ns |  2.28 |    0.07 |   0.0166 |        - |        - |     312 B |        0.29 |
| SparseBufferWriter<byte>       | 1000       |     100.16 ns |     0.834 ns |     0.739 ns |     100.11 ns |  2.36 |    0.08 |   0.0139 |        - |        - |     264 B |        0.24 |
| RecyclableMemoryStream         | 1000       |     349.89 ns |     2.460 ns |     2.181 ns |     350.15 ns |  8.23 |    0.27 |   0.0148 |        - |        - |     280 B |        0.26 |
| FileBufferingWriter            | 1000       |     821.75 ns |     9.333 ns |     8.730 ns |     827.28 ns | 19.33 |    0.66 |   0.0286 |        - |        - |     552 B |        0.51 |
|                                |            |               |              |              |               |       |         |          |          |          |           |             |
| SparseBufferWriter<byte>       | 10000      |     194.10 ns |     0.510 ns |     0.452 ns |     194.18 ns |  0.20 |    0.00 |   0.0174 |        - |        - |     328 B |       0.011 |
| PoolingArrayBufferWriter<byte> | 10000      |     285.20 ns |     2.702 ns |     2.527 ns |     284.50 ns |  0.29 |    0.00 |   0.0162 |        - |        - |     312 B |       0.010 |
| RecyclableMemoryStream         | 10000      |     437.91 ns |     4.131 ns |     3.865 ns |     439.25 ns |  0.44 |    0.00 |   0.0148 |        - |        - |     280 B |       0.009 |
| MemoryStream                   | 10000      |     992.58 ns |     2.860 ns |     2.536 ns |     991.66 ns |  1.00 |    0.00 |   1.6384 |   0.1354 |        - |   30880 B |       1.000 |
| FileBufferingWriter            | 10000      |   1,048.14 ns |    10.895 ns |    10.191 ns |   1,052.49 ns |  1.06 |    0.01 |   0.0286 |        - |        - |     552 B |       0.018 |
|                                |            |               |              |              |               |       |         |          |          |          |           |             |
| SparseBufferWriter<byte>       | 100000     |   1,432.00 ns |    11.107 ns |    10.389 ns |   1,434.84 ns |  0.04 |    0.00 |   0.0267 |        - |        - |     520 B |       0.002 |
| RecyclableMemoryStream         | 100000     |   1,494.27 ns |    15.335 ns |    14.344 ns |   1,496.85 ns |  0.04 |    0.00 |   0.0134 |        - |        - |     280 B |       0.001 |
| PoolingArrayBufferWriter<byte> | 100000     |   3,315.54 ns |    16.855 ns |    15.766 ns |   3,308.98 ns |  0.09 |    0.00 |   0.0153 |        - |        - |     312 B |       0.001 |
| FileBufferingWriter            | 100000     |  34,682.38 ns |   428.066 ns |   379.469 ns |  34,855.87 ns |  0.94 |    0.01 |        - |        - |        - |     720 B |       0.003 |
| MemoryStream                   | 100000     |  37,079.68 ns |   164.581 ns |   153.949 ns |  37,071.53 ns |  1.00 |    0.01 |  41.6260 |  41.6260 |  41.6260 |  260342 B |       1.000 |
|                                |            |               |              |              |               |       |         |          |          |          |           |             |
| SparseBufferWriter<byte>       | 1000000    |  14,920.93 ns |   133.721 ns |   125.083 ns |  14,960.18 ns |  0.03 |    0.00 |   0.0305 |        - |        - |     712 B |       0.000 |
| RecyclableMemoryStream         | 1000000    |  16,109.54 ns |   180.702 ns |   169.029 ns |  16,195.58 ns |  0.03 |    0.00 |   0.0305 |        - |        - |     912 B |       0.000 |
| PoolingArrayBufferWriter<byte> | 1000000    |  30,926.47 ns |    90.040 ns |    84.223 ns |  30,930.63 ns |  0.06 |    0.00 |        - |        - |        - |     312 B |       0.000 |
| FileBufferingWriter            | 1000000    | 214,754.04 ns | 2,851.151 ns | 2,666.968 ns | 215,879.67 ns |  0.43 |    0.01 |        - |        - |        - |     720 B |       0.000 |
| MemoryStream                   | 1000000    | 495,234.77 ns | 1,822.148 ns | 1,704.438 ns | 494,392.89 ns |  1.00 |    0.00 | 499.0234 | 499.0234 | 499.0234 | 2095576 B |       1.000 |


# TypeMap
[TypeMap&lt;TValue&gt;](xref:DotNext.Collections.Specialized.TypeMap`1) and [ConcurrentTypeMap&lt;TValue&gt;](xref:DotNext.Collections.Specialized.ConcurrentTypeMap`1) are specialized dictionaries where the keys are represented by the types passed as generic arguments. The are optimized in a way to be more performant than classic [Dictionary&lt;Type,TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2) and [ConcurrentDictionary&lt;Type,TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2). [This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/Collections/Specialized/TypeMapBenchmark.cs) demonstrates efficiency of the specialized collections:

| Method                                    | Mean       | Error     | StdDev    |
|------------------------------------------ |-----------:|----------:|----------:|
| 'TypeMap, Set + TryGetValue'              |  0.8406 ns | 0.0059 ns | 0.0055 ns |
| 'Dictionary, Set + TryGetValue'           |  5.3626 ns | 0.1172 ns | 0.1096 ns |
| 'ConcurrentTypeMap, Set + TryGetValue'    | 12.6409 ns | 0.0203 ns | 0.0170 ns |
| 'ConcurrentDictionary, Set + TryGetValue' | 14.2465 ns | 0.1529 ns | 0.1430 ns |
| 'ConcurrentTypeMap, GetOrAdd'             |  5.5630 ns | 0.0127 ns | 0.0112 ns |
| 'ConcurrentDictionary, GetOrAdd'          |  1.7144 ns | 0.0358 ns | 0.0335 ns |

# TaskCompletionPipe
[This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/Threading/Tasks/ChannelVersusPipeBenchmark.cs) demonstrates efficiency of [Task Completion Pipe](./features/threading/taskpipe.md) versus [async channels](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels.channel-1) from .NET. Pipe sorts the submitted tasks in order of their completion in time. The fastest result is available immediately for the consumer, while the channel needs to wait for completion of the task and only then add it to the queue.

| Method                         | iterations | Mean       | Error     | StdDev    | Ratio | RatioSD |
|------------------------------- |----------- |-----------:|----------:|----------:|------:|--------:|
| ProduceConsumeCompletionPipe   | 10         |   7.656 us | 0.0617 us | 0.0577 us |  0.71 |    0.01 |
| ProduceConsumeUnboundedChannel | 10         |  10.713 us | 0.2125 us | 0.2087 us |  1.00 |    0.03 |
|                                |            |            |           |           |       |         |
| ProduceConsumeCompletionPipe   | 100        |  70.826 us | 1.1429 us | 1.0131 us |  0.93 |    0.02 |
| ProduceConsumeUnboundedChannel | 100        |  76.471 us | 1.4577 us | 1.4316 us |  1.00 |    0.03 |
|                                |            |            |           |           |       |         |
| ProduceConsumeCompletionPipe   | 1000       | 695.160 us | 5.4029 us | 4.7895 us |  0.99 |    0.01 |
| ProduceConsumeUnboundedChannel | 1000       | 702.587 us | 8.6315 us | 7.2077 us |  1.00 |    0.01 |