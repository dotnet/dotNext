Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:

| Parameter | Configuration |
| ---- | ---- |
| Runtime | .NET 6.0.7 (6.0.722.32202), X64 RyuJIT |
| LaunchCount | 1 |
| RunStrategy | Throughput |
| OS | Ubuntu 20.04.4 |
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

|              Method |      Bytes |      Mean |     Error |    StdDev | Ratio | RatioSD |
|-------------------- |----------- |----------:|----------:|----------:|------:|--------:|
| Convert.ToHexString | 1024 bytes | 558.11 ns | 11.209 ns | 11.510 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 | 1024 bytes | 538.77 ns |  7.643 ns |  7.150 ns |  0.96 |    0.02 |
|    Hex.EncodeToUtf8 | 1024 bytes | 560.04 ns |  8.495 ns |  7.094 ns |  1.00 |    0.03 |
|                     |            |           |           |           |       |         |
| Convert.ToHexString |  128 bytes |  85.82 ns |  1.580 ns |  1.319 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 |  128 bytes |  84.43 ns |  0.598 ns |  0.560 ns |  0.98 |    0.01 |
|    Hex.EncodeToUtf8 |  128 bytes |  47.05 ns |  0.740 ns |  0.693 ns |  0.55 |    0.01 |
|                     |            |           |           |           |       |         |
| Convert.ToHexString |   16 bytes |  28.62 ns |  0.265 ns |  0.221 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 |   16 bytes |  28.70 ns |  0.239 ns |  0.212 ns |  1.00 |    0.01 |
|    Hex.EncodeToUtf8 |   16 bytes |  17.13 ns |  0.083 ns |  0.074 ns |  0.60 |    0.01 |
|                     |            |           |           |           |       |         |
| Convert.ToHexString |  256 bytes | 154.71 ns |  1.679 ns |  1.311 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 |  256 bytes | 146.90 ns |  2.749 ns |  3.273 ns |  0.96 |    0.03 |
|    Hex.EncodeToUtf8 |  256 bytes |  84.73 ns |  1.750 ns |  2.275 ns |  0.55 |    0.02 |
|                     |            |           |           |           |       |         |
| Convert.ToHexString |  512 bytes | 308.55 ns |  6.217 ns |  7.863 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 |  512 bytes | 291.02 ns |  5.800 ns |  9.199 ns |  0.95 |    0.02 |
|    Hex.EncodeToUtf8 |  512 bytes | 160.93 ns |  3.219 ns |  3.832 ns |  0.52 |    0.02 |
|                     |            |           |           |           |       |         |
| Convert.ToHexString |   64 bytes |  54.09 ns |  1.152 ns |  1.132 ns |  1.00 |    0.00 |
|   Hex.EncodeToUtf16 |   64 bytes |  50.94 ns |  0.725 ns |  0.643 ns |  0.94 |    0.03 |
|    Hex.EncodeToUtf8 |   64 bytes |  30.32 ns |  0.678 ns |  1.076 ns |  0.58 |    0.02 |

# Fast Reflection
The next series of benchmarks demonstrate performance of strongly typed reflection provided by DotNext Reflection library.

## Property Getter
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/PropertyGetterReflectionBenchmark.cs) demonstrates overhead of getting instance property value caused by different mechanisms:
1. Using [FastMember](https://github.com/mgravell/fast-member) library
1. Using strongly typed reflection from DotNext Reflection library: `Type<IndexOfCalculator>.Property<int>.RequireGetter`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type `Function<object, ValueTuple, object>`. It is assumed that instance type and property type is not known at compile type (th) so the delegate performs type check on every call.
1. Classic .NET reflection

|                                                                             Method |       Mean |     Error |    StdDev | Ratio | RatioSD |
|----------------------------------------------------------------------------------- |-----------:|----------:|----------:|------:|--------:|
|                                                                      'Direct call' |   9.390 ns | 0.0265 ns | 0.0248 ns |  1.00 |    0.00 |
| 'Reflection with DotNext using delegate type MemberGetter<IndexOfCalculator, int>' |  10.600 ns | 0.0325 ns | 0.0304 ns |  1.13 |    0.00 |
|                                     'Reflection with DotNext using DynamicInvoker' |  19.541 ns | 0.0716 ns | 0.0670 ns |  2.08 |    0.01 |
| 'Reflection with DotNext using delegate type Function<object, ValueTuple, object>' |  20.439 ns | 0.0656 ns | 0.0614 ns |  2.18 |    0.01 |
|                                       'ObjectAccess class from FastMember library' |  40.496 ns | 0.1832 ns | 0.1714 ns |  4.31 |    0.02 |
|                                                                  '.NET reflection' | 137.905 ns | 0.5757 ns | 0.5385 ns | 14.69 |    0.07 |

Strongly typed reflection provided by DotNext Reflection library has the same performance as direct call.

## Instance Method Call
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/StringMethodReflectionBenchmark.cs) demonstrates overhead of calling instance method `IndexOf` of type **string** caused by different mechanisms:
1. Using strongly typed reflection from DotNext Reflection library: `Type<string>.Method<char, int>.Require<int>(nameof(string.IndexOf))`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Type<string>.RequireMethod<(char, int), int>(nameof(string.IndexOf));`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<object, (object, object), object>`. It is assumed that types of all parameters are not known at compile time.
1. Classic .NET reflection

