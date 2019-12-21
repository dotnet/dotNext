.NEXT
====
[![Build Status](https://dev.azure.com/rvsakno/dotNext/_apis/build/status/sakno.dotNext?branchName=master)](https://dev.azure.com/rvsakno/dotNext/_build/latest?definitionId=1&branchName=master)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/sakno/dotNext/blob/master/LICENSE)
![Test Coverage](https://img.shields.io/azure-devops/coverage/rvsakno/dotnext/1/master)

.NEXT (dotNext) is a set of powerful libaries aimed to improve development productivity and extend .NET API with unique features. Some of these features are planned in future releases of .NET platform but already implemented in the library:

| Proposal | Implementation |
| ---- | ---- |
| [Static Delegates](https://github.com/dotnet/csharplang/blob/master/proposals/static-delegates.md) | [Value Delegates](https://sakno.github.io/dotNext/features/core/valued.html) |
| [Operators for IntPtr and UIntPtr](https://github.com/dotnet/corefx/issues/32775) | [Extension methods](https://sakno.github.io/dotNext/api/DotNext.ValueTypeExtensions.html) for arithmetic, bitwise and comparison operations |
| [Enum API](https://github.com/dotnet/corefx/issues/34077) | [Documentation](https://sakno.github.io/dotNext/features/core/enum.html) |
| [Check if an instance of T is a default(T)](https://github.com/dotnet/corefx/issues/16209) | [IsDefault() method](https://sakno.github.io/dotNext/api/DotNext.Runtime.Intrinsics.html#DotNext_Runtime_Intrinsics_IsDefault__1___0_) |
| [Concept Types](https://github.com/dotnet/csharplang/issues/110) | [Documentation](https://sakno.github.io/dotNext/features/concept.html) |
| [Expression Trees covering additional language constructs](https://github.com/dotnet/csharplang/issues/158), i.e. `foreach`, `await`, patterns, multi-line lambda expressions | [Metaprogramming](https://sakno.github.io/dotNext/features/metaprogramming/index.html) |
| [Async Locks](https://github.com/dotnet/corefx/issues/34073) | [Documentation](https://sakno.github.io/dotNext/features/threading/index.html) |
| [High-performance general purpose Write Ahead Log](https://github.com/dotnet/corefx/issues/25034) | [Persistent Log](https://sakno.github.io/dotNext/features/cluster/wal.html)  | 

Quick overview of additional features:

* [Attachment of user data to arbitrary objects](https://sakno.github.io/dotNext/features/core/userdata.html)
* [Automatic generation of Equals/GetHashCode](https://sakno.github.io/dotNext/features/core/autoeh.html) for arbitrary type at runtime which is much better that Visual Studio compile-time helper for generating these methods
* Extended set of [atomic operations](https://sakno.github.io/dotNext/features/core/atomic.html). Inspired by [AtomicInteger](https://docs.oracle.com/javase/10/docs/api/java/util/concurrent/atomic/AtomicInteger.html) and friends from Java
* [Async execution of delegates](https://sakno.github.io/dotNext/features/core/asyncd.html) as replacement of `BeginInvoke` and `EndInvoke` pair of methods which are not supported on .NET Core.
* [Fast Reflection](https://sakno.github.io/dotNext/features/reflection/fast.html)
* `ManualResetEvent`, `ReaderWriterLockSlim` and other synchronization primitives now have their [asynchronous versions](https://sakno.github.io/dotNext/features/threading/rwlock.html)
* Powerful concurrent [ObjectPool](https://sakno.github.io/dotNext/features/threading/objectpool.html)
* [Atomic](https://sakno.github.io/dotNext/features/core/atomic.html) memory access operations for arbitrary value types
* [PipeExtensions](https://sakno.github.io/dotNext/api/DotNext.IO.Pipelines.PipeExtensions) provides high-level I/O operations for pipelines such as string encoding and decoding
* ASP.NET Core [Clustered microservices](https://sakno.github.io/dotNext/features/cluster/aspnetcore.html) powered by Raft Consensus Algorithm, data replication and point-to-point messaging

All these things are implemented in 100% managed code on top of existing .NET Standard stack without modifications of Roslyn compiler or CoreFX libraries.

# Quick Links

* [Features](https://sakno.github.io/dotNext/features/core/index.html)
* [API documentation](https://sakno.github.io/dotNext/api/DotNext.html)
* [Benchmarks](https://sakno.github.io/dotNext/benchmarks.html)
* [NuGet Packages](https://www.nuget.org/profiles/rvsakno)

Documentation for older versions:
* [1.x](https://sakno.github.io/dotNext/versions/1.x/index.html)

# What's new
Release Date: 12-XX-2019

Major release of version 2.0 is completely finished and contains polished existing and new API. All libraries in .NEXT family are upgraded. Migration guide for 1.x users is [here](https://sakno.github.io/dotNext/migration/1.html). **Automatic upgrade via NuGet package manager is not supported**. The version should be fixed in `csproj` manually. This is done consciously because the new version is not fully backward compatible with 1.x.

Major version is here for the following reasons:
1. .NET Core 3.1 LTS is finally released
1. .NET Standard 2.1 contains a lot of new API required for optimizations. The most expected API is asynchronous methods in [Stream](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) class. These enhancements are necessary for evolution of .NEXT library. For instance, new [DotNext.IO](https://www.nuget.org/packages/DotNext.IO/) library could not be released without the new .NET API.
1. ASP.NET Core 2.2 is no longer supported by Microsoft. Therefore, [DotNext.AspNetCore.Cluster](https://www.nuget.org/packages/DotNext.AspNetCore.Cluster/) library of version 1.x relies on unmaintainable platform. Now it is based on ASP.NET Core 3.1 which has long-term support.

What is done in this release:
1. Removed trivial "one-liners" in **DotNext** library
1. Reduced and unified API to work with unmanaged memory in **DotNext.Unsafe** library
1. **DotNext.AspNetCore.Cluster** migrated to ASP.NET Core 3.1 LTS
1. Introduced [DotNext.IO](https://www.nuget.org/packages/DotNext.IO/) library with unified asynchronous API surface for .NET streams and I/O [pipelines](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines). This API provides high-level methods for encoding and decoding of data such as strings and blittable types. In other words, if you want to have [BinaryReader](https://docs.microsoft.com/en-us/dotnet/api/system.io.binaryreader) or [BinaryWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.binarywriter) for pipelines then welcome!
1. Ability to obtain result of [task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1) asynchronously when its result type is not known at compile-time
1. Additional optimizations of performance in [Write-Ahead Log](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.html)
1. Increased test coverage and fixed bugs
1. Fixed issue [#4](https://github.com/sakno/dotNext/issues/4)
1. Migration to C# 8 and nullable reference types

Version 1.x is still supported and maintained because I want to keep .NEXT available on .NET Framework 4.8. However, backports of new features from 2.x to 1.x are very unlikely.

Changelog for previous versions located [here](./CHANGELOG.md).

# Release Policy
* The libraries are versioned according with [Semantic Versioning 2.0](https://semver.org/).
* Version 0.x and 1.x relies on .NET Standard 2.0
* Version 2.x relies on .NET Standard 2.1

# Support Policy
| Version | .NET compatibility | Support Level |
| ---- | ---- | ---- |
| 0.x | .NET Standard 2.0 | Not Supported |
| 1.x | .NET Standard 2.0 | Maintenance |
| 2.x | .NET Standard 2.1 | Active Development |

_Maintenance_ support level means that new releases will contain bug fixes only.

[DotNext.AspNetCore.Cluster](https://www.nuget.org/packages/DotNext.AspNetCore.Cluster/) of version 1.x is no longer supported because of ASP.NET Core 2.2 end-of-life. Please consider upgrade.

# Development Process
Philosophy of development process:
1. All libraries in .NEXT family based on .NET Standard to be available for wide range of .NET implementations: Mono, Xamarin, .NET Core
1. Compatibility with AOT compiler should be checked for every release
1. Minimize set of dependencies
1. Rely on .NET Standard specification
1. Provide high-quality documentation
1. Stay cross-platform
1. Provide benchmarks
