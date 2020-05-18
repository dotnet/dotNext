Release Notes
====

# 05-18-2020
<a href="https://www.nuget.org/packages/dotnext/2.4.1">DotNext 2.4.1</a>
* `ArrayRental<T>` can automatically determine array cleanup policy
* `MemoryRental<T>` is improved for stackalloc/pooling pattern
* Fixed bug in `Clear` method of `PooledBufferWriter` class

# 05-17-2020
This release is mostly aimed to improving code quality of all .NEXT libraries with help of _StyleCop_ analyzer.

<a href="https://www.nuget.org/packages/dotnext/2.4.0">DotNext 2.4.0</a>
* `DotNext.IO.StreamSource` class allows to convert `ReadOnlyMemory<byte>` or `ReadOnlySequence<byte>` to stream
* `DotNext.IO.StreamSource` class allows to obtain writable stream for `IBufferWriter<byte>`

<a href="https://www.nuget.org/packages/dotnext.io/2.4.0">DotNext.IO 2.4.0</a>
* Support of `BeginRead` and `EndRead` methods in `StreamSegment` class
* Update to the latest `System.IO.Pipelines` library

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.4.0">DotNext.Metaprogramming 2.4.0</a>
* Fixed several compiler warnings

<a href="https://www.nuget.org/packages/dotnext.reflection/2.4.0">DotNext.Reflection 2.4.0</a>
* Fixed several compiler warnings

<a href="https://www.nuget.org/packages/dotnext.threading/2.4.0">DotNext.Threading 2.4.0</a>
* Fixed several compiler warnings
* Update to the latest `System.Threading.Channels` library

<a href="https://www.nuget.org/packages/dotnext.unsafe/2.4.0">DotNext.Unsafe 2.4.0</a>
* Ability to convert `Pointer<T>` to `IMemoryOwner<T>`

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.4.0">DotNext.Net.Cluster 2.4.0</a>
* Added calls to `ConfigureAwait` in multiple places

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.4.0">DotNext.AspNetCore.Cluster 2.4.0</a>
* Added calls to `ConfigureAwait` in multiple places
* Fixed node status tracking when TCP or UDP transport in use

# 05-11-2020
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.3.2">DotNext.AspNetCore.Cluster 2.3.2</a>
* Section with local node configuration can be defined explicitly

# 05-09-2020
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.3.1">DotNext.AspNetCore.Cluster 2.3.1</a>
* Alternative methods for configuring local node

