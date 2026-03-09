.NEXT
====
[![Build Status](https://dev.azure.com/dotnet/dotNext/_apis/build/status/dotnet.dotNext?branchName=master)](https://dev.azure.com/dotnet/dotNext/_build/latest?definitionId=1&branchName=master)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/dotnet/dotNext/blob/master/LICENSE)
![Test Coverage](https://img.shields.io/azure-devops/coverage/dotnet/dotnext/160/master)
[![CodeQL](https://github.com/dotnet/dotNext/workflows/CodeQL/badge.svg)](https://github.com/dotnet/dotNext/actions?query=workflow%3ACodeQL)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/dotnet/dotNext)

.NEXT (dotNext) is a set of powerful libraries designed for high-performance scenarios when your application expects near zero memory allocation and high flexibility. It is aimed to high-load microservices, database engines, actors, and various types of distributed applications. The feature list includes a rich set of efficient tools with low overhead:
* [Buffer manipulations](https://dotnet.github.io/dotNext/features/io/buffers.html)
* [String building](https://dotnet.github.io/dotNext/features/core/stringb.html)
* Monadic types
* Advanced [GC notifications](https://dotnet.github.io/dotNext/features/core/gcnotif.html)
* Low-level API to work with memory: [alignment detection](https://dotnet.github.io/dotNext/features/core/intrinsics.html), type-safe unmanaged allocators, [Span&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.span-1) manipulations
* Base64 streaming [encoder/decoder](https://dotnet.github.io/dotNext/features/core/base64.html)
* Async-friendly [SIEVE cache](https://dotnet.github.io/dotNext/features/cache/index.html)
* [Async locks](https://dotnet.github.io/dotNext/features/threading/index.html)
* Extended LINQ Expression Trees and dynamic code generation tools with async lambdas for [metaprogramming](https://dotnet.github.io/dotNext/features/metaprogramming/index.html)
* [Raft Consensus Algorithm](https://dotnet.github.io/dotNext/features/cluster/raft.html) with extensions such as the failure detection
* Fast general-purpose [Write Ahead Log](https://dotnet.github.io/dotNext/features/cluster/wal.html)
* [TCP multiplexing](https://dotnet.github.io/dotNext/features/cluster/multiplex.html) protocol
* [HyParView protocol](https://dotnet.github.io/dotNext/features/cluster/gossip.html) implementation

All these things are implemented in 100% managed code on top of existing .NET API.

# Quick Links

* [Features](https://dotnet.github.io/dotNext/features/core/index.html)
* [API documentation](https://dotnet.github.io/dotNext/api/DotNext.html)
* [Benchmarks](https://dotnet.github.io/dotNext/benchmarks.html)
* [NuGet Packages](https://www.nuget.org/profiles/rvsakno)

# What's new
Release Date: 03-09-2026

.NEXT 6.0.0 has been released! Migration guide is [here](https://dotnet.github.io/dotNext/migration/4.html). All changes are mostly driven by [Extension Members](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14#extension-members) feature in C# 14. Most of the things in .NEXT now expressed naturally as extensions for existing .NET classes.

<a href="https://www.nuget.org/packages/dotnext/6.0.0">DotNext 6.0.0</a>
* Introduced convenient operators to work with spans: `>>>` for safe copying and `%` for trimming
* `BufferWriterSlim<T>` now implements [IBufferWriter&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) interface which allows to use it in generic scenarios
* Greatly improved performance of bit vector conversion methods exposed by `Number` class
* Removed memory allocation caused by `Bind` extension methods
* Added convenient static extension methods to [Stream](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream) class
* Improved AOT compatibility
* Introduced `Variant` type that represents the functionality similar to [TypedReference](https://learn.microsoft.com/en-us/dotnet/api/system.typedreference) but without related compiler restrictions
* Added **allows ref struct** anti-constraint to many parts of the library
* Improved performance of `ConcurrentTypeMap` class
* Built-in delegate types from .NET are extended with static extension methods
* Added static extensions to [ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception) class to cover more validation scenarios
* Introduced `Enum<T>` to enable the power of the Generic Math when working with enums
* All zero-alloc and encoding interpolation string handlers now in one place: `DotNext.Text` namespace
* Added `+=` operator overloading to `SpanWriter<T>` and `BufferWriterSlim<T>` types
* Added cancellation token support to [Lock](https://learn.microsoft.com/en-us/dotnet/api/system.threading.lock) and [Thread](https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread) classes

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.0.0">DotNext.Metaprogramming 6.0.0</a>
* Expression nodes can be combined with built-in operators: `+`, `-`, `/`, etc.

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.0.0">DotNext.Unsafe 6.0.0</a>
* `OpaqueValue<T>` type is introduced to unify the way of passing value types and reference types to unmanaged code. It's especially useful for passing .NET data types to the unmanaged callbacks implemented in C#
* Added static extension properties to `MemoryAllocator<T>` delegate to expose unmanaged memory allocators

<a href="https://www.nuget.org/packages/dotnext.threading/6.0.0">DotNext.Threading 6.0.0</a>
* `CancellationTokenMultiplexer` has improved CTS pooling mechanism
* Introduced fast bounded object pool
* Significantly reduced contention overhead in async locks

<a href="https://www.nuget.org/packages/dotnext.io/5.26.0">DotNext.IO 5.26.0</a>
* Added static extension methods to [Stream](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream) class
* Improved integration between `IJsonSerializable<T>` interface and JSON serialization infrastructure in .NET

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.0.0">DotNext.Net.Cluster 6.0.0</a>
* Old WAL implementation represented by `MemoryBasedStateMachine` and `DiskBasedStateMachine` classes is completely removed in favor of `WriteAheadLog` class
* Improved performance of the log entry serialization
* Improved AOT compatibility

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.0.0">DotNext.AspNetCore.Cluster 6.0.0</a>
* Improved AOT compatibility

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.0.0">DotNext.MaintenanceServices 1.0.0</a>
* Upgrade to `System.CommandLine` stable release
* Stabilized public API surface

# Release & Support Policy
The libraries are versioned according to [Semantic Versioning 2.0](https://semver.org/).

| Version | .NET compatibility | Support Level |
| ---- | ---- | ---- |
| 0.x | .NET Standard 2.0 | :x: |
| 1.x | .NET Standard 2.0 | :x: |
| 2.x | .NET Standard 2.1 | :x: |
| 3.x | .NET Standard 2.1, .NET 5 | :x: |
| 4.x | .NET 6 | :x: |
| 5.x | .NET 8 | :white_check_mark: |
| 6.x | .NET 10 | :heavy_check_mark: |

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