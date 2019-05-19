.NEXT
====

.NEXT (dotNext) is the family of powerful libaries aimed to improve development productivity and extend .NET API with unique features which potentially will be implemented in the next versions of C# compiler or .NET Runtime. 

This chapter gives quick overview of these libraries. Read [articles](./features/core/index.md) for closer look at all available features.

> [!IMPORTANT]
> DotNext is in early stage of development. Backward compatibility of API is not guaranteed before 1.0 version.

Prerequisites:
* Runtime: any .NET implementation compatible with .NET Standard 2.0
* OS: Linux, Windows, macOS
* Architecture: any if supported by underlying .NET Runtime

# DotNext
<a href="https://www.nuget.org/packages/dotnext/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.svg?style=flat"></a><br/>
This library is the core of .NEXT which extends .NET Standard API with
  * Extension methods for .NET Standard types including generic collections
  * Enum API to work with arbitrary **enum** types
  * Thread-safe advanced atomic operations to work with **int**, **long**, **bool**, **double**, **float** and reference types
  * Unified representation of various synchronization primitives in the form of the lock
  * Generic specialization with constant values
  * Generation of random strings
  * Low-level methods to work with value types
  * Fast comparison of arrays
  * Ad-hoc user data associated with arbitrary object

# DotNext.Reflection
<a href="https://www.nuget.org/packages/dotnext.reflection/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.reflection.svg?style=flat"></a><br/>
.NET Reflection is slow because relies on late-bound calls when every actual argument should be validated. There is alternative approach: dynamic code generation optimized for every member call. Reflection library from .NEXT family provides provides fully-featured fast reflection using dynamic code generation. Invocation cost is comparable to direct call. Check [Benchmarks](benchmarks.md) to see how it is fast.

Additionally, the library provides support of [Type Classes](https://github.com/dotnet/csharplang/issues/110). You don't need to wait C# language of version _X_ to obtain this feature.

# DotNext.Metaprogramming
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.metaprogramming.svg?style=flat"></a><br/>
This library provides a rich API to write and execute code on-the-fly. It extends [C# Expression Tree](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/) programming model with ordinary things for C# such as `foreach` loop, `for` loop, `while` loop, `using` statement, `lock` statement, string interpolation and even asynchronous lambda expressions with full support of `async`/`await` semantics.

# DotNext.Unsafe
<a href="https://www.nuget.org/packages/dotnext.unsafe/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.unsafe.svg?style=flat"></a><br/>
This library provides a special types to work with unmanaged memory in type-safe manner:
* Structured access to unmanaged memory
* Unstructured access to unmanaged memory
* Typed unmanaged array and list
* CLS-compliant generic pointer type for .NET languages without direct support of such type. Use this feature to work with pointers from VB.NET or F#.
* Volatile pointer operations
* Atomic thread-safe operations applicable to data placed into unmanaged memory: increment, decrement, compare-and-set etc.

# DotNext.Threading
<a href="https://www.nuget.org/packages/dotnext.threading/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.threading.svg?style=flat"></a><br/>
The library allows you to apply the experience of blocking synchronization using [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim), [AsyncCountdownEvent](api/DotNext.Threading.AsyncCountdownEvent.yml) and friends in asynchronous code using asynchronous alternatives of synchronization primitives such as asynchronous locks.

The following code describes these alternative implementations of synchronization primitives for asynchronous code:

| Synchronization Primitive | Asynchronous Version |
| ---- | ---- |
| [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim) | [AsyncReaderWriterLock](api/DotNext.Threading.AsyncReaderWriterLock.yml) |
| [Monitor](https://docs.microsoft.com/en-us/dotnet/api/system.threading.monitor) | [AsyncExclusiveLock](api/DotNext.Threading.AsyncExclusiveLock.yml)
| [ManualResetEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.manualresetevent) | [AsyncManualResetEvent](api/DotNext.Threading.AsyncManualResetEvent.yml)
| [AutoResetEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.autoresetevent) | [AsyncAutoResetEvent](api/DotNext.Threading.AsyncAutoResetEvent.yml)
| [Barrier](https://docs.microsoft.com/en-us/dotnet/api/system.threading.barrier) | [AsyncBarrier](api/DotNext.Threading.AsyncBarrier.yml)

But this is not all features of this library. Read more [here](./features/threading/index.html).


