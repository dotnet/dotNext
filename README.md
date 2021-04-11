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
Release Date: 04-XX-2021

This release is primarily focused on improvements of stuff related to cluster programming and Raft: persistent WAL, transferring over the wire, buffering and reducing I/O overhead. Many ideas for this release were proposed by [potrusil-osi](https://github.com/potrusil-osi) in the issue [57](https://github.com/sakno/dotNext/issues/57).

<a href="https://www.nuget.org/packages/dotnext/3.1.0">DotNext 3.1.0</a>
* Added async support to `IGrowableBuffer<T>` interface
* Added indexer to `MemoryOwner<T>` supporting **nint** data type

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/3.1.0">DotNext.Metaprogramming 3.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/3.1.0">DotNext.Reflection 3.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/3.1.0">DotNext.Unsafe 3.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/3.1.0">DotNext.Threading 3.1.0</a>
* `AsyncTigger` now supports fairness policy when resuming suspended callers

<a href="https://www.nuget.org/packages/dotnext.io/3.1.0">DotNext.IO 3.1.0</a>
* Added `SkipAsync` method to `IAsyncBinaryReader` interface
* Added more performance optimization options to `FileBufferingWriter` class
* Fixed bug in `StreamSegment.Position` property setter causes invalid position in the underlying stream

<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.1.0">DotNext.Net.Cluster 3.1.0</a>
* Added support of three log compaction modes to `PersistentState` class:
   * _Sequential_ which is the default compaction mode in 3.0.x and earlier versions. Provides best optimization of disk space by the cost of the performance of adding new log entries
   * _Background_ which allows to run log compaction in parallel with write operations
   * _Foreground_ which runs log compaction in parallel with commit operation
* Small performance improvements when passing log entries over the wire for TCP and UDP protocols
* Added buffering API for log entries
* Added optional buffering of log entries and snapshot when transferring using TCP or UDP protocols
* Introduced _copy-on-read_ behavior to `PersistentState` class to reduce lock contention between writers and the replication process
* Introduced in-memory cache of log entries to `PersistentState` class to eliminate I/O overhead when appending and applying new log entries
* Interpreter Framework: removed overhead caused by deserialization of command identifier from the log entry. Now the identifier is a part of log entry metadata which is usually pre-cached by underlying WAL implementation

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.1.0">DotNext.AspNetCore.Cluster 3.1.0</a>
* Added ability to override cluster members discovery service. See `IMembersDiscoveryService` interface
* Small performance improvements when passing log entries over the wire for HTTP/1, HTTP/2 and HTTP/3 protocols
* Added optional buffering of log entries and snapshot when transferring over the wire. Buffering allows to reduce lock contention of persistent WAL
* Introduced incremental compaction of committed log entries which is running by special background worker 

**Breaking Changes**: Binary format of persistent WAL has changed. `PersistentState` class from 3.1.0 release is unable to parse the log that was created by earlier versions.

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
