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
| [Interop between function pointer and delegate](https://github.com/dotnet/csharplang/discussions/3680) | [DelegateHelpers](https://www.fuget.org/packages/DotNext/latest/net5.0/lib/DotNext.dll/DotNext/DelegateHelpers) factory methods |
| [Check if an instance of T is default(T)](https://github.com/dotnet/corefx/issues/16209) | [IsDefault() method](https://www.fuget.org/packages/DotNext/latest/lib/net5.0/DotNext.dll/DotNext.Runtime/Intrinsics) |
| [Concept Types](https://github.com/dotnet/csharplang/issues/110) | [Documentation](https://dotnet.github.io/dotNext/features/concept.html) |
| [Expression Trees covering additional language constructs](https://github.com/dotnet/csharplang/issues/158), i.e. `foreach`, `await`, patterns, multi-line lambda expressions | [Metaprogramming](https://dotnet.github.io/dotNext/features/metaprogramming/index.html) |
| [Async Locks](https://github.com/dotnet/corefx/issues/34073) | [Documentation](https://dotnet.github.io/dotNext/features/threading/index.html) |
| [High-performance general purpose Write-Ahead Log](https://github.com/dotnet/corefx/issues/25034) | [Persistent Log](https://dotnet.github.io/dotNext/features/cluster/wal.html)  |
| [Memory-mapped file as Memory&lt;byte&gt;](https://github.com/dotnet/runtime/issues/37227) | [MemoryMappedFileExtensions](https://dotnet.github.io/dotNext/features/io/mmfile.html) |
| [Memory-mapped file as ReadOnlySequence&lt;byte&gt;](https://github.com/dotnet/runtime/issues/24805) | [ReadOnlySequenceAccessor](https://www.fuget.org/packages/DotNext.IO/latest/lib/net5.0/DotNext.IO.dll/DotNext.IO.MemoryMappedFiles/ReadOnlySequenceAccessor) |

Quick overview of additional features:

* [Attachment of user data to an arbitrary objects](https://dotnet.github.io/dotNext/features/core/userdata.html)
* [Automatic generation of Equals/GetHashCode](https://dotnet.github.io/dotNext/features/core/autoeh.html) for an arbitrary type at runtime which is much better that Visual Studio compile-time helper for generating these methods
* Extended set of [atomic operations](https://dotnet.github.io/dotNext/features/core/atomic.html). Inspired by [AtomicInteger](https://docs.oracle.com/javase/10/docs/api/java/util/concurrent/atomic/AtomicInteger.html) and friends from Java
* [Fast Reflection](https://dotnet.github.io/dotNext/features/reflection/fast.html)
* Fast conversion of bytes to hexadecimal representation and vice versa using `ToHex` and `FromHex` methods from [Span](https://www.fuget.org/packages/DotNext/latest/lib/net5.0/DotNext.dll/DotNext/Span) static class
* `ManualResetEvent`, `ReaderWriterLockSlim` and other synchronization primitives now have their [asynchronous versions](https://dotnet.github.io/dotNext/features/threading/rwlock.html)
* [Atomic](https://dotnet.github.io/dotNext/features/core/atomic.html) memory access operations for arbitrary value types including enums
* [PipeExtensions](https://www.fuget.org/packages/DotNext.IO/latest/lib/net5.0/DotNext.IO.dll/DotNext.IO.Pipelines/PipeExtensions) provides high-level I/O operations for pipelines such as string encoding and decoding
* Various high-performance [growable buffers](https://dotnet.github.io/dotNext/features/io/buffers.html) for efficient I/O
* Fully-featured [Raft implementation](https://github.com/dotnet/dotNext/tree/master/src/cluster)

All these things are implemented in 100% managed code on top of existing .NET API without modifications of Roslyn compiler or CoreFX libraries.

# Quick Links

* [Features](https://dotnet.github.io/dotNext/features/core/index.html)
* [API documentation](https://dotnet.github.io/dotNext/api.html)
* [Benchmarks](https://dotnet.github.io/dotNext/benchmarks.html)
* [NuGet Packages](https://www.nuget.org/profiles/rvsakno)

Documentation for older versions:
* [1.x](https://dotnet.github.io/dotNext/versions/1.x/index.html)
* [2.x](https://dotnet.github.io/dotNext/versions/2.x/index.html)

# What's new
Release Date: 08-XX-2021

<a href="https://www.nuget.org/packages/dotnext/4.0.0">DotNext 4.0.0</a>
* Added `DotNext.Span.Shuffle` and `DotNext.Collections.Generic.List.Shuffle` extension methods that allow to randomize position of elements within span/collection
* Added `DotNext.Collections.Generic.Sequence.Copy` extension method for making copy of the original enumerable collection. The memory for the copy is always rented from the pool
* Added `DotNext.Collections.Generic.Collection.PeekRandom` extension method that allows to select random element from the collection
* Improved performance of `DotNext.Span.TrimLength` and `StringExtensions.TrimLength` extension methods
* Introduced `DotNext.Buffers.BufferHelpers.TrimLength` extension methods for [ReadOnlyMemory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.readonlymemory-1) and [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1) data types
* Improved performance of `DotNext.Buffers.BufferWriter<T>.AddAll` method
* Reduced memory allocations by `ElementAt`, `FirstOrEmpty`, `FirstOrNull`, `ForEach` extension methods in `DotNext.Collections.Generic.Sequence` class
* Added `DotNext.Numerics.BitVector` that allows to convert **bool** vectors into integral types
* Added ability to write interpolated strings to `IBufferWriter<char>` without temporary allocations
* Added ability to write interpolated strings to `BufferWriterSlim<char>`. This makes `BufferWriterSlim<char>` type as allocation-free alternative to [StringBuilder](https://docs.microsoft.com/en-us/dotnet/api/system.text.stringbuilder)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/3.3.0">DotNext.Metaprogramming 3.3.0</a>
* Added `CodeGenerator.Statement` static method to simplify migration from pure Expression Trees
* Fixed LGTM warnings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/3.3.0">DotNext.Reflection 3.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/3.3.0">DotNext.Unsafe 3.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/3.3.0">DotNext.Threading 3.3.0</a>
* Reduced memory allocations caused by async locks

<a href="https://www.nuget.org/packages/dotnext.io/3.4.0">DotNext.IO 3.4.0</a>
* Added `DotNext.IO.SequenceBinaryReader.Position` property that allows to obtain the current position of the reader in the underlying sequence
* Added `DotNext.IO.SequenceBinaryReader.Read(Span<byte>)` method
* Optimized performance of some `ReadXXX` methods of `DotNext.IO.SequenceBinaryReader` type

<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.4.0">DotNext.Net.Cluster 3.4.0</a>
* Optimized memory allocation for each hearbeat message emitted by Raft node in leader state
* Introduced transport-independent implementation of [HyParView](https://asc.di.fct.unl.pt/~jleitao/pdf/dsn07-leitao.pdf) membership protocol
* Fixed compatibility of WAL Interpreter Framework with TCP/UDP transports

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.4.0">DotNext.AspNetCore.Cluster 3.4.0</a>
* Added configurable HTTP protocol version selection policy (.NET 5 or later)
* Added support of leader lease in Raft implementation for optimized read operations
* Added `IRaftCluster.LeadershipToken` that allows to track leadership transfer

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
