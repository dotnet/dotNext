Benchmarks
====
Microbenchmarks are important part of DotNext library to prove than important features can speed up performance of your application or, at least, is not slowing down it.

The configuration of all benchmarks:
| Parameter | Configuration |
| ---- | ---- |
| Host | .NET Core 2.2.3 (CoreCLR 4.6.27414.05, CoreFX 4.6.27414.05), 64bit RyuJIT |
| Job | .NET Core 2.2.3 (CoreCLR 4.6.27414.05, CoreFX 4.6.27414.05), 64bit RyuJIT |
| LaunchCount | 1 |
| RunStrategy | Throughput |
| OS | Ubuntu 18.04 |
| CPU | Intel Core i7-6700HQ CPU 2.60GHz (Skylake) |
| Number of CPUs | 1 |
| Physical Cores | 4 |
| Logiccal Cores | 8 |
| RAM | 24 GB |

# Bitwise Equality
This benchmark compares performance of [ValueType&lt;T&gt;.BitwiseEquals](../api/DotNext.ValueType-1.yml) with overloaded equality `==` operator. Testing data types: [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) and custom value type with multiple fields.

| Method | Mean | Error | StdDev |
| ---- | ---- | ---- | ---- |
| `ValueType<Guid>.BitwiseEquals` |  9.627 ns | 0.2266 ns | 0.3461 ns |
| `Guid.Equals` | 12.320 ns | 0.2798 ns | 0.4101 ns |
| `ValueType<BigStruct>.BitwiseEquals` | 27.097 ns | 0.5794 ns | 1.2221 ns |
| `BigStruct.Equals` | 53.299 ns | 0.8754 ns | 0.7760 ns |

Bitwise equality method has the better performance than field-by-field equality check because `BitwiseEquals` utilizes low-level optimizations performed by .NET Core according with underlying hardware such as SIMD.