# 04-23-2020
<a href="https://www.nuget.org/packages/dotnext/2.3.0">DotNext 2.3.0</a>
* Performance improvements of `BitwiseComparer` and `Intrinsics` classes  
* Introduced new [MemoryOwner&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.MemoryOwner-1.html) value type that unifies working with memory and array pools
* Path MTU [discovery](https://sakno.github.io/dotNext/api/DotNext.Net.NetworkInformation.MtuDiscovery.html)
* Pooled buffer writes: [PooledBufferWriter&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.PooledBufferWriter-1.html) and [PooledArrayBufferWriter&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Buffers.PooledArrayBufferWriter-1.html)
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.3.0">DotNext.Metaprogramming 2.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/2.3.0">DotNext.Unsafe 2.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/2.3.0">DotNext.IO 2.3.0</a>
* Fixed bugs that lead to unexpected EndOfStreamException in some methods of `StreamExtensions` class
* Introduced new methods in `StreamExtensions` class for reading data of exact size

<a href="https://www.nuget.org/packages/dotnext.threading/2.3.0">DotNext.Threading 2.3.0</a>
* Improved performance of existing asynchronous locks
* Added [AsyncTrigger](https://sakno.github.io/dotNext/api/DotNext.Threading.AsyncTrigger.html) synchronization primitive

<a href="https://www.nuget.org/packages/dotnext.reflection/2.3.0">DotNext.Reflection 2.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.3.0">DotNext.Net.Cluster 2.3.0</a>
* TCP transport for Raft
* UDP transport for Raft
* Fixed bug in [PersistentState](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.html) class that leads to incorrect usage of rented memory and unexpected result during replication between nodes
* Methods for handling Raft messages inside of [RaftCluster&lt;TMember&gt;](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.RaftCluster-1.html) class now support cancellation via token

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.3.0">DotNext.AspNetCore.Cluster 2.3.0</a>
* Updated dependencies
* Fixed cancellation of asynchronous operations

# 03-08-2020
<a href="https://www.nuget.org/packages/dotnext/2.2.0">DotNext 2.2.0</a>
* Ability to slice lists using range syntax and new `ListSegment` data type
* Various extension methods for broader adoption of range/index feature from C# 8

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.2.0">DotNext.Metaprogramming 2.2.0</a>
* Support of range and index expressions from C# 8

<a href="https://www.nuget.org/packages/dotnext.unsafe/2.2.0">DotNext.Unsafe 2.2.0</a>
* Access to memory-mapped file via `System.Memory<T>` data type

<a href="https://www.nuget.org/packages/dotnext.io/2.2.0">DotNext.IO 2.2.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/2.2.0">DotNext.Threading 2.2.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/2.2.0">DotNext.Reflection 2.2.0</a>
* Lighweight API for fast reflection is added. See overloaded `Unreflect` methods in `Reflector` class.

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.2.0">DotNext.Net.Cluster 2.2.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.2.0">DotNext.AspNetCore.Cluster 2.2.0</a>
* Upgrade to latest ASP.NET Core

<a href="https://www.nuget.org/packages/dotnext.augmentation.fody/2.0.1">DotNext.Augmentation.Fody 2.0.1</a>
* Removed obsolete calls

# 02-23-2020
<a href="https://www.nuget.org/packages/dotnext/2.1.0">DotNext 2.1.0</a>
* Reduced memory footprint of `DotNext.Span` static constructor
* `DotNext.UserDataStorage` behavior is now customizable via `UserDataStorage.IContainer` interface
* Introduced `Intrinsics.GetReadonlyRef` method allows to reinterpret managed pointer to array element
* `DelegateHelpers.Bind` now supports both closed and open delegates

# 01-31-2020
Major release of version 2.0 is completely finished and contains polished existing and new API. All libraries in .NEXT family are upgraded. Migration guide for 1.x users is [here](https://sakno.github.io/dotNext/migration/1.html). Please consider that this version is not fully backward compatible with 1.x.

Major version is here for the following reasons:
1. .NET Core 3.1 LTS is finally released
1. .NET Standard 2.1 contains a lot of new API required for optimizations. The most expected API is asynchronous methods in [Stream](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) class. These enhancements are necessary for evolution of .NEXT library. For instance, new [DotNext.IO](https://www.nuget.org/packages/DotNext.IO/) library could not be released without new .NET API.
1. ASP.NET Core 2.2 is no longer supported by Microsoft. Therefore, [DotNext.AspNetCore.Cluster](https://www.nuget.org/packages/DotNext.AspNetCore.Cluster/) library of version 1.x relies on unmaintainable platform. Now it is based on ASP.NET Core 3.1 which has long-term support.

What is done in this release:
1. Quality-focused changes
    1. Removed trivial "one-liners" in **DotNext** library
    1. Reduced and unified API to work with unmanaged memory in **DotNext.Unsafe** library
    1. **DotNext.AspNetCore.Cluster** migrated to ASP.NET Core 3.1 LTS
    1. Increased test coverage and fixed bugs
    1. Additional optimizations of performance in [Write-Ahead Log](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.html)
    1. Fixed issue [#4](https://github.com/sakno/dotNext/issues/4)
    1. Introduced API for client interaction support described in Chapter 6 of [Raft dissertation](https://github.com/ongardie/dissertation/blob/master/book.pdf)
    1. Migration to C# 8 and nullable reference types
1. New features
    1. Introduced [DotNext.IO](https://www.nuget.org/packages/DotNext.IO/) library with unified asynchronous API surface for .NET streams and I/O [pipelines](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines). This API provides high-level methods for encoding and decoding of data such as strings and blittable types. In other words, if you want to have [BinaryReader](https://docs.microsoft.com/en-us/dotnet/api/system.io.binaryreader) or [BinaryWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.binarywriter) for pipelines then welcome!
    1. Ability to obtain result of [task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1) asynchronously when its result type is not known at compile-time
    1. Fast hexadecimal string conversion to `Span<byte>` and vice versa

Raft users are strongly advised to migrate to this new version.

# 01-12-2020
<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.2.11">DotNext.Net.Cluster 1.2.11</a>
* Ability to reconstruct internal state using `PersistentState.ReplayAsync` method

# 01-11-2020
<a href="https://www.nuget.org/packages/dotnext/1.2.10">DotNext 1.2.10</a>
* Fixed invalid behavior of `StreamSegment.Position` property

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.2.10">DotNext.Net.Cluster 1.2.10</a>
* Removed redundant validation of log entry index in `PersistentState`

# 12-06-2019
<a href="https://www.nuget.org/packages/dotnext.unsafe/1.2.10">DotNext.Unsafe 1.2.10</a>
* Fixed invalid usage of `GC.RemoveMemoryPressure` in `Reallocate` methods

# 12-04-2019
<a href="https://www.nuget.org/packages/dotnext/1.2.9">DotNext 1.2.9</a>
* `UserDataStorage` no longer stores **null** values in its internal dictionary
* Updated dependencies
* Migration to SourceLink 1.0.0

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/1.2.9">DotNext.Metaprogramming 1.2.9</a>
* Updated dependencies
* Migration to SourceLink 1.0.0

<a href="https://www.nuget.org/packages/dotnext.reflection/1.2.9">DotNext.Reflection 1.2.9</a>
* Updated dependencies
* Migration to SourceLink 1.0.0

<a href="https://www.nuget.org/packages/dotnext.threading/1.3.3">DotNext.Threading 1.3.3</a>
* Updated dependencies
* Migration to SourceLink 1.0.0

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.2.9">DotNext.Unsafe 1.2.9</a>
* Updated dependencies
* Fixed invalid calculation of byte length in `Pointer.Clear` method

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.2.9">DotNext.Net.Cluster 1.2.9</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.2.9">DotNext.AspNetCore.Cluster 1.2.9</a>
* Updated dependencies

# 11-27-2019
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.2.8">DotNext.AspNetCore.Cluster 1.2.8</a>
* Improved performance of one-way no-ack messages that can be passed using `ISubscriber.SendSignalAsync` method

# 11-25-2019
<a href="https://www.nuget.org/packages/dotnext/1.2.7">DotNext 1.2.7</a>
* `BitwiseComparer` now available as singleton instance

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.2.7">DotNext.Net.Cluster 1.2.7</a>
* Improved performance of copying log entry content when `PersistentState` is used as persistent audit trail

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.2.7">DotNext.AspNetCore.Cluster 1.2.7</a>
* Improved performance of message exchange between cluster members

# 11-24-2019
<a href="https://www.nuget.org/packages/dotnext/1.2.6">DotNext 1.2.6</a>
* Fixed typos in XML documentation
* Updated *InlineIL.Fody* dependency

<a href="https://www.nuget.org/packages/dotnext.threading/1.3.2">DotNext.Threading 1.3.2</a>
* Fixed `MissingManifestResourceException` caused by `AsyncLock` value type on .NET Core 3.x

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.2.6">DotNext.Unsafe 1.2.6</a>
* Updated *InlineIL.Fody* dependency

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.2.6">DotNext.Net.Cluster 1.2.6</a>
* Fixed NRE when `RaftCluster.StopAsync` called multiple times

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.2.6">DotNext.AspNetCore.Cluster 1.2.6</a>
* Migration to patched `RaftCluster` class

# 11-20-2019
<a href="https://www.nuget.org/packages/dotnext.threading/1.3.1">DotNext.Threading 1.3.1</a>
* Fixed NRE when `Dispose` method of [PersistentChannel](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.Channels.PersistentChannel-2.html) class called multiple times

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.2.5">DotNext.AspNetCore.Cluster 1.2.5</a>
* Fixed bug when log entry may have invalid content when retrieved from persistent audit trail. Usually this problem can be observed in case of concurrent read/write and caused by invalid synchronization of multiple file streams.

# 11-18-2019
<a href="https://www.nuget.org/packages/dotnext.threading/1.3.0">DotNext.Threading 1.3.0</a>
* [PersistentChannel](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.Channels.PersistentChannel-2.html) is added as an extension of **channel** concept from [System.Threading.Channels](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.threading.channels). It allows to use disk memory instead of RAM for storing messages passed from producer to consumer. Read more [here](https://sakno.github.io/dotNext/features/threading/channel.html)
* [AsyncCounter](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncCounter.html) allows to simplify asynchronous coordination in producer/consumer scenario

# 11-15-2019
<a href="https://www.nuget.org/packages/dotnext/1.2.4">DotNext 1.2.4</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/1.2.4">DotNext.Metaprogramming 1.2.4</a>
* Fixed NRE

<a href="https://www.nuget.org/packages/dotnext.reflection/1.2.4">DotNext.Reflection 1.2.4</a>
* Internal cache is optimized to avoid storage of null values

<a href="https://www.nuget.org/packages/dotnext.threading/1.2.4">DotNext.Threading 1.2.4</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.2.4">DotNext.Unsafe 1.2.4</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.2.4">DotNext.Net.Cluster 1.2.4</a>
* Fixed unnecessary boxing of generic log entry value

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.2.4">DotNext.AspNetCore.Cluster 1.2.4</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.augmentation.fody/1.2.4">DotNext.Augmentation.Fody 1.2.4</a>
* Updated dependencies

# 11-11-2019
<a href="https://www.nuget.org/packages/dotnext/1.2.3">DotNext 1.2.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/1.2.3">DotNext.Metaprogramming 1.2.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/1.2.3">DotNext.Reflection 1.2.3</a>
* Fixed potential NRE
* Fixed reflection of value type constructors
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/1.2.3">DotNext.Threading 1.2.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.2.3">DotNext.Unsafe 1.2.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.2.3">DotNext.Net.Cluster 1.2.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.2.3">DotNext.AspNetCore.Cluster 1.2.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.augmentation.fody/1.2.3">DotNext.Augmentation.Fody 1.2.3</a>
* Updated dependencies

# 11-05-2019
<a href="https://www.nuget.org/packages/dotnext/1.2.2">DotNext 1.2.2</a>
* Fixed bitwise equality
* Fixed `Intrinsics.IsDefault` method

# 11-02-2019
<a href="https://www.nuget.org/packages/dotnext/1.2.1">DotNext 1.2.1</a>
* Fixed type modifier of `Current` property declared in [CopyOnWriteList&lt;T&gt;.Enumerator](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Collections.Concurrent.CopyOnWriteList-1.Enumerator.html)

# 10-31-2019
<a href="https://www.nuget.org/packages/dotnext/1.2.0">DotNext 1.2.0</a>
* Fixed memory leaks caused by methods in [StreamExtensions](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.IO.StreamExtensions.html) class
* [MemoryRental](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Buffers.MemoryRental-1.html) type is introduced to replace memory allocation with memory rental in some scenarios
* [ArrayRental](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Buffers.ArrayRental-1.html) type is extended
* Value Delegates now are protected from _dangling pointer_ issue caused by dynamic assembly loading 
* Reduced amount of memory utilized by random string generation methods
* Strict package versioning rules are added to avoid accidental upgrade to major version
* Improved performance of [AtomicEnum](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.AtomicEnum.html) methods
* Improved performance of [Atomic&lt;T&gt;](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.Atomic-1.html) using optimistic read locks
* Fixed unnecessary boxing in atomic operations
* `Intrinsics.HasFlag` static generic method is added as boxing-free and fast alternative to [Enum.HasFlag](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.enum.hasflag?view=netcore-2.2#System_Enum_HasFlag_System_Enum_) method

<a href="https://www.nuget.org/packages/dotnext.reflection/1.2.0">DotNext.Reflection 1.2.0</a>
* Updated version of `DotNext` dependency to fix potential memory leaks
* Strict package versioning rules are added to avoid accidental upgrade to major version

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/1.2.0">DotNext.Metaprogramming 1.2.0</a>
* Updated version of `DotNext` dependency to fix potential memory leaks
* Strict package versioning rules are added to avoid accidental upgrade to major version

<a href="https://www.nuget.org/packages/dotnext.threading/1.2.0">DotNext.Threading 1.2.0</a>
* Updated version of `DotNext` dependency to fix potential memory leaks
* Strict package versioning rules are added to avoid accidental upgrade to major version
* [AsyncReaderWriterLock](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncReaderWriterLock.html) now supports optimistic reads

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.2.0">DotNext.Unsafe 1.2.0</a>
* [UnmanagedMemoryPool](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Buffers.UnmanagedMemoryPool-1.html) is added
* Strict package versioning rules are added to avoid accidental upgrade to major version

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.2.0">DotNext.Net.Cluster 1.2.0</a>
* Updated version of `DotNext` dependency to fix potential memory leaks
* Strict package versioning rules are added to avoid accidental upgrade to major version
* Fixed incorrect computation of partition in `PersistentState.DropAsync` method

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.2.0">DotNext.AspNetCore.Cluster 1.2.0</a>
* HTTP/2 support
* Performance optimizations caused by changes in `ArrayRental` type
* Strict package versioning rules are added to avoid accidental upgrade to major version

<a href="https://www.nuget.org/packages/dotnext.augmentation.fody/1.2.0">DotNext.Augmentation.Fody 1.2.0</a>
* Improved support of `ValueRefAction` and `ValueRefFunc` value delegates

# 10-12-2019
<a href="https://www.nuget.org/packages/dotnext/1.1.0">DotNext 1.1.0</a>
* Reduced number of inline IL code
* Updated version of FxCop analyzer
* [ReaderWriterSpinLock](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.ReaderWriterSpinLock.html) type is introduced
* Improved performance of [UserDataStorage](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.UserDataStorage.html)

<a href="https://www.nuget.org/packages/dotnext.reflection/1.1.0">DotNext.Reflection 1.1.0</a>
* Updated version of FxCop analyzer
* Improved performance of internal caches

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/1.1.0">DotNext.Metaprogramming 1.1.0</a>
* Updated version of FxCop analyzer
* `RefAnyValExpression` is added

<a href="https://www.nuget.org/packages/dotnext.threading/1.1.0">DotNext.Threading 1.1.0</a>
* Updated version of FxCop analyzer

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.1.0">DotNext.Unsafe 1.1.0</a>
* Updated version of FxCop analyzer

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.1.0">DotNext.Net.Cluster 1.1.0</a>
* Minor performance optimizations of persistent WAL
* Updated version of FxCop analyzer

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.1.0">DotNext.AspNetCore.Cluster 1.1.0</a>
* Updated version of FxCop analyzer

# 10-02-2019
<a href="https://www.nuget.org/packages/dotnext/1.0.1">DotNext 1.0.1</a>
* Minor performance optimizations

<a href="https://www.nuget.org/packages/dotnext.reflection/1.0.1">DotNext.Reflection 1.0.1</a>
* Minor performance optimizations

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/1.0.1">DotNext.Metaprogramming 1.0.1</a>
* Minor performance optimizations

<a href="https://www.nuget.org/packages/dotnext.threading/1.0.1">DotNext.Threading 1.0.1</a>
* Introduced [AsyncSharedLock](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncSharedLock.html) as combination of reader/write lock and semaphore
* Minor performance optimizations

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.0.1">DotNext.Unsafe 1.0.1</a>
* Minor performance optimizations

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.0.1">DotNext.Net.Cluster 1.0.1</a>
* Minor performance optimizations

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.0.1">DotNext.AspNetCore.Cluster 1.0.1</a>
* Minor performance optimizations

<a href="https://www.nuget.org/packages/dotnext.augmentation.fody/1.0.1">DotNext.Augmentation.Fody 1.0.1</a>
* Code refactoring

# 10-02-2019
This is the major release of all parts of .NEXT library. Now the version is 1.0.0 and backward compatibility is guaranteed across all 1.x releases. The main motivation of this release is to produce stable API because .NEXT library active using in production code, especially Raft implementation.

.NEXT 1.x is based on .NET Standard 2.0 to keep compatibility with .NET Framework.

<a href="https://www.nuget.org/packages/dotnext/1.0.0">DotNext 1.0.0</a>
* Optimized methods of [Memory](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Runtime.InteropServices.Memory.html) class
* Extension methods for I/O are introduced. Now you don't need to instantiate [BinaryReader](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.io.binaryreader) or [BinaryWriter](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.io.binarywriter) for high-level parsing of stream content. Encoding and decoding of strings are fully supported. Moreover, these methods are asynchronous in contrast to methods of `BinaryReader` and `BinaryWriter`.

<a href="https://www.nuget.org/packages/dotnext.reflection/1.0.0">DotNext.Reflection 1.0.0</a>
* API is stabilized

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/1.0.0">DotNext.Metaprogramming 1.0.0</a>
* API is stabilized

<a href="https://www.nuget.org/packages/dotnext.threading/1.0.0">DotNext.Threading 1.0.0</a>
* [AsyncManualResetEvent](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncManualResetEvent.html) has auto-reset optional behavior which allows to repeatedly unblock many waiters

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.0.0">DotNext.Unsafe 1.0.0</a>
* [MemoryMappedFileExtensions](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.IO.MemoryMappedFiles.MemoryMappedFileExtensions.html) allows to work with virtual memory associated with memory-mapped file using unsafe pointer or [Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.span-1) to achieve the best performance.

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.0.0">DotNext.Net.Cluster 1.0.0</a>
* Audit trail programming model is redesigned
* Persistent and high-performance Write Ahead Log (WAL) is introduced. Read more [here](https://sakno.github.io/dotNext/features/cluster/aspnetcore.html#replication)
* Log compaction is supported

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.0.0">DotNext.AspNetCore.Cluster 1.0.0</a>
* Redirection to leader now uses `307 Temporary Redirect` instead of `302 Moved Temporarily` by default
* Compatibility with persistent WAL is provided

<a href="https://www.nuget.org/packages/dotnext.augmentation.fody/1.0.0">DotNext.Augmentation.Fody 1.0.0</a>
* Behavior of augmented compilation is stabilized

# 09-03-2019
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.5.7">DotNext.AspNetCore.Cluster 0.5.7</a>
* Custom redirection logic can be asynchronous
* Fixed compatibility of redirection to leader with MVC

# 09-02-2019
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.5.5">DotNext.AspNetCore.Cluster 0.5.5</a>
* Automatic redirection to leader now works correctly with reverse proxies
* Custom redirection logic is introduced

# 08-31-2019
<a href="https://www.nuget.org/packages/dotnext/0.14.0">DotNext 0.14.0</a>
* [Timestamp](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Diagnostics.Timestamp.html) type is introduced as allocation-free alternative to [Stopwatch](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.diagnostics.stopwatch)
* [Memory](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Runtime.InteropServices.Memory.html) class now have methods for reading and writing null-terminated UTF-16 string from/to unmanaged or pinned managed memory
* Updated InlineIL dependency to 1.3.1

<a href="https://www.nuget.org/packages/dotnext.threading/0.14.0">DotNext.Threading 0.14.0</a>
* [AsyncTimer](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncTimer.html) is completely rewritten in backward-incompatible way. Wait handle are no longer used.

<a href="https://www.nuget.org/packages/dotnext.unsafe/0.14.0">DotNext.Unsafe 0.14.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.reflection/0.14.0">DotNext.Reflection 0.14.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/0.14.0">DotNext.Metaprogramming 0.14.0</a>
* Small code fixes
* Updated `DotNext` dependency to 0.14.0
* Updated `Fody` dependency to 6.0.0
* Updated augmented compilation to 0.14.0

<a href="https://www.nuget.org/packages/dotnext.net.cluster/0.5.0">DotNext.Net.Cluster 0.5.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.5.0">DotNext.AspNetCore.Cluster 0.5.0</a>
* Measurement of runtime metrics are introduced and exposed through [MetricsCollector](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Net.Cluster.Consensus.Raft.MetricsCollector.html) and [HttpMetricsCollector](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Net.Cluster.Consensus.Raft.Http.HttpMetricsCollector.html) classes

<a href="https://www.nuget.org/packages/dotnext.augmentation.fody/0.14.0">DotNext.Augmentation.Fody 0.14.0</a>
* Updated `Fody` dependency to 6.0.0

# 08-28-2019
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.4.0">DotNext.AspNetCore.Cluster 0.4.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.net.cluster/0.4.0">DotNext.Net.Cluster 0.4.0</a>
* Heartbeat timeout can be tuned through configuration
* Optimized Raft state machine

# 08-27-2019
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.3.5">DotNext.AspNetCore.Cluster 0.3.5</a><br/>
* Docker support

# 08-22-2019
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.3.3">DotNext.AspNetCore.Cluster 0.3.3</a><br/>
<a href="https://www.nuget.org/packages/dotnext.net.cluster/0.3.3">DotNext.Net.Cluster 0.3.3</a>
* Reduced number of logs produced by cluster node

# 08-21-2019
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.3.2">DotNext.AspNetCore.Cluster 0.3.2</a>
* Fixed endpoint redirection to leader node

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.3.1">DotNext.AspNetCore.Cluster 0.3.1</a>
* Fixed detection of local IP address
* Improved IPv6 support

# 08-20-2019
<a href="https://www.nuget.org/packages/dotnext/0.13.0">DotNext 0.13.0</a>
* Fixed bug with equality comparison of **null** arrays inside of [EqualityComparerBuilder](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.EqualityComparerBuilder-1.html)
* Improved debugging experience:
	* SourceLink is enabled
	* Debug symbols now embedded into assembly file
	* NuGet Symbols Package is no longer used

<a href="https://www.nuget.org/packages/dotnext.threading/0.13.0">DotNext.Threading 0.13.0</a>
* Internals of several classes now based on Value Delegates to reduce memory allocations
* Improved debugging experience:
	* SourceLink is enabled
	* Debug symbols now embedded into assembly file
	* NuGet Symbols Package is no longer used

<a href="https://www.nuget.org/packages/dotnext.unsafe/0.13.0">DotNext.Unsafe 0.13.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.reflection/0.13.0">DotNext.Reflection 0.13.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/0.13.0">DotNext.Metaprogramming 0.13.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.net.cluster/0.3.0">DotNext.Net.Cluster 0.3.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.3.0">DotNext.AspNetCore.Cluster 0.3.0</a>
* Improved debugging experience:
	* SourceLink is enabled
	* Debug symbols now embedded into assembly file
	* NuGet Symbols Package is no longer used

# 08-18-2019
<a href="https://www.nuget.org/packages/dotnext/0.12.0">DotNext 0.12.0</a>
* Value (struct) Delegates are introduced as allocation-free alternative to classic delegates
* Atomic&lt;T&gt; is added to provide atomic memory access operations for arbitrary value types
* Arithmetic, bitwise and comparison operations for [IntPtr](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.intptr) and [UIntPtr](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.uintptr)
* Improved performance of methods declared in [EnumConverter](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.EnumConverter.html)
* Improved performance of atomic operations
* Improved performance of bitwise equality and bitwise comparison methods for value types
* Improved performance of [IsDefault](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Runtime.Intrinsics.html#DotNext_Runtime_Intrinsics_IsDefault__1_) method which allows to check whether the arbitrary value of type `T` is `default(T)`
* GetUnderlyingType() method is added to obtain underlying type of Result&lt;T&gt;
* [TypedReference](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.typedreference) can be converted into managed pointer (type T&amp;, or ref T) using [Memory](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Runtime.InteropServices.Memory.html) class

This release introduces a new feature called Value Delegates which are allocation-free alternative to regular .NET delegates. Value Delegate is a value type which holds a pointer to the managed method and can be invoked using `Invoke` method in the same way as regular .NET delegate. Read more [here](https://sakno.github.io/dotNext/features/core/valued.html).

`ValueType<T>` is no longer exist and most of its methods moved into [BitwiseComparer](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.BitwiseComparer-1.html) class.

<a href="https://www.nuget.org/packages/dotnext.reflection/0.12.0">DotNext.Reflection 0.12.0</a>
* Ability to obtain managed pointer (type T&amp;, or `ref T`) to static or instance field from [FieldInfo](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.reflection.fieldinfo) using [Reflector](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Reflection.Reflector.html) class

<a href="https://www.nuget.org/packages/dotnext.threading/0.12.0">DotNext.Threading 0.12.0</a>
* [AsyncLazy&lt;T&gt;](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncLazy-1.html) is introduced as asynchronous alternative to [Lazy&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.lazy-1) class

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/0.12.0">DotNext.Metaprogramming 0.12.0</a>
* [Null-safe navigation expression](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Linq.Expressions.NullSafetyExpression.html) is introduced

<a href="https://www.nuget.org/packages/dotnext.unsafe/0.12.0">DotNext.Unsafe 0.12.0</a>
* [UnmanagedFunction](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Runtime.InteropServices.UnmanagedFunction.html) and [UnmanagedFunction&lt;R&gt;](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Runtime.InteropServices.UnmanagedFunction-1.html) classes are introduced to call unmanaged functions by pointer

<a href="https://www.nuget.org/packages/dotnext.net.cluster/0.2.0">DotNext.Net.Cluster 0.2.0</a>
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.2.0">DotNext.AspNetCore.Cluster 0.2.0</a>
* Raft client is now capable to ensure that changes are committed by leader node using [WriteConcern](https://sakno.github.io/dotNext/versions/1.x/api/DotNext.Net.Cluster.Replication.WriteConcern.html)