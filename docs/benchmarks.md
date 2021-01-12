Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:

| Parameter | Configuration |
| ---- | ---- |
| Host | .NET 5.0.2 (CoreCLR 5.0.220.61120, CoreFX 5.0.220.61120), X64 RyuJIT |
| Job | .NET 5.0.2 (CoreCLR 5.0.220.61120, CoreFX 5.0.220.61120), X64 RyuJIT |
| LaunchCount | 1 |
| RunStrategy | Throughput |
| OS | Ubuntu 20.04.1 |
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

# Bitwise Equality
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/BitwiseEqualityBenchmark.cs) compares performance of [BitwiseComparer&lt;T&gt;.Equals](./api/DotNext.BitwiseComparer-1.yml) with overloaded equality `==` operator. Testing data types: [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `BitwiseComparer<Guid>.Equals` | 3.4195 ns |  0.0145 ns |  0.0135 ns |
| `Guid.Equals` | 1.8179 ns |  0.0132 ns |  0.0123 ns |
| `ReadOnlySpan.SequenceEqual` for `Guid` | 6.1847 ns |  0.0471 ns |  0.0440 ns |
| `BitwiseComparer<LargeStruct>.Equals` | 9.4089 ns |  0.0481 ns |  0.0426 ns |
| `LargeStruct.Equals` | 29.6790 ns |  0.1137 ns |  0.0887 ns |
| `ReadOnlySpan.SequenceEqual` for `LargeStruct` | 11.5508 ns |  0.1198 ns |  0.1062 ns |

Bitwise equality method has the better performance than field-by-field equality check especially for large value types because `BitwiseEquals` utilizes low-level optimizations performed by .NET according with underlying hardware such as SIMD. Additionally, it uses [aligned memory access](https://en.wikipedia.org/wiki/Data_structure_alignment) in constrast to [SequenceEqual](https://docs.microsoft.com/en-us/dotnet/api/system.memoryextensions.sequenceequal) method.

# Equality of Arrays
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/ArrayEqualityBenchmark.cs) compares performance of [ReadOnlySpan.SequenceEqual](https://docs.microsoft.com/en-us/dotnet/api/system.memoryextensions.sequenceequal#System_MemoryExtensions_SequenceEqual__1_System_ReadOnlySpan___0__System_ReadOnlySpan___0__), [OneDimensionalArray.BitwiseEquals](./api/DotNext.OneDimensionalArray.yml) and manual equality check between two arrays using `for` loop. The benchmark is applied to the array of [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) elements.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `Guid[].BitwiseEquals`, small arrays (~10 elements) | 9.778 ns | 0.2334 ns | 0.3702 ns |
| `ReadOnlySpan<Guid>.SequenceEqual`, small arrays (~10 elements) | 39.946 ns | 0.1607 ns | 0.1503 ns |
| `for` loop, small arrays (~10 elements) | 68.674 ns | 0.1695 ns | 0.1585 ns |
| `Guid[].BitwiseEquals`, large arrays (~100 elements) | 68.444 ns | 0.1789 ns | 0.1674 ns |
| `ReadOnlySpan<Guid>.SequenceEqual`, large arrays (~100 elements) | 377.179 ns | 1.0001 ns | 0.8351 ns |
| `for` loop, large arrays (~100 elements) | 677.581 ns | 1.2724 ns | 1.1279 ns |

Bitwise equality is an absolute winner for equality check between arrays of any size.

# Bitwise Hash Code
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/BitwiseHashCodeBenchmark.cs) compares performance of [BitwiseComparer&lt;T&gt;.GetHashCode](./api/DotNext.BitwiseComparer-1.yml) and `GetHashCode` instance method for the types [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `Guid.GetHashCode` | 1.416 ns | 0.0211 ns | 0.0198 ns |
| `BitwiseComparer<Guid>.GetHashCode` | 6.202 ns | 0.0355 ns | 0.0315 ns |
| `BitwiseComparer<LargeStructure>.GetHashCode` | 44.327 ns | 0.1936 ns | 0.1716 ns |
| `LargeStructure.GetHashCode` | 20.520 ns | 0.0666 ns | 0.0623 ns |

Bitwise hash code algorithm is slower than JIT optimizations introduced by .NET 5 but still convenient in complex cases.

# Bytes to Hex
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/HexConversionBenchmark.cs) demonstrates performance of `DotNext.Span.ToHex` extension method that allows to convert arbitrary set of bytes into hexadecimal form. It is compatible with`Span<T>` data type in constrast to [BitConverter.ToString](https://docs.microsoft.com/en-us/dotnet/api/system.bitconverter.tostring) method.

| Method | Num of Bytes | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- | ---- |
| `BitConverter.ToString` | 16 bytes | 64.27 ns | 1.259 ns | 1.051 ns |
| `Span.ToHex` | 16 bytes | 58.78 ns | 0.219 ns | 0.205 ns |
| `BitConverter.ToString` | 64 bytes | 197.94 ns | 0.620 ns | 0.550 ns |
| `Span.ToHex` | 64 bytes | 134.83 ns | 1.751 ns | 1.638 ns |
| `BitConverter.ToString` | 128 bytes | 382.76 ns | 3.908 ns | 3.263 ns |
| `Span.ToHex` | 128 bytes | 234.62 ns | 1.959 ns | 1.833 ns |
| `BitConverter.ToString` | 256 bytes | 745.88 ns | 4.340 ns | 3.847 ns |
| `Span.ToHex` | 256 bytes | 484.20 ns | 1.768 ns | 1.568 ns |

`Span.ToHex` demonstrates the best performance especially for large arrays.

# Fast Reflection
The next series of benchmarks demonstrate performance of strongly typed reflection provided by DotNext Reflection library.

## Property Getter
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/PropertyGetterReflectionBenchmark.cs) demonstrates overhead of getting instance property value caused by different mechanisms:
1. Using [FastMember](https://github.com/mgravell/fast-member) library
1. Using strongly typed reflection from DotNext Reflection library: `Type<IndexOfCalculator>.Property<int>.RequireGetter`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type `Function<object, ValueTuple, object>`. It is assumed that instance type and property type is not known at compile type (th) so the delegate performs type check on every call.
1. Classic .NET reflection

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| Direct call | 11.75 ns | 0.208 ns | 0.163 ns |
| Reflection with DotNext using delegate type `MemberGetter<IndexOfCalculator, int>` | 13.03 ns | 0.088 ns | 0.073 ns |
| Reflection with DotNext using `DynamicInvoker` | 21.71 ns | 0.112 ns | 0.100 ns |
| Reflection with DotNext using delegate type `Function<object, ValueTuple, object>` | 22.66 ns | 0.407 ns | 0.381 ns |
| `ObjectAccess` class from _FastMember_ library | 46.65 ns | 0.095 ns | 0.089 ns |
| .NET reflection | 167.05 ns | 0.609 ns | 0.570 ns |

Strongly typed reflection provided by DotNext Reflection library has the same performance as direct call.

## Instance Method Call
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/StringMethodReflectionBenchmark.cs) demonstrates overhead of calling instance method `IndexOf` of type **string** caused by different mechanisms:
1. Using strongly typed reflection from DotNext Reflection library: `Type<string>.Method<char, int>.Require<int>(nameof(string.IndexOf))`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Type<string>.RequireMethod<(char, int), int>(nameof(string.IndexOf));`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<object, (object, object), object>`. It is assumed that types of all parameters are not known at compile time.
1. Classic .NET reflection

The benchmark uses series of different strings to run the same set of tests. Worst case means that character lookup is performed for a string that doesn't contain the given character. Best case means that character lookup is performed for a string that has the given character.

| Method | Condition | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- | ---- |
| Direct call | Empty String | 5.326 ns | 0.1191 ns | 0.1783 ns |
| Direct call | Best Case | 9.883 ns | 0.1057 ns | 0.0988 ns |
| Direct call | Worst Case | 12.836 ns | 0.0516 ns | 0.0431 ns |
| Reflection with DotNext using delegate type `Func<string, char, int, int>` | Empty String | 8.619 ns | 0.1266 ns | 0.1184 ns |
| Reflection with DotNext using delegate type `Func<string, char, int, int>` | Best Case | 12.950 ns | 0.2557 ns | 0.3413 ns |
| Reflection with DotNext using delegate type `Func<string, char, int, int>` | Worst Case | 19.191 ns | 0.3604 ns | 0.4006 ns |
| Reflection with DotNext using delegate type `Function<string, (char, int), int>` | Empty String | 12.535 ns | 0.1385 ns | 0.1295 ns |
| Reflection with DotNext using delegate type `Function<string, (char, int), int>` | Best Case | 17.662 ns | 0.3306 ns | 0.3092 ns |
| Reflection with DotNext using delegate type `Function<string, (char, int), int>` | Worst Case | 21.126 ns | 0.3728 ns | 0.3487 ns |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | Empty String | 30.211 ns | 0.1373 ns | 0.1284 ns |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | Best Case | 35.754 ns | 0.0965 ns | 0.0806 ns |
| Reflection with DotNext using delegate type `Function<object, (object, object), object>` | Worst Case | 40.073 ns | 0.2489 ns | 0.2328 ns |
| .NET reflection | Empty String | 303.852 ns | 1.9509 ns | 1.8249 ns |
| .NET reflection | Best Case | 324.094 ns | 1.2919 ns | 1.1453 ns |
| .NET reflection | Worst Case | 324.064 ns | 2.8673 ns | 2.6821 ns |

DotNext Reflection library offers the best result in case when delegate type exactly matches to the reflected method with small overhead measured in a few nanoseconds.

## Static Method Call
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Reflection/TryParseReflectionBenchmark.cs) demonstrates overhead of calling static method `TryParse` of type **decimal** caused by different mechanisms:
1. Using strongly typed reflection from DotNext Reflection library: `Type<decimal>.Method.Get<TryParseDelegate>(nameof(decimal.TryParse), MethodLookup.Static)`. The delegate type exactly matches to the reflected method signature: `delegate bool TryParseDelegate(string text, out decimal result)`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<(string text, decimal result), bool>`
1. Using strongly typed reflection from DotNext Reflection library using special delegate type: `Function<(object text, object result), object>`. It is assumed that types of all parameters are not known at compile time.
1. Classic .NET reflection

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| Direct call | 119.8 ns | 2.27 ns | 4.32 ns | 117.8 ns |
| Reflection with DotNext using delegate type `TryParseDelegate` | 125.3 ns | 0.41 ns | 0.34 ns | 125.3 ns |
| Reflection with DotNext using delegate type `Function<(string text, decimal result), bool>` | 131.0 ns | 0.30 ns | 0.28 ns | 131.1 ns |
| Reflection with DotNext using delegate type `Function<(object text, object result), object>` | 147.9 ns | 0.54 ns | 0.51 ns | 147.8 ns |
| .NET reflection | 530.2 ns | 1.71 ns | 1.60 ns | 530.0 ns |

Strongly typed reflection provided by DotNext Reflection library has the same performance as direct call.

# Atomic Access to Arbitrary Value Type
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/Threading/AtomicContainerBenchmark.cs) compares performance of [Atomic&lt;T&gt;](./api/DotNext.Threading.Atomic-1.yml) and Synchronized methods. The implementation of the benchmark contains concurrent read/write threads to ensure that lock contention is in place.

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| Atomic | 342.2 us | 7.82 us | 73.57 us | 329.6 us |
| Synchronized | 962.1 us | 12.04 us | 111.03 us | 944.3 us |
| SpinLock | 1,456.6 us | 43.64 us | 408.19 us | 1,548.5 us |

# Value Delegate
[This benchmark](https://github.com/sakno/DotNext/blob/master/src/DotNext.Benchmarks/FunctionPointerBenchmark.cs) compares performance of indirect method call using classic delegates from .NET and [value delegates](./features/core/valued.md).

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| Instance method, regular delegate, has implicit **this** | 0.8176 ns | 0.0192 ns | 0.0160 ns |
| Instance method, Value Delegate, has implicit **this** | 1.5130 ns | 0.0104 ns | 0.0097 ns |
| Static method, regular delegate, large size of param type, no implicitly captured object | 10.0363 ns | 0.0599 ns | 0.0560 ns |
| Static method, Value Delegate, large size of param type, no implicitly captured object | 9.9476 ns | 0.0642 ns | 0.0569 ns |
| Static method, regular delegate, small size of param type, no implicitly captured object | 21.4590 ns | 0.1720 ns | 0.1343 ns |
| Static method, Value Delegate, small size of param type, no implicitly captured object | 22.3372 ns | 0.4040 ns | 0.3373 ns |

_Large size of param type_ means that the type of the parameter is larger than 64 bit.

Performance of value delegates is the same as of regular .NET delegates.

# File-buffering Writer
[This benchmark](https://github.com/sakno/dotNext/blob/master/src/DotNext.Benchmarks/IO/FileBufferingWriterBenchmark.cs) compares performance of [FileBufferingWriteStream](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webutilities.filebufferingwritestream) from ASP.NET Core and [FileBufferingWriter](./api/DotNext.IO.FileBufferingWriter.yml) from .NEXT library.

Both classes switching from in-memory buffer to file-based buffer during benchmark execution. Note that benchmark result highly depends on disk I/O performance. The following results were obtained using NVMe SSD.

| Method | Mean | Error | StdDev | Median |
| ---- | ---- | ---- | ---- | ---- |
| `FileBufferingWriter` in synchronous mode | 975.8 us | 18.39 us | 35.43 us | 964.4 us |
| `FileBufferingWriteStream` in synchronous mode |  4,318.6 us | 37.99 us | 31.72 us | 4,320.9 us |
| `FileBufferingWriter` in asynchronous mode | 14,108.0 us | 2,021.26 us | 5,928.00 us | 16,238.9 us |
| `FileBufferingWriteStream` in asynchronous mode | 21,263.8 us | 1,197.22 us | 3,473.35 us | 22,037.2 us |

`FileBufferingWriter` is a winner in synchronous scenario because it has native support for synchronous mode in contrast to `FileBufferingWriteStream`.

# Various Buffer Types
[This benchmark](https://github.com/sakno/dotNext/blob/master/src/DotNext.Benchmarks/Buffers/MemoryStreamingBenchmark.cs) demonstrates the performance of write operation and memory consumption of the following types:
* [MemoryStream](https://docs.microsoft.com/en-us/dotnet/api/system.io.memorystream)
* [RecyclableMemoryStream](https://github.com/microsoft/Microsoft.IO.RecyclableMemoryStream)
* [SparseBufferWriter&lt;byte&gt;](./api/DotNext.Buffers.SparseBufferWriter-1.yml)
* [PooledArrayBufferWriter&lt;byte&gt;](./api/DotNext.Buffers.PooledArrayBufferWriter-1.yml)
* [FileBufferingWriter](./api/DotNext.IO.FileBufferingWriter.yml)

|                   Buffer Type   | Written bytes |       Mean |         Error |        StdDev | Ratio | RatioSD |    Gen 0 |    Gen 1 |    Gen 2 | Allocated |
|-------------------------------- |-------------- |--------------:|--------------:|--------------:|------:|--------:|---------:|---------:|---------:|----------:|
|                  `MemoryStream` |           100 |      66.91 ns |      1.379 ns |      2.187 ns |  1.00 |    0.00 |   0.1097 |        - |        - |     344 B |
| `PooledArrayBufferWriter<byte>` |           100 |     237.78 ns |      1.819 ns |      1.519 ns |  3.53 |    0.10 |   0.0405 |        - |        - |     128 B |
|      `SparseBufferWriter<byte>` |           100 |     249.18 ns |      3.458 ns |      3.234 ns |  3.68 |    0.14 |   0.0663 |        - |        - |     208 B |
|           `FileBufferingWriter` |           100 |   1,681.56 ns |     31.827 ns |     29.771 ns | 24.85 |    0.94 |   0.0324 |        - |        - |     104 B |
|        `RecyclableMemoryStream` |           100 |   2,503.46 ns |     10.280 ns |      9.113 ns | 37.16 |    0.93 |   0.1030 |        - |        - |     328 B |
|                                 |               |               |               |               |       |         |          |          |          |           |
|                  `MemoryStream` |          1000 |     124.90 ns |      0.882 ns |      1.264 ns |  1.00 |    0.00 |   0.3467 |        - |        - |    1088 B |
|      `SparseBufferWriter<byte>` |          1000 |     273.03 ns |      2.024 ns |      1.794 ns |  2.20 |    0.03 |   0.0663 |        - |        - |     208 B |
| `PooledArrayBufferWriter<byte>` |          1000 |     491.45 ns |      2.027 ns |      1.693 ns |  3.96 |    0.04 |   0.0401 |        - |        - |     128 B |
|           `FileBufferingWriter` |          1000 |   1,704.61 ns |     33.291 ns |     31.141 ns | 13.72 |    0.39 |   0.0324 |        - |        - |     104 B |
|        `RecyclableMemoryStream` |          1000 |   2,535.13 ns |     20.769 ns |     19.427 ns | 20.40 |    0.21 |   0.1030 |        - |        - |     328 B |
|                                 |               |               |               |               |       |         |          |          |          |           |
|      `SparseBufferWriter<byte>` |         10000 |     778.99 ns |      3.440 ns |      3.050 ns |  0.32 |    0.00 |   0.0858 |        - |        - |     272 B |
| `PooledArrayBufferWriter<byte>` |         10000 |   1,570.87 ns |     11.332 ns |     10.045 ns |  0.64 |    0.01 |   0.0401 |        - |        - |     128 B |
|                  `MemoryStream` |         10000 |   2,441.00 ns |     43.130 ns |     38.234 ns |  1.00 |    0.00 |   9.8343 |        - |        - |   30880 B |
|           `FileBufferingWriter` |         10000 |   2,841.10 ns |     54.135 ns |     57.924 ns |  1.17 |    0.02 |   0.0305 |        - |        - |     104 B |
|        `RecyclableMemoryStream` |         10000 |   2,971.70 ns |     23.778 ns |     22.242 ns |  1.22 |    0.02 |   0.1030 |        - |        - |     328 B |
|                                 |               |               |               |               |       |         |          |          |          |           |
|      `SparseBufferWriter<byte>` |        100000 |   5,332.73 ns |     18.125 ns |     16.954 ns |  0.08 |    0.00 |   0.1450 |        - |        - |     464 B |
|        `RecyclableMemoryStream` |        100000 |   7,589.19 ns |     37.040 ns |     32.835 ns |  0.11 |    0.00 |   0.0916 |        - |        - |     328 B |
| `PooledArrayBufferWriter<byte>` |        100000 |   8,901.54 ns |     34.404 ns |     28.729 ns |  0.13 |    0.00 |   0.0305 |        - |        - |     128 B |
|                  `MemoryStream` |        100000 |  66,530.32 ns |  1,048.177 ns |    980.465 ns |  1.00 |    0.00 |  41.6260 |  41.6260 |  41.6260 |  260340 B |
|           `FileBufferingWriter` |        100000 | 117,812.86 ns |  2,269.839 ns |  3,533.865 ns |  1.79 |    0.06 |        - |        - |        - |     368 B |
|                                 |               |               |               |               |       |         |          |          |          |           |
|      `SparseBufferWriter<byte>` |       1000000 |  50,656.44 ns |    906.284 ns |    847.739 ns |  0.05 |    0.00 |   0.1831 |        - |        - |     656 B |
|        `RecyclableMemoryStream` |       1000000 |  53,671.61 ns |    854.939 ns |    799.711 ns |  0.06 |    0.00 |   0.1831 |        - |        - |     736 B |
| `PooledArrayBufferWriter<byte>` |       1000000 |  84,345.21 ns |  1,475.370 ns |  1,699.039 ns |  0.09 |    0.00 |        - |        - |        - |     128 B |
|           `FileBufferingWriter` |       1000000 | 739,081.13 ns | 14,452.230 ns | 17,204.351 ns |  0.79 |    0.02 |        - |        - |        - |     368 B |
|                  `MemoryStream` |       1000000 | 931,331.53 ns | 17,399.875 ns | 14,529.684 ns |  1.00 |    0.00 | 498.0469 | 498.0469 | 498.0469 | 2095552 B |