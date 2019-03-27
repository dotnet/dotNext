.NEXT
====

.NEXT (Dot Next) is the family of powerful libaries aimed to improve development productivity and extend .NET API with unique features which potentially will be implemented in the next versions of C# compiler or .NET Runtime. 

This chapter gives quick overview of these libraries. Read [articles](./features/core/index.md) for closer look at all available features.

> [!IMPORTANT]
> DotNext is in early stage of development. Backward compatibility of API is not guaranteed before 1.0 version.

# DotNext
<a href="https://www.nuget.org/packages/dotnext"><img src="https://img.shields.io/nuget/v/dotnext.svg?style=flat"></a><br/>
This library is the core of .NEXT which extends .NET Standard API with
  * Extension methods for .NET Standard types including generic collections
  * Thread-safe atomic operations to work with `int`, `long`, `bool` and reference types
  * Unified representation of synchronization lock
  * Generic specialization with constant values
  * Generation of random strings
  * Low-level methods to work with value types
  * Ad-hoc user data associated with any object

# DotNext.Reflection
<a href="https://www.nuget.org/packages/dotnext.reflection"><img src="https://img.shields.io/nuget/v/dotnext.reflection.svg?style=flat"></a><br/>
This library provides support of strongly-typed and fast reflection as well as [Type Classes](https://github.com/dotnet/csharplang/issues/110). You don't need to wait C# language of version _X_ to obtain this feature.

# DotNext.Metaprogramming
<a href="https://www.nuget.org/packages/dotnext.metaprogramming"><img src="https://img.shields.io/nuget/v/dotnext.metaprogramming.svg?style=flat"></a><br/>
This library provides a rich set of tools to write and execute code on-the-fly. It extends [C# Expression Tree](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/) programming model with ordinary things for C# such as `foreach` loop, `for` loop, `while` loop, `using` statement and even asynchronous lambda expressions with full support of `async`/`await` semantics.

# DotNext.Unsafe
<a href="https://www.nuget.org/packages/dotnext.unsafe"><img src="https://img.shields.io/nuget/v/dotnext.unsafe.svg?style=flat"></a><br/>
This library provides a special types to work with unmanaged memory in type-safe manner:
* Value types allocated in the unmanaged memory (heap)
* Typed unmanaged arrays
* CLS-compliant generic pointer type for .NET languages without direct support of such type. Use this feature to work with pointers from VB.NET or F#.