The benchmark uses series of different strings to run the same set of tests. Worst case means that character lookup is performed for a string that doesn't contain the given character. Best case means that character lookup is performed for a string that has the given character.

|                                                                                   Method |          StringValue |       Mean |     Error |    StdDev |     Median | Ratio | RatioSD |
|----------------------------------------------------------------------------------------- |--------------------- |-----------:|----------:|----------:|-----------:|------:|--------:|
|                                                                        '.NET reflection' |                      | 267.785 ns | 1.1146 ns | 0.9881 ns | 267.686 ns | 64.82 |    0.39 |
|                                                                            'Direct call' |                      |   4.129 ns | 0.0197 ns | 0.0153 ns |   4.125 ns |  1.00 |    0.00 |
|               'Reflection with DotNext using delegate type Func<string, char, int, int>' |                      |   7.290 ns | 0.0220 ns | 0.0206 ns |   7.295 ns |  1.77 |    0.01 |
| 'Reflection with DotNext using delegate type Function<object, (object, object), object>' |                      |  29.997 ns | 0.2193 ns | 0.2051 ns |  29.995 ns |  7.26 |    0.07 |
|         'Reflection with DotNext using delegate type Function<string, (char, int), int>' |                      |  12.705 ns | 0.0410 ns | 0.0363 ns |  12.721 ns |  3.08 |    0.01 |
|                                           'Reflection with DotNext using DynamicInvoker' |                      |  28.729 ns | 0.5514 ns | 0.5900 ns |  28.433 ns |  6.99 |    0.15 |
|                                                                                          |                      |            |           |           |            |       |         |
|                                                                        '.NET reflection' | abccdahehkgbe387jwgr | 275.576 ns | 1.6060 ns | 1.5023 ns | 275.883 ns | 32.08 |    0.19 |
|                                                                            'Direct call' | abccdahehkgbe387jwgr |   8.589 ns | 0.0162 ns | 0.0144 ns |   8.589 ns |  1.00 |    0.00 |
|               'Reflection with DotNext using delegate type Func<string, char, int, int>' | abccdahehkgbe387jwgr |  14.085 ns | 0.0720 ns | 0.0639 ns |  14.079 ns |  1.64 |    0.01 |
| 'Reflection with DotNext using delegate type Function<object, (object, object), object>' | abccdahehkgbe387jwgr |  33.526 ns | 0.1700 ns | 0.1590 ns |  33.556 ns |  3.90 |    0.02 |
|         'Reflection with DotNext using delegate type Function<string, (char, int), int>' | abccdahehkgbe387jwgr |  16.886 ns | 0.2396 ns | 0.2242 ns |  16.951 ns |  1.97 |    0.03 |
|                                           'Reflection with DotNext using DynamicInvoker' | abccdahehkgbe387jwgr |  30.060 ns | 0.0953 ns | 0.1767 ns |  30.102 ns |  3.51 |    0.02 |
|                                                                                          |                      |            |           |           |            |       |         |
|                                                                        '.NET reflection' | wfjwk(...)wjbvw [52] | 276.583 ns | 1.3709 ns | 1.1447 ns | 276.860 ns | 19.75 |    0.12 |
|                                                                            'Direct call' | wfjwk(...)wjbvw [52] |  14.006 ns | 0.0904 ns | 0.0706 ns |  13.996 ns |  1.00 |    0.00 |
|               'Reflection with DotNext using delegate type Func<string, char, int, int>' | wfjwk(...)wjbvw [52] |  17.665 ns | 0.1914 ns | 0.1790 ns |  17.607 ns |  1.26 |    0.01 |
| 'Reflection with DotNext using delegate type Function<object, (object, object), object>' | wfjwk(...)wjbvw [52] |  37.263 ns | 0.0681 ns | 0.0604 ns |  37.264 ns |  2.66 |    0.01 |
|         'Reflection with DotNext using delegate type Function<string, (char, int), int>' | wfjwk(...)wjbvw [52] |  20.071 ns | 0.2297 ns | 0.2149 ns |  20.147 ns |  1.44 |    0.01 |
|                                           'Reflection with DotNext using DynamicInvoker' | wfjwk(...)wjbvw [52] |  36.154 ns | 0.7031 ns | 1.2128 ns |  35.446 ns |  2.69 |    0.04 |

