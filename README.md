.NEXT
====
[![Build Status](https://dev.azure.com/rvsakno/dotNext/_apis/build/status/sakno.dotNext?branchName=master)](https://dev.azure.com/rvsakno/dotNext/_build/latest?definitionId=1&branchName=master)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/sakno/dotNext/blob/master/LICENSE)
![Test Coverage](https://img.shields.io/azure-devops/coverage/rvsakno/dotnext/1/master)
[![Join the chat](https://badges.gitter.im/dot_next/community.svg)](https://gitter.im/dot_next/community)

.NEXT (dotNext) is a set of powerful libraries aimed to improve development productivity and extend .NET API with unique features. Some of these features are planned in future releases of .NET platform but already implemented in the library:

| Proposal | Implementation |
| ---- | ---- |
| [Interop between function pointer and delegate](https://github.com/dotnet/csharplang/discussions/3680) | [DelegateHelpers](https://www.fuget.org/packages/DotNext/latest/net5.0/lib/DotNext.dll/DotNext/DelegateHelpers) factory methods |
| [Check if an instance of T is a default(T)](https://github.com/dotnet/corefx/issues/16209) | [IsDefault() method](https://www.fuget.org/packages/DotNext/latest/lib/net5.0/DotNext.dll/DotNext.Runtime/Intrinsics) |
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
Release Date: 01-30-2021

The next major version is out! Its primary focus is .NET 5 support while keeping compatibility with .NET Standard 2.1. As a result, .NEXT libraries built for multiple target frameworks. Additional changes include performance optimizations, polishing of existing API, dropping support of members that were deprecated in 2.x, expanding usage of nullable reference types.

Migration guide for 2.x users is [here](https://sakno.github.io/dotNext/migration/2.html). Please consider that this version is not fully backward compatible with 2.x.

<a href="https://www.nuget.org/packages/dotnext/3.0.0">DotNext 3.0.0</a>
* Improved performance of [SparseBufferWriter&lt;T&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/SparseBufferWriter%601), [BufferWriterSlim&lt;T&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/BufferWriterSlim%601), [PooledArrayBufferWriter&lt;T&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/PooledArrayBufferWriter%601), [PooledBufferWriter&lt;T&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/PooledBufferWriter%601)
* Fixed nullability attributes
* `ArrayRental<T>` type is replaced by [MemoryOwner&lt;T&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/MemoryOwner%601) type
* Removed obsolete members and classes
* Removed `UnreachableCodeExecutionException` exception
* Completely rewritten implementation of extension methods provided by [AsyncDelegate](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Threading/AsyncDelegate) class
* Added [Base64Decoder](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Text/Base64Decoder) type for efficient decoding of base64-encoded bytes in streaming scenarios
* Removed `Future&lt;T&gt;` type
* Added `ThreadPoolWorkItemFactory` static class with extension methods for constructing [IThreadPoolWorkItem](https://docs.microsoft.com/en-us/dotnet/api/system.threading.ithreadpoolworkitem) instances from method pointers. Available only for .NET 5 target
* Introduced factory methods for constructing delegate instances from the pointers to the managed methods
* `DOTNEXT_STACK_ALLOC_THRESHOLD` environment variable can be used to override stack allocation threshold for all .NEXT routines
* Dropped support of value delegates. They are replaced by functional interfaces. However, they are hiddent from the library consumer so every public API that was based on value delegates now has at least two overloads: CLS-compliant version using regular delegate type and unsafe version using function pointer syntax.
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/3.0.0">DotNext.IO 3.0.0</a>
* Changed behavior of `FileBufferingWriter.GetWrittenContentAsStream` and `FileBufferingWriter.GetWrittenContentAsStreamAsync` in a way which allows you to use synchronous/asynchronous I/O for writing and reading separately
* Introduced extension methods for [BufferWriterSlim&lt;char&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/BufferWriterSlim%601) type for encoding of primitive data types
* Fixed nullability attributes
* Added advanced encoding/decoding methods to [IAsyncBinaryWriter](https://www.fuget.org/packages/DotNext.IO/3.0.0/lib/net5.0/DotNext.IO.dll/DotNext.IO/IAsyncBinaryWriter) and [IAsyncBinaryReader](https://www.fuget.org/packages/DotNext.IO/3.0.0/lib/net5.0/DotNext.IO.dll/DotNext.IO/IAsyncBinaryReader) interfaces
* Removed obsolete members and classes
* Simplified signature of `AppendAsync` methods exposed by [IAuditTrail&lt;TEntry&gt;](https://www.fuget.org/packages/DotNext.IO/3.0.0/lib/net5.0/DotNext.IO.dll/DotNext.IO.Log/IAuditTrail%601) interface
* Improved performances of extension methods declared in [PipeExtensions](https://www.fuget.org/packages/DotNext.IO/3.0.0/lib/net5.0/DotNext.IO.dll/DotNext.IO.Pipelines/PipeExtensions) class
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/3.0.0">DotNext.Metaprogramming 3.0.0</a>
* Fixed nullability attributes
* Fixed [issue 23](https://github.com/sakno/dotNext/issues/23)
* Fixed code generation of **finally** blocks inside of asynchronous lambda expressions
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/3.0.0">DotNext.Reflection 3.0.0</a>
* Improved performance of reflective calls
* [DynamicInvoker](https://www.fuget.org/packages/DotNext.Reflection/3.0.0/lib/net5.0/DotNext.Reflection.dll/DotNext.Reflection/DynamicInvoker) delegate allows to pass arguments for dynamic invocation as [Span&lt;object&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) instead of `object[]`
* Fixed nullability attributes

<a href="https://www.nuget.org/packages/dotnext.threading/3.0.0">DotNext.Threading 3.0.0</a>
* Modified ability to await on [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken) and [WaitHandle](https://docs.microsoft.com/en-us/dotnet/api/system.threading.waithandle). [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask) is the primary return type of the appropriate methods
* Fixed nullability attributes
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/3.0.0">DotNext.Unsafe 3.0.0</a>
* Removed obsolete members and classes
* Fixed nullability attributes
* Added `PinnedArray<T>` as a wrapper of pinned arrays from .NET 5
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.0.0">DotNext.Net.Cluster 3.0.0</a>
* Improved performance of [persistent WAL](https://www.fuget.org/packages/DotNext.Net.Cluster/3.0.0/lib/net5.0/DotNext.Net.Cluster.dll/DotNext.Net.Cluster.Consensus.Raft/PersistentState)
* Added support of active-standby configuration of Raft cluster. Standby node cannot become a leader but can be used for reads
* Introduced [framework](https://www.fuget.org/packages/DotNext.Net.Cluster/3.0.0/lib/net5.0/DotNext.Net.Cluster.dll/DotNext.Net.Cluster.Consensus.Raft.Commands/CommandInterpreter) for writing interpreters of log entries stored in persistent write-ahead log
* Added support of JSON-serializable log entries (available for .NET 5 only)
* Fixed bug causing long shutdown of Raft node which is using TCP transport
* Added support of **PreVote** extension for Raft preventing _term inflation_

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.0.0">DotNext.AspNetCore.Cluster 3.0.0</a>
* Added `UsePersistenceEngine` extension method for correct registration of custom persistence engine derived from [PersistentState](https://www.fuget.org/packages/DotNext.Net.Cluster/3.0.0/lib/net5.0/DotNext.Net.Cluster.dll/DotNext.Net.Cluster.Consensus.Raft/PersistentState) class
* Added support of HTTP/3 (available for .NET 5 only)
* Significantly optimized performance and traffic volume of **AppendEntries** Raft RPC call. Now replication performance is comparable to TCP/UDP transports
* Added DNS support. Now cluster member address can be specified using its name instead of IP address

`DotNext.Augmentation` IL weaver add-on for MSBuild is no longer supported.

Changelog for previous versions located [here](./CHANGELOG.md).

# Release & Support Policy
The libraries are versioned according with [Semantic Versioning 2.0](https://semver.org/).

| Version | .NET compatibility | Support Level |
| ---- | ---- | ---- |
| 0.x | .NET Standard 2.0 | Not Supported |
| 1.x | .NET Standard 2.0 | Not Supported |
| 2.x | .NET Standard 2.1 | Maintenance (EOL 02/28/2021) |
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
