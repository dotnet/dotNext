.NEXT
====

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
* [Async execution of delegates](https://sakno.github.io/dotNext/features/core/asyncd.html) as replacement of `BeginInvoke` and `EndInvoke` pair of methods which are not supported in .NET Core.
* [Fast Reflection](https://sakno.github.io/dotNext/features/reflection/fast.html)
* `ManualResetEvent`, `ReaderWriterLockSlim` and other synchronization primitives now have their [asynchronous versions](https://sakno.github.io/dotNext/features/threading/rwlock.html)
* Powerful concurrent [ObjectPool](https://sakno.github.io/dotNext/features/threading/objectpool.html)
* [Atomic](https://sakno.github.io/dotNext/features/core/atomic.html) memory access operations for arbitrary value types
* ASP.NET Core [Clustered microservices](https://sakno.github.io/dotNext/features/cluster/aspnetcore.html) powered by Raft Consensus Algorithm, data replication and point-to-point messaging

All these things are implemented in 100% managed code on top of existing .NET Standard stack without modifications of Roslyn compiler or CoreFX libraries.

# Quick Links

* [Features](https://sakno.github.io/dotNext/features/core/index.html)
* [API documentation](https://sakno.github.io/dotNext/api/DotNext.html)
* [Benchmarks](https://sakno.github.io/dotNext/benchmarks.html)
* [NuGet Packages](https://www.nuget.org/profiles/rvsakno)

# What's new
Release Date: 10-XX-2019

<a href="https://www.nuget.org/packages/dotnext/1.2.0">DotNext 1.2.0</a>
* Fixed memory leaks caused by methods in [StreamExtensions](https://sakno.github.io/dotNext/api/DotNext.IO.StreamExtensions.html) class
* [MemoryRental](https://sakno.github.io/dotNext/api/DotNext.Buffers.MemoryRental-1.html) type is introduced to replace memory allocation with memory rental in some scenarios
* [ArrayRental](https://sakno.github.io/dotNext/api/DotNext.Buffers.ArrayRental-1.html) type is extended
* Value Delegates now are protected from _dangling pointer_ issue caused by dynamic assembly loading 
* Reduced amount of memory utilized by random string generation methods
* Strict package versioning rules are added to avoid accidental upgrade to major version

<a href="https://www.nuget.org/packages/dotnext.reflection/1.2.0">DotNext.Reflection 1.2.0</a>
* Updated version of `DotNext` dependency to fix potential memory leaks
* Strict package versioning rules are added to avoid accidental upgrade to major version

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/1.2.0">DotNext.Metaprogramming 1.2.0</a>
* Updated version of `DotNext` dependency to fix potential memory leaks
* Strict package versioning rules are added to avoid accidental upgrade to major version

<a href="https://www.nuget.org/packages/dotnext.threading/1.2.0">DotNext.Threading 1.2.0</a>
* Updated version of `DotNext` dependency to fix potential memory leaks
* Strict package versioning rules are added to avoid accidental upgrade to major version

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.2.0">DotNext.Unsafe 1.2.0</a>
* [UnmanagedMemoryPool](https://sakno.github.io/dotNext/api/DotNext.Buffers.UnmanagedMemoryPool-1.html) is added
* Strict package versioning rules are added to avoid accidental upgrade to major version

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.2.0">DotNext.Net.Cluster 1.2.0</a>
* Updated version of `DotNext` dependency to fix potential memory leaks
* Strict package versioning rules are added to avoid accidental upgrade to major version

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.2.0">DotNext.AspNetCore.Cluster 1.2.0</a>
* Performance optimizations caused by changes in `ArrayRental` type 
* Strict package versioning rules are added to avoid accidental upgrade to major version

Changelog for previous versions located [here](./CHANGELOG.md).

# Release Policy
* The libraries are versioned according with [Semantic Versioning 2.0](https://semver.org/).
* API can be backward incompatible between 0.x versions
* Version 0.x and 1.x relies on .NET Standard 2.0
* Support of newer versions of .NET Standard is aligned with .NET Core LTS (Long-Term Support) release train. For example, support for .NET Standard 2.1 is scheduled no earlier than in November, 2019. Check [.NET Core Support Policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core) for more information.

# Support Policy
| Version | .NET compatibility | Support Level |
| ---- | ---- | ---- |
| 0.x | .NET Standard 2.0 | Not Supported |
| 1.x | .NET Standard 2.0 | Maintenance |
| 2.x | .NET Standard 2.1 | Active Development |

_Maintenance_ support level means that new releases will contain bug fixes only.

# Development Process
Philosophy of development process:
1. All libraries in .NEXT family based on .NET Standard to be available for wide range of .NET implementations: Mono, Xamarin, .NET Core
1. Compatibility with AOT compiler should be checked for every release
1. Minimize set of dependencies
1. Rely on .NET Standard specification
1. Provide high-quality documentation
1. Stay cross-platform
1. Provide benchmarks