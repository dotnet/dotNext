.NEXT
====
[![Build Status](https://dev.azure.com/rvsakno/dotNext/_apis/build/status/sakno.dotNext?branchName=master)](https://dev.azure.com/rvsakno/dotNext/_build/latest?definitionId=1&branchName=master)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/sakno/dotNext/blob/master/LICENSE)
![Test Coverage](https://img.shields.io/azure-devops/coverage/rvsakno/dotnext/1/master)
[![Total alerts](https://img.shields.io/lgtm/alerts/g/sakno/dotNext.svg?logo=lgtm&logoWidth=18)](https://lgtm.com/projects/g/sakno/dotNext/alerts/)
[![Join the chat](https://badges.gitter.im/dot_next/community.svg)](https://gitter.im/dot_next/community)

.NEXT (dotNext) is a set of powerful libraries aimed to improve development productivity and extend .NET API with unique features. Some of these features are planned in future releases of .NET platform but already implemented in the library:

| Proposal | Implementation |
| ---- | ---- |
| [Interop between function pointer and delegate](https://github.com/dotnet/csharplang/discussions/3680) | [DelegateHelpers](https://www.fuget.org/packages/DotNext/latest/net5.0/lib/DotNext.dll/DotNext/DelegateHelpers) factory methods |
| [Check if an instance of T is default(T)](https://github.com/dotnet/corefx/issues/16209) | [IsDefault() method](https://www.fuget.org/packages/DotNext/latest/lib/net5.0/DotNext.dll/DotNext.Runtime/Intrinsics) |
| [Concept Types](https://github.com/dotnet/csharplang/issues/110) | [Documentation](https://sakno.github.io/dotNext/features/concept.html) |
| [Expression Trees covering additional language constructs](https://github.com/dotnet/csharplang/issues/158), i.e. `foreach`, `await`, patterns, multi-line lambda expressions | [Metaprogramming](https://sakno.github.io/dotNext/features/metaprogramming/index.html) |
| [Async Locks](https://github.com/dotnet/corefx/issues/34073) | [Documentation](https://sakno.github.io/dotNext/features/threading/index.html) |
| [High-performance general purpose Write-Ahead Log](https://github.com/dotnet/corefx/issues/25034) | [Persistent Log](https://sakno.github.io/dotNext/features/cluster/wal.html)  |
| [Memory-mapped file as Memory&lt;byte&gt;](https://github.com/dotnet/runtime/issues/37227) | [MemoryMappedFileExtensions](https://sakno.github.io/dotNext/features/io/mmfile.html) |
| [Memory-mapped file as ReadOnlySequence&lt;byte&gt;](https://github.com/dotnet/runtime/issues/24805) | [ReadOnlySequenceAccessor](https://www.fuget.org/packages/DotNext.IO/latest/lib/net5.0/DotNext.IO.dll/DotNext.IO.MemoryMappedFiles/ReadOnlySequenceAccessor) |

Quick overview of additional features:

* [Attachment of user data to an arbitrary objects](https://sakno.github.io/dotNext/features/core/userdata.html)
* [Automatic generation of Equals/GetHashCode](https://sakno.github.io/dotNext/features/core/autoeh.html) for an arbitrary type at runtime which is much better that Visual Studio compile-time helper for generating these methods
* Extended set of [atomic operations](https://sakno.github.io/dotNext/features/core/atomic.html). Inspired by [AtomicInteger](https://docs.oracle.com/javase/10/docs/api/java/util/concurrent/atomic/AtomicInteger.html) and friends from Java
* [Fast Reflection](https://sakno.github.io/dotNext/features/reflection/fast.html)
* Fast conversion of bytes to hexadecimal representation and vice versa using `ToHex` and `FromHex` methods from [Span](https://www.fuget.org/packages/DotNext/latest/lib/net5.0/DotNext.dll/DotNext/Span) static class
* `ManualResetEvent`, `ReaderWriterLockSlim` and other synchronization primitives now have their [asynchronous versions](https://sakno.github.io/dotNext/features/threading/rwlock.html)
* [Atomic](https://sakno.github.io/dotNext/features/core/atomic.html) memory access operations for arbitrary value types including enums
* [PipeExtensions](https://www.fuget.org/packages/DotNext.IO/latest/lib/net5.0/DotNext.IO.dll/DotNext.IO.Pipelines/PipeExtensions) provides high-level I/O operations for pipelines such as string encoding and decoding
* Various high-performance [growable buffers](https://sakno.github.io/dotNext/features/io/buffers.html) for efficient I/O
* Fully-featured [Raft implementation](https://github.com/sakno/dotNext/tree/master/src/cluster)

All these things are implemented in 100% managed code on top of existing .NET API without modifications of Roslyn compiler or CoreFX libraries.

# Quick Links

* [Features](https://sakno.github.io/dotNext/features/core/index.html)
* [API documentation](https://sakno.github.io/dotNext/api.html)
* [Benchmarks](https://sakno.github.io/dotNext/benchmarks.html)
* [NuGet Packages](https://www.nuget.org/profiles/rvsakno)

Documentation for older versions:
* [1.x](https://sakno.github.io/dotNext/versions/1.x/index.html)
* [2.x](https://sakno.github.io/dotNext/versions/2.x/index.html)

# What's new
Release Date: 06-03-2021

<a href="https://www.nuget.org/packages/dotnext/3.2.0">DotNext 3.2.0</a>
* Added `TryDetachBuffer` method to `BufferWriterSlim<T>` type that allows to flow buffer in async scenarios
* Added `TryGetWrittenContent` method to `SparseBufferWriter<T>` that allows to obtain the written buffer if it is represented by contiguous memory block
* Added `OptionalConverterFactory` class that allows to use `Optional<T>` data type in JSON serialization. This type allows to hide data from JSON if the property of field has undefined value. Useful for designing DTOs for REST API with partial resource updates via PATCH method. Available only when target is .NET 5.
* Added `TryResize` and `Resize` methods to `MemoryOwner<T>` value type
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/3.2.0">DotNext.Metaprogramming 3.2.0</a>
* Call site optimization for `AsDynamic()` extension method that allows to construct LINQ expression tree on-the-fly using C# expressions
* Fixed [70](https://github.com/sakno/dotNext/issues/70)

<a href="https://www.nuget.org/packages/dotnext.reflection/3.2.0">DotNext.Reflection 3.2.0</a>
* Respect volatile modifier when reading/writing field

<a href="https://www.nuget.org/packages/dotnext.unsafe/3.2.0">DotNext.Unsafe 3.2.0</a>
* Added additional overloads to `Pointer<T>` value type with **nuint** parameter

<a href="https://www.nuget.org/packages/dotnext.threading/3.2.0">DotNext.Threading 3.2.0</a>
* Added `EnsureState` to `AsyncTrigger` class as synchronous alternative with fail-fast behavior

<a href="https://www.nuget.org/packages/dotnext.io/3.2.0">DotNext.IO 3.2.0</a>
* Improved performance of all `IAsyncBinaryReader` interface implementations
* Added `TryReadBlock` extension method that allows to read the block of memory from pipe synchronously
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.2.0">DotNext.Net.Cluster 3.2.0</a>
* Smallish improvements of I/O operations related to log entries
* Improved performance of background compaction algorithm
* Persistent WAL now supports concurrent read/write. Appending of new log entries to the log tail doesn't suspend readers anymore
* Added event id and event name to all log messages

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.2.0">DotNext.AspNetCore.Cluster 3.2.0</a>
* Improved performance of log entries decoding on receiver side
* Added event id and event name to all log messages

Changelog for previous versions located [here](./CHANGELOG.md).

# Release & Support Policy
The libraries are versioned according with [Semantic Versioning 2.0](https://semver.org/).

| Version | .NET compatibility | Support Level |
| ---- | ---- | ---- |
| 0.x | .NET Standard 2.0 | Not Supported |
| 1.x | .NET Standard 2.0 | Not Supported |
| 2.x | .NET Standard 2.1 | Not Supported |
| 3.x | .NET Standard 2.1, .NET 5 | Active Development |

_Maintenance_ support level means that new releases will contain bug fixes only.

# Development Process
Philosophy of development process:
1. All libraries in .NEXT family are available for the wide range of .NET implementations: Mono, Xamarin, .NET Core, .NET
1. Compatibility with AOT compiler should be checked for every release
1. Minimize set of dependencies
1. Provide high-quality documentation
1. Stay cross-platform
1. Provide benchmarks
