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
Release Date: 03-29-2026

<a href="https://www.nuget.org/packages/dotnext/6.1.0">DotNext 6.1.0</a>
* Fixed cancellation support in `BufferWriterSlim<T>`
* `BoxedValue<T>` cannot be derived anymore (which was a bug)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.1.0">DotNext.Metaprogramming 6.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.1.0">DotNext.Unsafe 6.1.0</a>
* Added cleanup callback for `OpaqueValue<T>` that can be passed to the unmanaged code

<a href="https://www.nuget.org/packages/dotnext.threading/6.1.0">DotNext.Threading 6.1.0</a>
* Added alternate lookup to `RandomAccessCache<TKey, TValue>` class
* Fixed false positive when the object cannot be returned to the object pool even if there is a free space in it

<a href="https://www.nuget.org/packages/dotnext.io/6.1.0">DotNext.IO 6.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.1.0">DotNext.Net.Cluster 6.1.0</a>
* Fixed memory leak [280](https://github.com/dotnet/dotNext/pull/280)
* Refresh election timer when vote is granted [281](https://github.com/dotnet/dotNext/pull/281)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.1.0">DotNext.AspNetCore.Cluster 6.1.0</a>
* Updated dependencies

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