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
| [Expression Trees covering additional language constructs](https://github.com/dotnet/csharplang/issues/158), e.g. `foreach`, `await`, patterns, multi-line lambda expressions | [Metaprogramming](https://dotnet.github.io/dotNext/features/metaprogramming/index.html) |
| [Async Locks](https://github.com/dotnet/corefx/issues/34073) | [Documentation](https://dotnet.github.io/dotNext/features/threading/index.html) |
| [High-performance general purpose Write-Ahead Log](https://github.com/dotnet/corefx/issues/25034) | [Persistent Log](https://dotnet.github.io/dotNext/features/cluster/wal.html)  |
| [Memory-mapped file as Memory&lt;byte&gt;](https://github.com/dotnet/runtime/issues/37227) | [MemoryMappedFileExtensions](https://dotnet.github.io/dotNext/features/io/mmfile.html) |
| [Memory-mapped file as ReadOnlySequence&lt;byte&gt;](https://github.com/dotnet/runtime/issues/24805) | [ReadOnlySequenceAccessor](https://dotnet.github.io/dotNext/api/DotNext.IO.MemoryMappedFiles.ReadOnlySequenceAccessor.html) |
| [A dictionary where the keys are represented by generic arguments](https://github.com/dotnet/runtime/issues/59718) | [Documentation](https://dotnet.github.io/dotNext/features/core/typem.html) |
| [Process asynchronous tasks as they complete](https://github.com/dotnet/runtime/issues/61959) | [Documentation](https://dotnet.github.io/dotNext/features/threading/taskpipe.html) |
| [Soft References](https://github.com/dotnet/runtime/issues/63113) | [Documentation](https://dotnet.github.io/dotNext/features/core/softref.html) |

Quick overview of additional features:

* [Attachment of user data to an arbitrary objects](https://dotnet.github.io/dotNext/features/core/userdata.html)
* Extended set of [atomic operations](https://dotnet.github.io/dotNext/features/core/atomic.html)
* Fast conversion of bytes to hexadecimal representation and vice versa with [Hex](https://dotnet.github.io/dotNext/api/DotNext.Buffers.Text.Hex.html) class
* `ManualResetEvent`, `ReaderWriterLockSlim` and other synchronization primitives now have their [asynchronous versions](https://dotnet.github.io/dotNext/features/threading/rwlock.html)
* [Atomic](https://dotnet.github.io/dotNext/features/core/atomic.html) memory access operations for arbitrary value types
* [PipeExtensions](https://dotnet.github.io/dotNext/api/DotNext.IO.Pipelines.PipeExtensions.html) provides high-level I/O operations for pipelines such as string encoding and decoding
* A rich set of high-performance [memory buffers](https://dotnet.github.io/dotNext/features/io/buffers.html) for efficient I/O
* String formatting, encoding and decoding with low GC pressure: [dynamic char buffers](https://dotnet.github.io/dotNext/features/io/buffers.html#char-buffer)
* Fully-featured [Raft implementation](https://github.com/dotnet/dotNext/tree/master/src/cluster#raft)
* Fully-featured [HyParView implementation](https://github.com/dotnet/dotNext/tree/master/src/cluster#hyparview)

All these things are implemented in 100% managed code on top of existing .NET API.

# Quick Links

* [Features](https://dotnet.github.io/dotNext/features/core/index.html)
* [API documentation](https://dotnet.github.io/dotNext/api/DotNext.html)
* [Benchmarks](https://dotnet.github.io/dotNext/benchmarks.html)
* [NuGet Packages](https://www.nuget.org/profiles/rvsakno)

# What's new
Release Date: 11-04-2025

<a href="https://www.nuget.org/packages/dotnext/5.26.1">DotNext 5.26.1</a>
* Lock upgrade logic provided by `ReaderWriterSpinLock` is adjusted according to [275](https://github.com/dotnet/dotNext/issues/275)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.26.1">DotNext.Metaprogramming 5.26.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.26.1">DotNext.Unsafe 5.26.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.26.1">DotNext.Threading 5.26.1</a>
* Lock upgrade logic provided by `AsyncReaderWriterLock` is adjusted according to [275](https://github.com/dotnet/dotNext/issues/275)
* Improved accuracy of `CancellationTokenMultiplexer.Scope.IsTimedOut` property

<a href="https://www.nuget.org/packages/dotnext.io/5.26.1">DotNext.IO 5.26.1</a>
* Added auxiliary `MemorySegmentStream` wrapper over [Memory&lt;byte&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.memory-1) type in the form of the writable stream
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.26.1">DotNext.Net.Cluster 5.26.1</a>
* Fixed [276](https://github.com/dotnet/dotNext/issues/276)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.26.1">DotNext.AspNetCore.Cluster 5.26.1</a>
* Fixed [276](https://github.com/dotnet/dotNext/issues/276)

Changelog for previous versions located [here](./CHANGELOG.md).

# Release & Support Policy
The libraries are versioned according to [Semantic Versioning 2.0](https://semver.org/).

| Version | .NET compatibility | Support Level |
| ---- | ---- | ---- |
| 0.x | .NET Standard 2.0 | :x: |
| 1.x | .NET Standard 2.0 | :x: |
| 2.x | .NET Standard 2.1 | :x: |
| 3.x | .NET Standard 2.1, .NET 5 | :x: |
| 4.x | .NET 6 | :x: |
| 5.x | .NET 8 | :heavy_check_mark: |

:x: - unsupported, :white_check_mark: - bug and security fixes only, :heavy_check_mark: - active development

# Development Process
Philosophy of development process:
1. All libraries in .NEXT family are available for various .NET form factors: Mono, WASM, NativeAOT
1. Minimal set of dependencies
1. Provide high-quality documentation
1. Stay cross-platform
1. Provide benchmarks

# Users
.NEXT is used by several companies in their projects:

[![Copenhagen Atomics](https://upload.wikimedia.org/wikipedia/commons/thumb/6/66/Copenhagenatomics_logo_gray.png/320px-Copenhagenatomics_logo_gray.png)](https://www.copenhagenatomics.com)

[![Wargaming](https://upload.wikimedia.org/wikipedia/en/f/fa/Wargaming_logo.svg)](https://wargaming.com)

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