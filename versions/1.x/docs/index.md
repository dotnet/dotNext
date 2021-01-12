.NEXT
====

.NEXT (dotNext) is the family of powerful libaries aimed to improve development productivity and extend .NET API with unique features which potentially will be implemented in the next versions of C# compiler or .NET Runtime. 

This chapter gives quick overview of these libraries. Read [articles](./features/core/index.md) for closer look at all available features.

Prerequisites:
* Runtime: any .NET implementation compatible with .NET Standard 2.0
* OS: Linux, Windows, MacOS
* Architecture: any if supported by underlying .NET Runtime

# DotNext
<a href="https://www.nuget.org/packages/dotnext/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.svg?logo=NuGet"></a><br/>
This library is the core of .NEXT which extends .NET Standard API with
  * Value Delegates as allocation-free and lightweight alternative to regular .NET delegates
  * Enum API to work with arbitrary **enum** types
  * Thread-safe advanced atomic operations to work with **int**, **long**, **bool**, **double**, **float**, **IntPtr**, arbitrary reference and value types
  * Unified representation of various synchronization primitives in the form of the lock
  * Generic specialization with constant values
  * Generation of random strings
  * Low-level methods to work with value types
  * Fast comparison of arrays
  * Ad-hoc user data associated with arbitrary object

# DotNext.Reflection
<a href="https://www.nuget.org/packages/dotnext.reflection/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.reflection.svg?logo=NuGet"></a><br/>
.NET Reflection is slow because relies on late-bound calls when every actual argument should be validated. There is alternative approach: dynamic code generation optimized for every member call. Reflection library from .NEXT family provides provides fully-featured fast reflection using dynamic code generation. Invocation cost is comparable to direct call. Check [Benchmarks](benchmarks.md) to see how it is fast.

Additionally, the library provides support of [Type Classes](https://github.com/dotnet/csharplang/issues/110). You don't need to wait C# language of version _X_ to obtain this feature.

# DotNext.Metaprogramming
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.metaprogramming.svg?logo=NuGet"></a><br/>
This library provides a rich API to write and execute code on-the-fly. It extends [C# Expression Tree](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/) programming model with ordinary things for C# such as `foreach` loop, `for` loop, `while` loop, `using` statement, `lock` statement, string interpolation and even asynchronous lambda expressions with full support of `async`/`await` semantics.

# DotNext.Unsafe
<a href="https://www.nuget.org/packages/dotnext.unsafe/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.unsafe.svg?logo=NuGet"></a><br/>
This library provides a special types to work with unmanaged memory in type-safe manner:
* Structured access to unmanaged memory
* Unstructured access to unmanaged memory
* Interop with unmanaged memory via [Memory](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1) value type
* CLS-compliant generic pointer type for .NET languages without direct support of such type. Use this feature to work with pointers from VB.NET or F#.
* Atomic thread-safe operations applicable to data placed into unmanaged memory: increment, decrement, compare-and-set etc, volatile access
* Calling unmanaged functions by pointer

# DotNext.Threading
<a href="https://www.nuget.org/packages/dotnext.threading/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.threading.svg?logo=NuGet"></a><br/>
The library allows you to reuse experience of blocking synchronization with help of [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim), [AsyncCountdownEvent](api/DotNext.Threading.AsyncCountdownEvent.yml) and friends in asynchronous code using their alternatives such as asynchronous locks.

The following code describes these alternative implementations of synchronization primitives for asynchronous code:

| Synchronization Primitive | Asynchronous Version |
| ---- | ---- |
| [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim) | [AsyncReaderWriterLock](api/DotNext.Threading.AsyncReaderWriterLock.yml) |
| [Monitor](https://docs.microsoft.com/en-us/dotnet/api/system.threading.monitor) | [AsyncExclusiveLock](api/DotNext.Threading.AsyncExclusiveLock.yml)
| [ManualResetEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.manualresetevent) | [AsyncManualResetEvent](api/DotNext.Threading.AsyncManualResetEvent.yml)
| [AutoResetEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.autoresetevent) | [AsyncAutoResetEvent](api/DotNext.Threading.AsyncAutoResetEvent.yml)
| [Barrier](https://docs.microsoft.com/en-us/dotnet/api/system.threading.barrier) | [AsyncBarrier](api/DotNext.Threading.AsyncBarrier.yml)
| [CountdownEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.countdownevent) | [AsyncCountdownEvent](https://sakno.github.io/dotNext/api/DotNext.Threading.AsyncCountdownEvent)

But this is not all features of this library. Read more [here](./features/threading/index.md).

# DotNext.Net.Cluster
<a href="https://www.nuget.org/packages/dotnext.net.cluster/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.net.cluster.svg?logo=NuGet"></a><br/>
Provides rich framework for building [clustered microservices](https://en.wikipedia.org/wiki/Computer_cluster) based on network consensus and distributed messaging. It includes transport-agnostic implementation of [Raft Consensus Algoritm](https://raft.github.io/) that can be adopted for any communication protocol and high-performance persistent Write Ahead Log suitable for general-purpose usage.

# DotNext.AspNetCore.Cluster
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.aspnetcore.cluster.svg?logo=NuGet"></a><br/>
Allows to build clustered microservices which rely on network consensus and distributed messaging with [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/) framework. This library contains HTTP-based implementation of [Raft](https://raft.github.io/) Consensus Algorithm, HTTP-based distributed messaging across cluster nodes, cluster leader detection, automatic redirection to leader node and many other things.

# DotNext.Augmentation
<a href="https://www.nuget.org/packages/dotnext.augmentation.fody/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.augmentation.fody.svg?logo=NuGet"></a><br/>
.NEXT Augmentations extends compilation pipeline with tricks and optimizations not available in Roslyin Compiler out-of-the-box. It is actually not a library, but IL code weaver implemented as [Fody](https://github.com/Fody/Fody) add-in. Read more about compile-time features [here](./features/aug.md).