DotNext Reflection library offers the best result in case when delegate type exactly matches to the reflected method with small overhead measured in a few nanoseconds.

## Static Method Call
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/TryParseReflectionBenchmark.cs) demonstrates overhead of calling static method `TryParse` of type **decimal** caused by different mechanisms:
1. Using strongly typed reflection from DotNext Reflection library: `Type<decimal>.Method.Get<TryParseDelegate>(nameof(decimal.TryParse), MethodLookup.Static)`. The delegate type exactly matches to the reflected method signature: `delegate bool TryParseDelegate(string text, out decimal result)`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<(string text, decimal result), bool>`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<(object text, object result), object>`. It is assumed that types of all parameters are not known at compile time.
1. Classic .NET reflection

|                                                                                         Method |     Mean |   Error |  StdDev | Ratio | RatioSD |
|----------------------------------------------------------------------------------------------- |---------:|--------:|--------:|------:|--------:|
|                                 Reflection with DotNext using delegate type `TryParseDelegate` | 124.1 ns | 0.44 ns | 0.39 ns |  1.00 |    0.01 |
|                                                                                  Direct call | 124.3 ns | 0.85 ns | 0.80 ns |  1.00 |    0.00 |
|    Reflection with DotNext using delegate type `Function<(string text, decimal result), bool>` | 132.0 ns | 0.35 ns | 0.33 ns |  1.06 |    0.01 |
| 'Reflection with DotNext using delegate type `Function<(object text, object result), object>`' | 144.2 ns | 0.67 ns | 0.63 ns |  1.16 |    0.01 |
|                                                 Reflection with DotNext using `DynamicInvoker` | 148.7 ns | 0.57 ns | 0.53 ns |  1.20 |    0.01 |
|                                                                              .NET reflection | 462.6 ns | 1.25 ns | 1.11 ns |  3.72 |    0.03 |

Strongly typed reflection provided by DotNext Reflection library has the same performance as direct call.

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

|                                        Method |     Mean |     Error |    StdDev |    Gen 0 |    Gen 1 |    Gen 2 | Allocated |
|---------------------------------------------- |---------:|----------:|----------:|---------:|---------:|---------:|----------:|
|       'FileBufferingWriter, synchronous mode' | 1.079 ms | 0.0208 ms | 0.0364 ms | 248.0469 | 248.0469 | 248.0469 |      1 MB |
|      'FileBufferingWriter, asynchronous mode' | 1.380 ms | 0.0276 ms | 0.0295 ms | 126.9531 | 111.3281 | 111.3281 |      1 MB |
| 'FileBufferingWriteStream, synchronouse mode' | 4.141 ms | 0.0317 ms | 0.0297 ms | 476.5625 | 359.3750 | 273.4375 |      2 MB |
| 'FileBufferingWriteStream, asynchronous mode' | 5.092 ms | 0.0557 ms | 0.0521 ms | 453.1250 | 367.1875 | 257.8125 |      2 MB |

`FileBufferingWriter` is a winner in synchronous scenario because it has native support for synchronous mode in contrast to `FileBufferingWriteStream`.

