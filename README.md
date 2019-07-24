.NEXT
====

.NEXT (dotNext) is a set of powerful libaries aimed to improve development productivity and extend .NET API with unique features which potentially will be implemented in the next versions of C# compiler or .NET Runtime. There are quick overview of key features:

* [Core Features](https://sakno.github.io/dotNext/features/core/index.html)
    * [Attachment of user data to arbitrary objects](https://sakno.github.io/dotNext/features/core/userdata.html)
    * [Ability to check whether the struct is default](https://sakno.github.io/dotNext/features/core/valuetype.html) and other useful generic methods for value types such as bitwise equality
    * [Generic Enum API](https://sakno.github.io/dotNext/features/core/enum.html). [Proposed to CoreFX](https://github.com/dotnet/corefx/issues/34077) but not yet implemented
    * [Automatic generation of Equals/GetHashCode](https://sakno.github.io/dotNext/features/core/autoeh.html) for arbitrary type at runtime which is much better that Visual Studio compile-time helper for generating these methods
    * Extended set of [atomic operations](https://sakno.github.io/dotNext/features/core/atomic.html). Inspired by [AtomicInteger](https://docs.oracle.com/javase/10/docs/api/java/util/concurrent/atomic/AtomicInteger.html) and friends from Java
    * [Execution of delegates asynchronously](https://sakno.github.io/dotNext/features/core/asyncd.html) as replacement of `BeginInvoke` and `EndInvoke` pair of methods which are not supported in .NET Core.
    * [Type Classes](https://github.com/dotnet/csharplang/issues/110) inspired by [Concept Types in C#](https://github.com/dotnet/csharplang/issues/110).
* [Metaprogramming](https://sakno.github.io/dotNext/features/metaprogramming/index.html) and advanced Expression Trees
    * [Multi-line lambda expressions](https://sakno.github.io/dotNext/features/metaprogramming/lambda.html)
    * [Dynamic building of async lambdas](https://sakno.github.io/dotNext/features/metaprogramming/async.html). [Proposed to Roslyin](https://github.com/dotnet/csharplang/issues/158) but not yet implemented.
* [Fast Reflection](https://sakno.github.io/dotNext/features/reflection/fast.html)
* [Threading](https://sakno.github.io/dotNext/features/threading/index.html)
    * `ManualResetEvent`, `ReaderWriterLockSlim` and other synchronization primitives now have their [asynchronous versions](https://sakno.github.io/dotNext/features/threading/rwlock.html)
    * Powerful concurrent [ObjectPool](https://sakno.github.io/dotNext/features/threading/objectpool.html)
* ASP.NET Core
    * [Clustered microservices](https://sakno.github.io/dotNext/features/cluster/aspnetcore.html) powered by Raft Consensus Algorithm, state replication and point-to-point messaging

All these features implemented on top of existing .NET Standard stack without modifications of Roslyn compiler or CoreFX libraries.

Learn more:

* [Features](https://sakno.github.io/dotNext/features/core/index.html)
* [API documentation](https://sakno.github.io/dotNext/api/DotNext.html)
* [Benchmarks](https://sakno.github.io/dotNext/benchmarks.html)
* [NuGet Packages](https://www.nuget.org/profiles/rvsakno)

**DISCLAIMER**: API is unstable prior to 1.0 version because the library is in active development. Backward compatibility is not guaranteed.

# What's new
Release Date: 07-XX-2019

Released Components:
* <a href="https://www.nuget.org/packages/dotnext/0.12.0">DotNext-0.12.0</a>
* <a href="https://www.nuget.org/packages/dotnext.reflection/0.12.0">DotNext.Reflection-0.12.0</a>

Changelog for previous versions located [here](./CHANGELOG.md).

# Development Process
Philosophy of development process:
1. All libraries in .NEXT family based on .NET Standard to be available for wide range of .NET implementations: Mono, Xamarin, .NET Core
1. Minimize set of dependencies
1. Rely on .NET standard libraries
1. Provide high-quality documentation
1. Stay cross-platform
1. Provide benchmarks