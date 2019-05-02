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
  * Unified representation of synchronization lock
  * Generic specialization with constant values
  * Generation of random strings
  * Low-level methods to work with value types
  * Fast comparison of arrays
  * Ad-hoc user data associated with arbitrary object

# DotNext.Reflection
<a href="https://www.nuget.org/packages/dotnext.reflection/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.reflection.svg?style=flat"></a><br/>
This library provides support of strongly-typed and fast reflection as well as [Type Classes](https://github.com/dotnet/csharplang/issues/110). You don't need to wait C# language of version _X_ to obtain this feature.

# DotNext.Metaprogramming
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.metaprogramming.svg?style=flat"></a><br/>
This library provides a rich set of tools to write and execute code on-the-fly. It extends [C# Expression Tree](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/) programming model with ordinary things for C# such as `foreach` loop, `for` loop, `while` loop, `using` statement, `lock` statement and even asynchronous lambda expressions with full support of `async`/`await` semantics.

# DotNext.Unsafe
<a href="https://www.nuget.org/packages/dotnext.unsafe/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.unsafe.svg?style=flat"></a><br/>
This library provides a special types to work with unmanaged memory in type-safe manner:
* Structured access to unmanaged memory
* Unstructured access to unmanaged memory
* Typed unmanaged array and list
* CLS-compliant generic pointer type for .NET languages without direct support of such type. Use this feature to work with pointers from VB.NET or F#.
* Volatile read and write from/to unmanaged memory
* Atomic thread-safe operations applicable to data placed into unmanaged memory: increment, decrement, compare-and-set etc.

# DotNext.Threading
<a href="https://www.nuget.org/packages/dotnext.threading/absoluteLatest"><img src="https://img.shields.io/nuget/v/dotnext.threading.svg?style=flat"></a><br/>
A set of advanced classes for multithreaded and asynchronous programming as well as non-blocking asynchronous alternatives of [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim), [Monitor](https://docs.microsoft.com/en-us/dotnet/api/system.threading.monitor), [ManualResetEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.manualresetevent), [AutoResetEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.autoresetevent) and [CountdownEvent](https://docs.microsoft.com/en-us/dotnet/api/system.threading.countdownevent) in the form of [AsyncReaderWriterLock](api/DotNext.Threading.AsyncReaderWriterLock.yml), [AsyncExclusiveLock](api/DotNext.Threading.AsyncExclusiveLock.yml), [AsyncManualResetEvent](api/DotNext.Threading.AsyncManualResetEvent.yml), [AsyncAutoResetEvent](api/DotNext.Threading.AsyncAutoResetEvent.yml) and [AsyncCountdownEvent](api/DotNext.Threading.AsyncCountdownEvent.yml) respectively.


