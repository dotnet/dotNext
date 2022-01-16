.NEXT
====
[![Build Status](https://dev.azure.com/dotnet/dotNext/_apis/build/status/dotnet.dotNext?branchName=master)](https://dev.azure.com/dotnet/dotNext/_build/latest?definitionId=1&branchName=master)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/dotnet/dotNext/blob/master/LICENSE)
![Test Coverage](https://img.shields.io/azure-devops/coverage/dotnet/dotnext/160/master)
[![CodeQL](https://github.com/dotnet/dotNext/workflows/CodeQL/badge.svg)](https://github.com/dotnet/dotNext/actions?query=workflow%3ACodeQL)
[![Join the chat](https://badges.gitter.im/dot_next/community.svg)](https://gitter.im/dot_next/community)

.NEXT (dotNext) is a set of powerful libraries aimed to improve development productivity and extend .NET API with unique features. Some of these features are planned in future releases of .NET platform but already implemented in the library:

| Proposal | Implementation |
| ---- | ---- |
| [Interop between function pointer and delegate](https://github.com/dotnet/csharplang/discussions/3680) | [DelegateHelpers](https://dotnet.github.io/dotNext/api/DotNext.DelegateHelpers.html) factory methods |
| [Check if an instance of T is default(T)](https://github.com/dotnet/corefx/issues/16209) | [IsDefault() method](https://dotnet.github.io/dotNext/api/DotNext.Runtime.Intrinsics.html) |
| [Concept Types](https://github.com/dotnet/csharplang/issues/110) | [Documentation](https://dotnet.github.io/dotNext/features/concept.html) |
| [Expression Trees covering additional language constructs](https://github.com/dotnet/csharplang/issues/158), i.e. `foreach`, `await`, patterns, multi-line lambda expressions | [Metaprogramming](https://dotnet.github.io/dotNext/features/metaprogramming/index.html) |
| [Async Locks](https://github.com/dotnet/corefx/issues/34073) | [Documentation](https://dotnet.github.io/dotNext/features/threading/index.html) |
| [High-performance general purpose Write-Ahead Log](https://github.com/dotnet/corefx/issues/25034) | [Persistent Log](https://dotnet.github.io/dotNext/features/cluster/wal.html)  |
| [Memory-mapped file as Memory&lt;byte&gt;](https://github.com/dotnet/runtime/issues/37227) | [MemoryMappedFileExtensions](https://dotnet.github.io/dotNext/features/io/mmfile.html) |
| [Memory-mapped file as ReadOnlySequence&lt;byte&gt;](https://github.com/dotnet/runtime/issues/24805) | [ReadOnlySequenceAccessor](https://dotnet.github.io/dotNext/api/DotNext.IO.MemoryMappedFiles.ReadOnlySequenceAccessor.html) |
| [A dictionary where the keys are represented by generic arguments](https://github.com/dotnet/runtime/issues/59718) | [Documentation](https://dotnet.github.io/dotNext/features/core/typem.html) |
| [Process asynchronous tasks as they complete](https://github.com/dotnet/runtime/issues/61959) | [Documentation](https://dotnet.github.io/dotNext/features/threading/taskpipe.html) |
| [Soft References](https://github.com/dotnet/runtime/issues/63113) | [Documentation](https://dotnet.github.io/dotNext/features/core/softref.html) |

Quick overview of additional features:

* [Attachment of user data to an arbitrary objects](https://dotnet.github.io/dotNext/features/core/userdata.html)
* Extended set of [atomic operations](https://dotnet.github.io/dotNext/features/core/atomic.html). Inspired by [AtomicInteger](https://docs.oracle.com/javase/10/docs/api/java/util/concurrent/atomic/AtomicInteger.html) and friends from Java
* [Fast Reflection](https://dotnet.github.io/dotNext/features/reflection/fast.html)
* Fast conversion of bytes to hexadecimal representation and vice versa using `ToHex` and `FromHex` methods from [Span](https://dotnet.github.io/dotNext/api/DotNext.Span.html) static class
* `ManualResetEvent`, `ReaderWriterLockSlim` and other synchronization primitives now have their [asynchronous versions](https://dotnet.github.io/dotNext/features/threading/rwlock.html)
* [Atomic](https://dotnet.github.io/dotNext/features/core/atomic.html) memory access operations for arbitrary value types including enums
* [PipeExtensions](https://dotnet.github.io/dotNext/api/DotNext.IO.Pipelines.PipeExtensions.html) provides high-level I/O operations for pipelines such as string encoding and decoding
* A rich set of high-performance [memory buffers](https://dotnet.github.io/dotNext/features/io/buffers.html) for efficient I/O
* String formatting, encoding and decoding with low GC pressure: [dynamic char buffers](https://dotnet.github.io/dotNext/features/io/buffers.html#char-buffer)
* Fully-featured [Raft implementation](https://github.com/dotnet/dotNext/tree/master/src/cluster)
* Fully-featured [HyParView implementation](https://github.com/dotnet/dotNext/tree/master/src/cluster)

All these things are implemented in 100% managed code on top of existing .NET API without modifications of Roslyn compiler or CoreFX libraries.

# Quick Links

* [Features](https://dotnet.github.io/dotNext/features/core/index.html)
* [API documentation](https://dotnet.github.io/dotNext/api.html)
* [Benchmarks](https://dotnet.github.io/dotNext/benchmarks.html)
* [NuGet Packages](https://www.nuget.org/profiles/rvsakno)

# What's new
Release Date: 01-05-2022

<a href="https://www.nuget.org/packages/dotnext/4.2.0">DotNext 4.2.0</a>
* Improved scalability of mechanism that allows to attach custom data to arbitrary objects using `UserDataStorage` and `UserDataSlot<T>` types. The improvement works better in high workloads without the risk of lock contention but requires a bit more CPU cycles to obtain the data attached to the object
* Added ability to enumerate values stored in `TypeMap<T>` or `ConcurrentTypeMap<T>`
* Improved debugging experience of `UserDataStorage` type
* Added `Dictionary.Empty` static method that allows to obtain a singleton of empty [IReadOnlyDictionary&lt;TKey, TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlydictionary-2)
* Fixed decoding buffer oveflow in `Base64Decoder` type
* Added `Base64Encoder` type for fast encoding of large binary data
* Deprecation of `Sequence.FirstOrEmpty` extension methods in favor of `Sequence.FirstOrNone`
* Fixed [#91](https://github.com/dotnet/dotNext/pull/91)
* Public constructors of `PooledBufferWriter` and `PooledArrayBufferWriter` with parameters are obsolete in favor of init-only properties
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members
* Optimized performance of `Timeout`, `Optional<T>`, `Result<T>` and `Result<T, TError>` types
* Introduced `DotNext.Runtime.SoftReference` data type in addition to [WeakReference](https://docs.microsoft.com/en-us/dotnet/api/system.weakreference) from .NET

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.2.0">DotNext.Metaprogramming 4.2.0</a>
* Improved overall performance of some scenarios where `UserDataStorage` is used
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members

<a href="https://www.nuget.org/packages/dotnext.reflection/4.2.0">DotNext.Reflection 4.2.0</a>
* Improved overall performance of some scenarios where `UserDataStorage` is used
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.2.0">DotNext.Unsafe 4.2.0</a>
* Updated dependencies
* Reduced size of the compiled assembly: omit private and internal member's nullability attributes

<a href="https://www.nuget.org/packages/dotnext.threading/4.2.0">DotNext.Threading 4.2.0</a>
* Reduced execution time of `CreateTask` overloads declared in `ValueTaskCompletionSource` and `ValueTaskCompletionSource<T>` classes
* Added overflow check to `AsyncCounter` class
* Improved debugging experience of all asynchronous locks
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members
* Reduced lock contention that can be caused by asynchronous locks in concurrent scenarios
* Added `Reset()` method to `TaskCompletionPipe<T>` that allows to reuse the pipe

<a href="https://www.nuget.org/packages/dotnext.io/4.2.0">DotNext.IO 4.2.0</a>
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members
* `FileWriter` now implements [IBufferWriter&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1)

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.2.0">DotNext.Net.Cluster 4.2.0</a>
* Improved compatibility with IL trimming
* Reduced size of the compiled assembly: omit private and internal member's nullability attributes
* Completely rewritten implementation of TCP transport: better buffering and less network overhead. This version of protocol is not binary compatible with any version prior to 4.2.0
* Increased overall stability of the cluster
* Fixed bug with incorrect calculation of the offset within partition file when using persistent WAL. The bug could prevent the node to start correctly with non-empty WAL

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.2.0">DotNext.AspNetCore.Cluster 4.2.0</a>
* Improved compatibility with IL trimming
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members

Changelog for previous versions located [here](./CHANGELOG.md).

# Release & Support Policy
The libraries are versioned according with [Semantic Versioning 2.0](https://semver.org/).

| Version | .NET compatibility | Support Level |
| ---- | ---- | ---- |
| 0.x | .NET Standard 2.0 | Not Supported |
| 1.x | .NET Standard 2.0 | Not Supported |
| 2.x | .NET Standard 2.1 | Not Supported |
| 3.x | .NET Standard 2.1, .NET 5 | Maintenance |
| 4.x | .NET 6 | Active development |

_Maintenance_ support level means that new releases will contain bug fixes only.

# Development Process
Philosophy of development process:
1. All libraries in .NEXT family are available for the wide range of .NET runtimes: Mono, .NET, Blazor
1. Compatibility with R2R/AOT compiler should be checked for every release
1. Minimize set of dependencies
1. Provide high-quality documentation
1. Stay cross-platform
1. Provide benchmarks

# Contributing
This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).
For more information see the [Code of Conduct FAQ](https://www.contributor-covenant.org/faq/) or
contact [conduct@dotnetfoundation.org](mailto:conduct@dotnetfoundation.org) with any additional questions or comments.