# Various Buffer Types
[This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/Buffers/MemoryStreamingBenchmark.cs) demonstrates the performance of write operation and memory consumption of the following types:
* [MemoryStream](https://docs.microsoft.com/en-us/dotnet/api/system.io.memorystream)
* [RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream)
* [SparseBufferWriter&lt;byte&gt;](xref:DotNext.Buffers.SparseBufferWriter`1)
* [PooledArrayBufferWriter&lt;byte&gt;](xref:DotNext.Buffers.PooledArrayBufferWriter`1)
* [FileBufferingWriter](xref:DotNext.IO.FileBufferingWriter)

|                        Method | TotalCount |          Mean |        Error |       StdDev | Ratio | RatioSD |    Gen 0 |    Gen 1 |    Gen 2 |   Allocated |
|------------------------------ |----------- |--------------:|-------------:|-------------:|------:|--------:|---------:|---------:|---------:|------------:|
|                  MemoryStream |        100 |      65.10 ns |     0.363 ns |     0.340 ns |  1.00 |    0.00 |   0.1097 |        - |        - |       344 B |
| PooledArrayBufferWriter<byte> |        100 |     173.01 ns |     0.757 ns |     0.708 ns |  2.66 |    0.02 |   0.0331 |        - |        - |       104 B |
|      SparseBufferWriter<byte> |        100 |     255.33 ns |     1.130 ns |     1.057 ns |  3.92 |    0.02 |   0.0610 |        - |        - |       192 B |
|           FileBufferingWriter |        100 |   2,052.94 ns |    10.153 ns |     9.498 ns | 31.53 |    0.24 |   0.0420 |        - |        - |       136 B |
|        RecyclableMemoryStream |        100 |   3,128.89 ns |    14.429 ns |    13.497 ns | 48.06 |    0.30 |   0.0839 |        - |        - |       264 B |
|                               |            |               |              |              |       |         |          |          |          |             |
|                  MemoryStream |       1000 |     119.12 ns |     1.052 ns |     0.984 ns |  1.00 |    0.00 |   0.3467 |        - |        - |     1,088 B |
|      SparseBufferWriter<byte> |       1000 |     261.20 ns |     2.168 ns |     1.692 ns |  2.20 |    0.02 |   0.0610 |        - |        - |       192 B |
| PooledArrayBufferWriter<byte> |       1000 |     393.98 ns |     2.161 ns |     2.022 ns |  3.31 |    0.04 |   0.0329 |        - |        - |       104 B |
|           FileBufferingWriter |       1000 |   2,076.33 ns |    11.508 ns |    10.764 ns | 17.43 |    0.21 |   0.0420 |        - |        - |       136 B |
|        RecyclableMemoryStream |       1000 |   3,174.63 ns |    12.924 ns |    10.792 ns | 26.68 |    0.22 |   0.0839 |        - |        - |       264 B |
|                               |            |               |              |              |       |         |          |          |          |             |
|      SparseBufferWriter<byte> |      10000 |     692.51 ns |     4.486 ns |     3.976 ns |  0.28 |    0.00 |   0.0811 |        - |        - |       256 B |
| PooledArrayBufferWriter<byte> |      10000 |   1,290.99 ns |     4.624 ns |     4.099 ns |  0.52 |    0.01 |   0.0324 |        - |        - |       104 B |
|                  MemoryStream |      10000 |   2,467.75 ns |    25.317 ns |    23.682 ns |  1.00 |    0.00 |   9.8343 |        - |        - |    30,880 B |
|           FileBufferingWriter |      10000 |   3,032.47 ns |     6.964 ns |     5.815 ns |  1.23 |    0.01 |   0.0420 |        - |        - |       136 B |
|        RecyclableMemoryStream |      10000 |   3,586.03 ns |    12.236 ns |    10.218 ns |  1.45 |    0.02 |   0.0839 |        - |        - |       264 B |
|                               |            |               |              |              |       |         |          |          |          |             |
|      SparseBufferWriter<byte> |     100000 |   4,577.32 ns |    24.336 ns |    22.764 ns |  0.07 |    0.00 |   0.1373 |        - |        - |       448 B |
|        RecyclableMemoryStream |     100000 |   7,839.17 ns |    31.838 ns |    26.586 ns |  0.12 |    0.00 |   0.0763 |        - |        - |       264 B |
| PooledArrayBufferWriter<byte> |     100000 |   8,670.98 ns |    28.679 ns |    26.826 ns |  0.13 |    0.00 |   0.0305 |        - |        - |       104 B |
|                  MemoryStream |     100000 |  64,611.89 ns |   292.558 ns |   259.345 ns |  1.00 |    0.00 |  41.6260 |  41.6260 |  41.6260 |   260,356 B |
|           FileBufferingWriter |     100000 | 119,751.00 ns | 1,909.731 ns | 1,786.363 ns |  1.85 |    0.03 |        - |        - |        - |       304 B |
|                               |            |               |              |              |       |         |          |          |          |             |
|      SparseBufferWriter<byte> |    1000000 |  43,734.05 ns |   149.028 ns |   132.109 ns |  0.05 |    0.00 |   0.1831 |        - |        - |       640 B |
|        RecyclableMemoryStream |    1000000 |  50,682.50 ns |   158.356 ns |   140.379 ns |  0.06 |    0.00 |   0.2441 |        - |        - |       896 B |
| PooledArrayBufferWriter<byte> |    1000000 |  79,814.60 ns |   277.574 ns |   259.643 ns |  0.09 |    0.00 |        - |        - |        - |       104 B |
|           FileBufferingWriter |    1000000 | 645,606.80 ns | 5,333.340 ns | 4,988.809 ns |  0.73 |    0.01 |        - |        - |        - |       305 B |
|                  MemoryStream |    1000000 | 880,304.68 ns | 3,535.776 ns | 3,307.367 ns |  1.00 |    0.00 | 499.0234 | 499.0234 | 499.0234 | 2,095,744 B |

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