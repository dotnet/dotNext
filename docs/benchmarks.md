Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:

| Parameter | Configuration |
| ---- | ---- |
| Host | .NET 6.0.0 (6.0.21.45113), X64 RyuJIT, X64 RyuJIT |
| Job | .NET 6.0.0 (6.0.21.45113), X64 RyuJIT, X64 RyuJIT |
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
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/HexConversionBenchmark.cs) demonstrates performance of `DotNext.Span.ToHex` extension method that allows to convert arbitrary set of bytes into hexadecimal form. It is compatible with`Span<T>` data type in constrast to [BitConverter.ToString](https://docs.microsoft.com/en-us/dotnet/api/system.bitconverter.tostring) method.

|                Method |     Bytes |        Mean |     Error |    StdDev |
|---------------------- |---------- |------------:|----------:|----------:|
| BitConverter.ToString |  16 bytes |    59.81 ns |  0.379 ns |  0.296 ns |
|            Span.ToHex |  16 bytes |    54.27 ns |  0.754 ns |  0.705 ns |
| BitConverter.ToString |  64 bytes |   195.29 ns |  1.175 ns |  1.099 ns |
|            Span.ToHex |  64 bytes |   129.07 ns |  0.564 ns |  0.471 ns |
| BitConverter.ToString | 128 bytes |   520.31 ns |  9.848 ns | 14.740 ns |
|            Span.ToHex | 128 bytes |   228.17 ns |  1.084 ns |  0.961 ns |
| BitConverter.ToString | 256 bytes | 1,013.92 ns | 17.653 ns | 15.649 ns |
|            Span.ToHex | 256 bytes |   458.78 ns |  5.442 ns |  4.545 ns |

`Span.ToHex` demonstrates the best performance especially for large arrays.

# Fast Reflection
The next series of benchmarks demonstrate performance of strongly typed reflection provided by DotNext Reflection library.

## Property Getter
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/PropertyGetterReflectionBenchmark.cs) demonstrates overhead of getting instance property value caused by different mechanisms:
1. Using [FastMember](https://github.com/mgravell/fast-member) library
1. Using strongly typed reflection from DotNext Reflection library: `Type<IndexOfCalculator>.Property<int>.RequireGetter`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type `Function<object, ValueTuple, object>`. It is assumed that instance type and property type is not known at compile type (th) so the delegate performs type check on every call.
1. Classic .NET reflection

|                                                                             Method |      Mean |    Error |   StdDev | Ratio | RatioSD |
|----------------------------------------------------------------------------------- |----------:|---------:|---------:|------:|--------:|
|                                                                      Direct call |  11.18 ns | 0.083 ns | 0.073 ns |  1.00 |    0.00 |
| Reflection with DotNext using delegate type `MemberGetter<IndexOfCalculator, int>` |  11.83 ns | 0.060 ns | 0.056 ns |  1.06 |    0.01 |
|                                     Reflection with DotNext using `DynamicInvoker` |  21.46 ns | 0.131 ns | 0.122 ns |  1.92 |    0.02 |
| Reflection with DotNext using delegate type `Function<object, ValueTuple, object>` |  20.49 ns | 0.127 ns | 0.106 ns |  1.83 |    0.02 |
|                                       ObjectAccess class from _FastMember_ library |  41.46 ns | 0.185 ns | 0.173 ns |  3.71 |    0.03 |
|                                                                    .NET reflection | 135.60 ns | 1.085 ns | 1.015 ns | 12.13 |    0.15 |

Strongly typed reflection provided by DotNext Reflection library has the same performance as direct call.

## Instance Method Call
[This benchmark](https://github.com/dotnet/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/StringMethodReflectionBenchmark.cs) demonstrates overhead of calling instance method `IndexOf` of type **string** caused by different mechanisms:
1. Using strongly typed reflection from DotNext Reflection library: `Type<string>.Method<char, int>.Require<int>(nameof(string.IndexOf))`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Type<string>.RequireMethod<(char, int), int>(nameof(string.IndexOf));`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<object, (object, object), object>`. It is assumed that types of all parameters are not known at compile time.
1. Classic .NET reflection

The benchmark uses series of different strings to run the same set of tests. Worst case means that character lookup is performed for a string that doesn't contain the given character. Best case means that character lookup is performed for a string that has the given character.

|                                                                                   Method |          StringValue |       Mean |     Error |    StdDev | Ratio | RatioSD |
|----------------------------------------------------------------------------------------- |--------------------- |-----------:|----------:|----------:|------:|--------:|
|                                                                        .NET reflection |                      | 267.051 ns | 0.7739 ns | 0.6860 ns | 61.84 |    2.42 |
|                                                                            Direct call |                      |   4.341 ns | 0.1173 ns | 0.1483 ns |  1.00 |    0.00 |
|               Reflection with DotNext using delegate type `Func<string, char, int, int>` |                      |   8.694 ns | 0.0299 ns | 0.0265 ns |  2.01 |    0.08 |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` |                      |  29.566 ns | 0.1688 ns | 0.1496 ns |  6.85 |    0.26 |
|         Reflection with DotNext using delegate type `Function<string, (char, int), int>` |                      |  12.496 ns | 0.0556 ns | 0.0521 ns |  2.89 |    0.10 |
|                                           Reflection with DotNext using `DynamicInvoker` |                      |  27.646 ns | 0.2521 ns | 0.2235 ns |  6.40 |    0.25 |
|                                                                                          |                      |            |           |           |       |         |
|                                                                        .NET reflection | abccdahehkgbe387jwgr | 276.495 ns | 1.3943 ns | 1.3042 ns | 38.86 |    0.35 |
|                                                                            Direct call | abccdahehkgbe387jwgr |   7.111 ns | 0.0502 ns | 0.0419 ns |  1.00 |    0.00 |
|               Reflection with DotNext using delegate type `Func<string, char, int, int>` | abccdahehkgbe387jwgr |  10.947 ns | 0.0467 ns | 0.0390 ns |  1.54 |    0.01 |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | abccdahehkgbe387jwgr |  30.356 ns | 0.1505 ns | 0.1408 ns |  4.27 |    0.03 |
|         Reflection with DotNext using delegate type `Function<string, (char, int), int>` | abccdahehkgbe387jwgr |  15.782 ns | 0.1097 ns | 0.0972 ns |  2.22 |    0.02 |
|                                           Reflection with DotNext using `DynamicInvoker` | abccdahehkgbe387jwgr |  34.757 ns | 0.1380 ns | 0.1223 ns |  4.89 |    0.03 |
|                                                                                          |                      |            |           |           |       |         |
|                                                                        .NET reflection | wfjwk(...)wjbvw [52] | 268.056 ns | 1.8884 ns | 1.6740 ns | 20.30 |    0.18 |
|                                                                            Direct call | wfjwk(...)wjbvw [52] |  13.207 ns | 0.1005 ns | 0.0891 ns |  1.00 |    0.00 |
|               Reflection with DotNext using delegate type `Func<string, char, int, int>` | wfjwk(...)wjbvw [52] |  13.430 ns | 0.0718 ns | 0.0599 ns |  1.02 |    0.01 |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | wfjwk(...)wjbvw [52] |  34.486 ns | 0.1407 ns | 0.1175 ns |  2.61 |    0.02 |
|         Reflection with DotNext using delegate type `Function<string, (char, int), int>` | wfjwk(...)wjbvw [52] |  17.992 ns | 0.0771 ns | 0.0683 ns |  1.36 |    0.01 |
|                                           Reflection with DotNext using `DynamicInvoker` | wfjwk(...)wjbvw [52] |  35.445 ns | 0.2010 ns | 0.1880 ns |  2.68 |    0.03 |

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
|       Atomic |   317.9 us |  9.36 us |  86.20 us |   305.8 us |
| Synchronized |   977.9 us | 10.77 us |  98.57 us |   967.1 us |
|     SpinLock | 1,891.5 us | 58.82 us | 556.19 us | 1,772.4 us |

# File-buffering Writer
[This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/IO/FileBufferingWriterBenchmark.cs) compares performance of [FileBufferingWriteStream](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webutilities.filebufferingwritestream) from ASP.NET Core and [FileBufferingWriter](xref:DotNext.IO.FileBufferingWriter) from .NEXT library.

Both classes switching from in-memory buffer to file-based buffer during benchmark execution. Note that benchmark result highly depends on disk I/O performance. The following results were obtained using NVMe SSD.

|                                        Method |     Mean |     Error |    StdDev |   Median |
|---------------------------------------------- |---------:|----------:|----------:|---------:|
|       'FileBufferingWriter, synchronous mode' | 1.083 ms | 0.0147 ms | 0.0137 ms | 1.089 ms |
|      'FileBufferingWriter, asynchronous mode' | 1.532 ms | 0.0501 ms | 0.1477 ms | 1.447 ms |
| 'FileBufferingWriteStream, synchronouse mode' | 4.251 ms | 0.0434 ms | 0.0385 ms | 4.242 ms |
| 'FileBufferingWriteStream, asynchronous mode' | 5.127 ms | 0.0683 ms | 0.0639 ms | 5.106 ms |

`FileBufferingWriter` is a winner in synchronous scenario because it has native support for synchronous mode in contrast to `FileBufferingWriteStream`.

# Various Buffer Types
[This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/Buffers/MemoryStreamingBenchmark.cs) demonstrates the performance of write operation and memory consumption of the following types:
* [MemoryStream](https://docs.microsoft.com/en-us/dotnet/api/system.io.memorystream)
* [RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream)
* [SparseBufferWriter&lt;byte&gt;](xref:DotNext.Buffers.SparseBufferWriter`1)
* [PooledArrayBufferWriter&lt;byte&gt;](xref:DotNext.Buffers.PooledArrayBufferWriter`1)
* [FileBufferingWriter](xref:DotNext.IO.FileBufferingWriter)

|                   Buffer Type | TotalCount |          Mean |         Error |        StdDev | Ratio | RatioSD |    Gen 0 |    Gen 1 |    Gen 2 |   Allocated |
|------------------------------ |----------- |--------------:|--------------:|--------------:|------:|--------:|---------:|---------:|---------:|------------:|
|                  `MemoryStream` |        100 |      60.39 ns |      0.674 ns |      0.598 ns |  1.00 |    0.00 |   0.1097 |        - |        - |       344 B |
| `PooledArrayBufferWriter<byte>` |        100 |     171.04 ns |      0.719 ns |      0.672 ns |  2.83 |    0.03 |   0.0355 |        - |        - |       112 B |
|      `SparseBufferWriter<byte>` |        100 |     240.96 ns |      2.217 ns |      1.731 ns |  3.99 |    0.05 |   0.0610 |        - |        - |       192 B |
|           `FileBufferingWriter` |        100 |   2,230.30 ns |     14.776 ns |     13.822 ns | 36.95 |    0.49 |   0.0343 |        - |        - |       112 B |
|        `RecyclableMemoryStream` |        100 |   2,418.88 ns |     13.071 ns |     11.587 ns | 40.06 |    0.39 |   0.1183 |        - |        - |       376 B |
|                               |            |               |               |               |       |         |          |          |          |             |
|                  `MemoryStream` |       1000 |     114.40 ns |      1.511 ns |      1.413 ns |  1.00 |    0.00 |   0.3468 |        - |        - |     1,088 B |
|      `SparseBufferWriter<byte>` |       1000 |     262.99 ns |      1.076 ns |      0.954 ns |  2.30 |    0.03 |   0.0610 |        - |        - |       192 B |
| `PooledArrayBufferWriter<byte>` |       1000 |     393.58 ns |      1.456 ns |      1.291 ns |  3.44 |    0.04 |   0.0353 |        - |        - |       112 B |
|           `FileBufferingWriter` |       1000 |   2,112.13 ns |     12.391 ns |     10.984 ns | 18.48 |    0.24 |   0.0343 |        - |        - |       112 B |
|        `RecyclableMemoryStream` |       1000 |   2,485.95 ns |     12.796 ns |     11.970 ns | 21.73 |    0.28 |   0.1183 |        - |        - |       376 B |
|                               |            |               |               |               |       |         |          |          |          |             |
|      `SparseBufferWriter<byte>` |      10000 |     771.29 ns |      2.057 ns |      1.823 ns |  0.32 |    0.00 |   0.0811 |        - |        - |       256 B |
| `PooledArrayBufferWriter<byte>` |      10000 |   1,293.53 ns |      7.453 ns |      6.224 ns |  0.53 |    0.00 |   0.0343 |        - |        - |       112 B |
|                  `MemoryStream` |      10000 |   2,419.10 ns |     21.564 ns |     19.116 ns |  1.00 |    0.00 |   9.8343 |        - |        - |    30,880 B |
|        `RecyclableMemoryStream` |      10000 |   2,891.81 ns |     11.367 ns |      9.492 ns |  1.19 |    0.01 |   0.1183 |        - |        - |       376 B |
|           `FileBufferingWriter` |      10000 |   3,004.68 ns |     13.759 ns |     12.870 ns |  1.24 |    0.01 |   0.0343 |        - |        - |       112 B |
|                               |            |               |               |               |       |         |          |          |          |             |
|      `SparseBufferWriter<byte>` |     100000 |   5,122.79 ns |     27.198 ns |     25.441 ns |  0.08 |    0.00 |   0.1373 |        - |        - |       448 B |
|        `RecyclableMemoryStream` |     100000 |   7,108.29 ns |     30.354 ns |     25.347 ns |  0.11 |    0.00 |   0.1144 |        - |        - |       376 B |
| `PooledArrayBufferWriter<byte>` |     100000 |   8,401.60 ns |     41.469 ns |     38.790 ns |  0.13 |    0.00 |   0.0305 |        - |        - |       112 B |
|                  `MemoryStream` |     100000 |  65,821.24 ns |    781.429 ns |    692.716 ns |  1.00 |    0.00 |  41.6260 |  41.6260 |  41.6260 |   260,356 B |
|           `FileBufferingWriter` |     100000 | 130,246.81 ns |  2,765.125 ns |  8,109.632 ns |  1.94 |    0.09 |        - |        - |        - |       480 B |
|                               |            |               |               |               |       |         |          |          |          |             |
|        `RecyclableMemoryStream` |    1000000 |  50,008.15 ns |    224.449 ns |    209.950 ns |  0.06 |    0.00 |   0.3052 |        - |        - |     1,008 B |
|      `SparseBufferWriter<byte>` |    1000000 |  51,783.54 ns |    356.691 ns |    316.197 ns |  0.06 |    0.00 |   0.1831 |        - |        - |       640 B |
| `PooledArrayBufferWriter<byte>` |    1000000 |  83,773.13 ns |    430.674 ns |    402.853 ns |  0.09 |    0.00 |        - |        - |        - |       112 B |
|           `FileBufferingWriter` |    1000000 | 736,273.23 ns | 12,760.737 ns | 11,312.061 ns |  0.82 |    0.01 |        - |        - |        - |       481 B |
|                  `MemoryStream` |    1000000 | 899,134.70 ns |  6,252.141 ns |  5,542.360 ns |  1.00 |    0.00 | 499.0234 | 499.0234 | 499.0234 | 2,095,744 B |

# TypeMap
[TypeMap&lt;TValue&gt;](xref:DotNext.Collections.Specialized.TypeMap`1) and [ConcurrentTypeMap&lt;TValue&gt;](xref:DotNext.Collections.Specialized.ConcurrentTypeMap`1) are specialized dictionaries where the keys are represented by the types passed as generic arguments. The are optimized in a way to be more performant than classic [Dictionary&lt;Type,TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2) and [ConcurrentDictionary&lt;Type,TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2). [This benchmark](https://github.com/dotnet/dotNext/blob/master/src/DotNext.Benchmarks/Collections/Specialized/TypeMapBenchmark.cs) demonstrates efficiency of the specialized collections:

|                                        Method |      Mean |     Error |    StdDev |
|---------------------------------------------- |----------:|----------:|----------:|
|              `TypeMap`, `Set` + `TryGetValue` |  1.860 ns | 0.0173 ns | 0.0162 ns |
|           `Dictionary`, `Set` + `TryGetValue` | 34.212 ns | 0.1182 ns | 0.1048 ns |
|    `ConcurrentTypeMap`, `Set` + `TryGetValue` | 20.773 ns | 0.1711 ns | 0.1600 ns |
| `ConcurrentDictionary`, `Set` + `TryGetValue` | 58.532 ns | 0.2534 ns | 0.2247 ns |
|             `ConcurrentTypeMap`, `GetOrAdd`   | 10.064 ns | 0.0572 ns | 0.0535 ns |
|          `ConcurrentDictionary`, `GetOrAdd`   | 16.246 ns | 0.1248 ns | 0.1042 ns |