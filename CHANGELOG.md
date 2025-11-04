Release Notes
====

# 11-03-2025
<a href="https://www.nuget.org/packages/dotnext/5.26.1">DotNext 5.26.1</a>
* Lock upgrade logic provided by `ReaderWriterSpinLock` is adjusted according to [275](https://github.com/dotnet/dotNext/issues/275)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.26.1">DotNext.Metaprogramming 5.26.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.26.1">DotNext.Unsafe 5.26.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.26.1">DotNext.Threading 5.26.1</a>
* Lock upgrade logic provided by `AsyncReaderWriterLock` is adjusted according to [275](https://github.com/dotnet/dotNext/issues/275)
* Improved accuracy of `CancellationTokenMultiplexer.Scope.IsTimedOut` property

<a href="https://www.nuget.org/packages/dotnext.io/5.26.1">DotNext.IO 5.26.1</a>
* Added auxiliary `MemorySegmentStream` wrapper over [Memory&lt;byte&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.memory-1) type in the form of the writable stream
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.26.1">DotNext.Net.Cluster 5.26.1</a>
* Fixed [276](https://github.com/dotnet/dotNext/issues/276)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.26.1">DotNext.AspNetCore.Cluster 5.26.1</a>
* Fixed [276](https://github.com/dotnet/dotNext/issues/276)

# 10-22-2025
<a href="https://www.nuget.org/packages/dotnext/5.26.0">DotNext 5.26.0</a>
* Introduced `DotNext.IO.ModernStream` class that derives from [Stream](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream) and implements many of the methods introduced since .NET Framework 1.1 by default in a modern way, requiring only minimal subset of core methods to be implemented by the derived class
* Removed async state machine allocations for ad-hoc streams returned by `StreamSource` factory methods

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.26.0">DotNext.Metaprogramming 5.26.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.26.0">DotNext.Unsafe 5.26.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.26.0">DotNext.Threading 5.26.0</a>
* Improved support of timeouts in `CancellationTokenMultiplexer`. The scope object now has explicit property to detect whether the multiplexed token source is cancelled due to timeout

<a href="https://www.nuget.org/packages/dotnext.io/5.26.0">DotNext.IO 5.26.0</a>
* Migration to `ModernStream` class

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.26.0">DotNext.Net.Cluster 5.26.0</a>
* Migration of Raft implementation to `CancellationTokenMultiplexer`

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.26.0">DotNext.AspNetCore.Cluster 5.26.0</a>
* Migration of Raft implementation to `CancellationTokenMultiplexer`

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/0.5.0">DotNext.MaintenanceServices 0.7.0</a>
* Migration to `System.CommandLine` release candidate
* Added custom parser configuration (`ParserConfiguration` class) that can be registered in DI

# 09-29-2025
<a href="https://www.nuget.org/packages/dotnext.threading/5.25.2">DotNext.Threading 5.25.2</a>
* Fixed [272](https://github.com/dotnet/dotNext/pull/272)

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.25.2">DotNext.Net.Cluster 5.25.2</a>
* Forced upgrade to newer `DotNext.Threading` library

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.25.2">DotNext.AspNetCore.Cluster 5.25.2</a>
* Forced upgrade to newer `DotNext.Threading` library

# 09-15-2025
<a href="https://www.nuget.org/packages/dotnext/5.25.0">DotNext 5.25.0</a>
* Added `CatchException` extension method to capture the exception produced by `await` operator instead of raising it at the call site
* Various performance improvements

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.25.0">DotNext.Metaprogramming 5.25.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.25.0">DotNext.Unsafe 5.25.0</a>
* Fixed mutability modifiers for properties of `UnmanagedMemory<T>` type

<a href="https://www.nuget.org/packages/dotnext.threading/5.25.0">DotNext.Threading 5.25.0</a>
* Added optional hard concurrency limit for async lock primitives
* Rewritten the internal engine for async lock primitives to decrease the lock contention and increase the response time
* Async lock primitive no longer produce lock contention time to improve the response time

<a href="https://www.nuget.org/packages/dotnext.io/5.25.0">DotNext.IO 5.25.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.25.0">DotNext.Net.Cluster 5.25.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.25.0">DotNext.AspNetCore.Cluster 5.25.0</a>
* Updated dependencies

# 08-23-2025
<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.24.1">DotNext.Net.Cluster 5.24.1</a>
* Fixed stream ID inflation of the multiplexing protocol client if underlying TCP connection is unstable

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.24.1">DotNext.AspNetCore.Cluster 5.24.1</a>
* Updated dependencies

# 08-19-2025
<a href="https://www.nuget.org/packages/dotnext/5.24.0">DotNext 5.24.0</a>
* Merged [258](https://github.com/dotnet/dotNext/pull/258)
* Added `CopyTo` extension method overload for [ReadOnlySequence&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) data type that returns the position within the sequence
* Fixed correctness of atomic read/write operations exposed by `Atomic` static class for **double** data type on 32-bit platforms
* `LockAcquisition` static methods are no longer extension methods to avoid ambiguity (see [267](https://github.com/dotnet/dotNext/discussions/267))

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.24.0">DotNext.Metaprogramming 5.24.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.24.0">DotNext.Unsafe 5.24.0</a>
* Added custom marshallers for `IUnmanagedMemory<T>` interface and `Pointer<T>` data type that are compatible with PInvoke source generator

<a href="https://www.nuget.org/packages/dotnext.threading/5.24.0">DotNext.Threading 5.24.0</a>
* `AsyncLockAcquisition` static methods are no longer extension methods to avoid ambiguity (see [267](https://github.com/dotnet/dotNext/discussions/267))
* Lock contention reported by all async lock primitives is now an up-down counter rather than regular counter

<a href="https://www.nuget.org/packages/dotnext.io/5.24.0">DotNext.IO 5.24.0</a>
* Improved behavioral compatibility with [Pipe](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipe) class by extension methods exposed by `PipeExtensions` class

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.24.0">DotNext.Net.Cluster 5.24.0</a>
* Added `DotNext.Net.Multiplexing` namespace that exposes simple unencrypted multiplexing protocol implementation on top of TCP. The multiplexed channel is exposed as [IDuplexPipe](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.iduplexpipe). The main purpose of this implementation is the efficient communication between nodes within the cluster inside the trusted LAN

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.24.0">DotNext.AspNetCore.Cluster 5.24.0</a>
* Updated dependencies

# 06-29-2025
<a href="https://www.nuget.org/packages/dotnext/5.23.0">DotNext 5.23.0</a>
* Added `Atomic.Read` and `Atomic.Write` methods for **long** and **ulong** data types for cross-architecture support of atomic reads/writes
* Fixed compatibility with 32-bit arch for `Timstamp` data type

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.23.0">DotNext.Metaprogramming 5.23.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.23.0">DotNext.Unsafe 5.23.0</a>
* Added `UnmanagedMemory<T>` data type that provides allocation of the blittable value in the unmanaged memory in CLS-compliant manner
* Introduced `UnmanagedMemory.Discard` static method for detaching of the physical memory from the virtual memory page

<a href="https://www.nuget.org/packages/dotnext.threading/5.23.0">DotNext.Threading 5.23.0</a>
* Fixed compatibility with 32-bit arch for asynchronous primitives and `IndexPool` data type
* Replaced spin wait with the monitor lock for value task sources

<a href="https://www.nuget.org/packages/dotnext.io/5.23.0">DotNext.IO 5.23.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.23.0">DotNext.Net.Cluster 5.23.0</a>
* Introduced private memory allocation type for `WriteAheadLog` class
* Added support of Linux Transparent Huge Pages for `WriteAheadLog` class when private memory allocation is enabled
* `WriteAheadLog.FlushAsync` ensures that the background flush is completed

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.23.0">DotNext.AspNetCore.Cluster 5.23.0</a>
* Updated dependencies

# 05-30-2025
<a href="https://www.nuget.org/packages/dotnext/5.22.0">DotNext 5.22.0</a>
* Added `!` operator overloading for the result and optional types: [261](https://github.com/dotnet/dotNext/pull/261)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.22.0">DotNext.Metaprogramming 5.22.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.22.0">DotNext.Unsafe 5.22.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.22.0">DotNext.Threading 5.22.0</a>
* Added `Interrupt` method to `AsyncTrigger` class

<a href="https://www.nuget.org/packages/dotnext.io/5.22.0">DotNext.IO 5.22.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.22.0">DotNext.Net.Cluster 5.22.0</a>
* Introduced a new `WriteAheadLog` experimental class as long-term replacement of `MemoryBasedStateMachine` and `DiskBasedStateMachine`. Both classes remain available in the current major version. However, they are subject to removal in the next major version. A new write-ahead log provides much better performance, simpler configuration and simpler API

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.22.0">DotNext.AspNetCore.Cluster 5.22.0</a>
* Added new configuration helper methods to support new write-ahead log

# 04-08-2025
<a href="https://www.nuget.org/packages/dotnext/5.21.0">DotNext 5.21.0</a>
* Added `Disposable.CreateException()` protected method

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.21.0">DotNext.Metaprogramming 5.21.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.21.0">DotNext.Unsafe 5.21.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.21.0">DotNext.Threading 5.21.0</a>
* Added `Contains`, `ReplaceAsync` methods to `RandomAccessCache<TKey, TValue>` class as well as scanning enumerator

<a href="https://www.nuget.org/packages/dotnext.io/5.21.0">DotNext.IO 5.21.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.21.0">DotNext.Net.Cluster 5.21.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.21.0">DotNext.AspNetCore.Cluster 5.21.0</a>
* Updated dependencies

# 03-30-2025
<a href="https://www.nuget.org/packages/dotnext/5.20.0">DotNext 5.20.0</a>
* Introduced `List.Repeat()` static method to construct read-only lists of repeatable items. Similar to [Enumerable.Repeat](https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.repeat) but returns [IReadOnlyList&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1)
* Added `Number.RoundUp` and `Number.RoundDown` generic extension methods to round the numbers to the multiple of the specified value

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.20.0">DotNext.Metaprogramming 5.20.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.20.0">DotNext.Unsafe 5.20.0</a>
* Added static methods to `UnmanagedMemory` for page-aligned memory allocation

<a href="https://www.nuget.org/packages/dotnext.threading/5.20.0">DotNext.Threading 5.20.0</a>
* Improved debugging experience of `RandomAccessCache<TKey, TValue>` class
* Added `AsyncCounter.TryIncrement` method that allows to specify the upper bound of the counter. As a result, `AsyncCounter` class can be used as a rate limiter

<a href="https://www.nuget.org/packages/dotnext.io/5.20.0">DotNext.IO 5.20.0</a>
* Introduced `DiskSpacePool` class to assist with building caches when the cached data stored on the disk rather than in memory. The class can be combined with `RandomAccessCache<TKey, TValue>` to organize L1 application-specific caches (while L0 resides in memory and can be organized on top of `RandomAccessCache<TKey, TValue>` as well)

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.20.0">DotNext.Net.Cluster 5.20.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.20.0">DotNext.AspNetCore.Cluster 5.20.0</a>
* Updated dependencies

# 03-06-2025
<a href="https://www.nuget.org/packages/dotnext/5.19.1">DotNext 5.19.1</a>
* Smallish performance improvements of `SparseBufferWriter<T>`

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.19.1">DotNext.Metaprogramming 5.19.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.19.1">DotNext.Unsafe 5.19.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.19.1">DotNext.Threading 5.19.1</a>
* Fixed weight counting in `RandomAccessCache<TKey, TValue, TWeight>` class

<a href="https://www.nuget.org/packages/dotnext.io/5.19.1">DotNext.IO 5.19.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.19.1">DotNext.Net.Cluster 5.19.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.19.1">DotNext.AspNetCore.Cluster 5.19.1</a>
* Updated dependencies

# 03-03-2025
<a href="https://www.nuget.org/packages/dotnext/5.19.0">DotNext 5.19.0</a>
* Added `ConsoleLifetimeTokenSource` that exposes the cancellation token bounded to the console application lifetime
* Added more pipelined methods to work with `Optional<T>` and `Result<T>` in async code

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.19.0">DotNext.Metaprogramming 5.19.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.19.0">DotNext.Unsafe 5.19.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.19.0">DotNext.Threading 5.19.0</a>
* Added weight-based cache on top of existing `RandomAccessCache<TKey, TValue>` class
* Fixed potential race conditions within `RandomAccessCache<TKey, TValue>` class
* `RandomAccessCache<TKey, TValue, TWeight>` can grow dynamically depending on the number of hash collisions

<a href="https://www.nuget.org/packages/dotnext.io/5.19.0">DotNext.IO 5.19.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.19.0">DotNext.Net.Cluster 5.19.0</a>
* Reused FNV1a hash implementation
* Improved WAL performance if `WriteMode.NoFlush` is chosen

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.19.0">DotNext.AspNetCore.Cluster 5.19.0</a>
* Updated dependencies

# 01-20-2025
<a href="https://www.nuget.org/packages/dotnext/5.18.0">DotNext 5.18.0</a>
* Introduced `FileUri` class that allows to convert Windows/Unix file names to URI according to `file://` scheme

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.18.0">DotNext.Metaprogramming 5.18.0</a>
* Introduced expression tree for unsigned right shift operator `>>>`

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.18.0">DotNext.Unsafe 5.18.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.18.1">DotNext.Threading 5.18.1</a>
* Synchronous `TryAcquire` implemented by `AsyncExclusiveLock` and `AsyncReaderWriterLock` are now implemented in portable way. Previously, WASM target was not supported. Additionally, the method supports lock stealing
* * Improved synchronous support for `RandomAccessCache` class

<a href="https://www.nuget.org/packages/dotnext.io/5.18.2">DotNext.IO 5.18.2</a>
* Fixed issue of `PoolingBufferedStream` class when the stream has buffered bytes in the write buffer and `Position` is set to backward
* Fixed [256](https://github.com/dotnet/dotNext/issues/256)

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.18.0">DotNext.Net.Cluster 5.18.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.18.0">DotNext.AspNetCore.Cluster 5.18.0</a>
* Updated dependencies

# 01-03-2025
<a href="https://www.nuget.org/packages/dotnext/5.17.2">DotNext 5.17.2</a>
* Improved AOT compatibility
* Fixed nullability attributes

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.17.2">DotNext.Metaprogramming 5.17.2</a>
* Fixed nullability attributes

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.17.2">DotNext.Unsafe 5.17.2</a>
* Fixed nullability attributes

<a href="https://www.nuget.org/packages/dotnext.threading/5.17.2">DotNext.Threading 5.17.2</a>
* Fixed nullability attributes

<a href="https://www.nuget.org/packages/dotnext.io/5.17.2">DotNext.IO 5.17.2</a>
* Fixed nullability attributes

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.17.2">DotNext.Net.Cluster 5.17.2</a>
* Fixed nullability attributes

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.17.2">DotNext.AspNetCore.Cluster 5.17.2</a>
* Fixed nullability attributes

# 12-30-2024
<a href="https://www.nuget.org/packages/dotnext.io/5.17.1">DotNext.IO 5.17.1</a>
* Fixed `EndOfStreamException` caused by async read from `PoolingBufferedStream`

# 12-29-2024
This release is aimed to improve AOT compatibility. All the examples in the repo are now AOT compatible.
<a href="https://www.nuget.org/packages/dotnext/5.17.0">DotNext 5.17.0</a>
* Fixed AOT compatibility in `TaskType` class
* Added [ISpanFormattable](https://learn.microsoft.com/en-us/dotnet/api/system.ispanformattable) and [IParsable&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.iparsable-1) interfaces to `HttpEndPoint`
* Introduced `TryEncodeAsUtf8` extension method for `SpanWriter<T>`
* Added more factory methods to `DotNext.Buffers.Memory` class to create [ReadOnlySequence&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1)
* `Intrinsics.KeepAlive` is introduced for value types
* Added `Synchronization.Wait()` synchronous methods for blocking wait of [value tasks](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask) without wait handles

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.17.0">DotNext.Metaprogramming 5.17.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.17.0">DotNext.Unsafe 5.17.0</a>
* Improved AOT support
* Fixed finalizer for unmanaged memory manager that allows to release the allocated unmanaged memory automatically by GC to avoid memory leak
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.17.0">DotNext.Threading 5.17.0</a>
* Improved AOT support

<a href="https://www.nuget.org/packages/dotnext.io/5.17.0">DotNext.IO 5.17.0</a>
* Reduced memory consumption for applications that use `FileReader` and `FileWriter` classes. These classes are now implemented by using lazy buffer pattern. It means that the different instances can reuse the same buffer taken from the pool
* Fixed [255](https://github.com/dotnet/dotNext/issues/255)
* `PoolingBufferedStream` is introduced to replace classic [BufferedStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.bufferedstream). This class supports memory pooling and implements lazy buffer pattern

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.17.0">DotNext.Net.Cluster 5.17.0</a>
* Improved AOT support

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.17.0">DotNext.AspNetCore.Cluster 5.17.0</a>
* Improved AOT support
* Fixed [254](https://github.com/dotnet/dotNext/issues/254)

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/0.5.0">DotNext.MaintenanceServices 0.5.0</a>
* Improved AOT support

# 12-07-2024
<a href="https://www.nuget.org/packages/dotnext/5.16.0">DotNext 5.16.1</a>
* Added [LEB128](https://en.wikipedia.org/wiki/LEB128) encoder and decoder as a public API. See `DotNext.Buffers.Binary.Leb128<T>` type for more information
* Added `SlideToEnd` method to `SpanWriter<T>` type
* Added `IsBitSet` and `SetBit` generic methods to `Number` type
* Added `DetachOrCopyBuffer` to `BufferWriterSlim<T>` type

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.16.1">DotNext.Metaprogramming 5.16.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.16.1">DotNext.Unsafe 5.16.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.16.1">DotNext.Threading 5.16.1</a>
* Async locks with synchronous acquisition methods now throw [LockRecursionException](https://learn.microsoft.com/en-us/dotnet/api/system.threading.lockrecursionexception) if the current thread tries to acquire the lock synchronously and recursively.
* Added support of cancellation token to synchronous acquisition methods of `AsyncExclusiveLock` and `AsyncReaderWriterLock` classes
* Introduced `LinkTo` method overload that supports multiple cancellation tokens

<a href="https://www.nuget.org/packages/dotnext.io/5.16.1">DotNext.IO 5.16.1</a>
* Introduced `RandomAccessStream` class that represents [Stream](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream) wrapper over the underlying data storage that supports random access pattern
* Added extension method for `SpanWriter<byte>` that provides length-prefixed string encoding

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.16.1">DotNext.Net.Cluster 5.16.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.16.1">DotNext.AspNetCore.Cluster 5.16.1</a>
* Updated dependencies

# 10-16-2024
<a href="https://www.nuget.org/packages/dotnext.threading/5.15.0">DotNext.Threading 5.15.0</a>
* Added support of synchronous lock acquisition to `AsyncExclusiveLock`, `AsyncReaderWriterLock`, `AsyncManualResetEvent`, `AsyncAutoResetEvent` so the users can easily migrate step-by-step from monitors and other synchronization primitives to async-friendly primitives
* Fixed random `InvalidOperationException` caused by `RandomAccessCache<TKey, TValue>`
* Added synchronous methods to `RandomAccessCache<TKey, TValue>` to support [251](https://github.com/dotnet/dotNext/issues/251) feature request

# 10-13-2024
<a href="https://www.nuget.org/packages/dotnext/5.14.0">DotNext 5.14.0</a>
* Added helpers to `DelegateHelpers` class to convert delegates with synchronous signature to their asynchronous counterparts
* Added support of async enumerator to `SingletonList<T>`
* Fixed exception propagation in `DynamicTaskAwaitable`
* Added support of [ConfigureAwaitOptions](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.configureawaitoptions) to `DynamicTaskAwaitable`

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.14.0">DotNext.Metaprogramming 5.14.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.14.0">DotNext.Unsafe 5.14.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.14.0">DotNext.Threading 5.14.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/5.14.0">DotNext.IO 5.14.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.14.0">DotNext.Net.Cluster 5.14.0</a>
* Fixed graceful shutdown of Raft TCP listener
* Updated vulnerable dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.14.0">DotNext.AspNetCore.Cluster 5.14.0</a>
* Updated vulnerable dependencies

# 08-30-2024
<a href="https://www.nuget.org/packages/dotnext/5.13.0">DotNext 5.13.0</a>
* Improved interoperability of `DotNext.Runtime.ValueReference<T>` and `DotNext.Runtime.ReadOnlyValueReference<T>` with .NEXT ecosystem
* Fixed [249](https://github.com/dotnet/dotNext/issues/249)
* Improved codegen quality for ad-hoc enumerator types

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.13.0">DotNext.Metaprogramming 5.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.13.0">DotNext.Unsafe 5.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.13.0">DotNext.Threading 5.13.0</a>
* Redesigned `AsyncEventHub` to improve overall performance and reduce memory allocation
* Improved codegen quality for ad-hoc enumerator types

<a href="https://www.nuget.org/packages/dotnext.io/5.13.0">DotNext.IO 5.13.0</a>
* Improved codegen quality for ad-hoc enumerator types

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.13.0">DotNext.Net.Cluster 5.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.13.0">DotNext.AspNetCore.Cluster 5.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/0.4.0">DotNext.MaintenanceServices 0.4.0</a>
* Added [gc refresh-mem-limit](https://learn.microsoft.com/en-us/dotnet/api/system.gc.refreshmemorylimit) maintenance command
* Updated dependencies

# 08-19-2024
<a href="https://www.nuget.org/packages/dotnext/5.12.1">DotNext 5.12.1</a>
* Added support of static field references to `DotNext.Runtime.ValueReference<T>` data type

<a href="https://www.nuget.org/packages/dotnext.threading/5.12.1">DotNext.Threading 5.12.1</a>
* Smallish performance improvements of `RandomAccessCache<TKey, TValue>` class

# 08-13-2024
<a href="https://www.nuget.org/packages/dotnext/5.12.0">DotNext 5.12.0</a>
* Added `DotNext.Runtime.ValueReference<T>` data type that allows to obtain async-friendly managed pointer to the field
* Deprecation of `ConcurrentCache<TKey, TValue>` in favor of `RandomAccessCache<TKey, TValue>`

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.12.0">DotNext.Metaprogramming 5.12.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.12.0">DotNext.Unsafe 5.12.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.12.0">DotNext.Threading 5.12.0</a>
* Introduced async-friendly `RandomAccessCache<TKey, TValue>` as a replacement for deprecated `ConcurrentCache<TKey, TValue>`. It uses [SIEVE](https://cachemon.github.io/SIEVE-website/) eviction algorithm.

<a href="https://www.nuget.org/packages/dotnext.io/5.12.0">DotNext.IO 5.12.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.12.0">DotNext.Net.Cluster 5.12.0</a>
* Fixed cancellation of `PersistentState` async methods

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.12.0">DotNext.AspNetCore.Cluster 5.12.0</a>
* Fixed cancellation of `PersistentState` async methods

# 08-01-2024
<a href="https://www.nuget.org/packages/dotnext/5.11.0">DotNext 5.11.0</a>
* Added `DotNext.Threading.Epoch` for epoch-based reclamation
* Fixed one-shot FNV1a hashing method
* Fixed [248](https://github.com/dotnet/dotNext/issues/248)
* Minor performance improvements

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.11.0">DotNext.Metaprogramming 5.11.0</a>
* Minor performance improvements
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.11.0">DotNext.Unsafe 5.11.0</a>
* Minor performance improvements
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.11.0">DotNext.Threading 5.11.0</a>
* Fixed `AsyncSharedLock.Downgrade` behavior, so it can be used to release a weak lock
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/5.11.0">DotNext.IO 5.11.0</a>
* Minor performance improvements
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.11.0">DotNext.Net.Cluster 5.11.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.11.0">DotNext.AspNetCore.Cluster 5.11.0</a>
* Updated dependencies

# 07-15-2024
<a href="https://www.nuget.org/packages/dotnext/5.8.0">DotNext 5.8.0</a>
* Added `FirstOrNone` and `LastOrNone` extension methods back from .NEXT 4.x as requested in [247](https://github.com/dotnet/dotNext/issues/247)

<a href="https://www.nuget.org/packages/dotnext.threading/5.10.0">DotNext.Threading 5.10.0</a>
* Added `TaskQueue<T>` class
* Added `Completion` optional property to [TaskCompletionPipe&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Threading.Tasks.TaskCompletionPipe-1.html) that allows to synchronize on full completion of the pipe
* Added one-shot static methods to [TaskCompletionPipe](https://dotnet.github.io/dotNext/api/DotNext.Threading.Tasks.TaskCompletionPipe.html) to take `IAsyncEnumerable<T>` over tasks as they complete

# 07-09-2024
<a href="https://www.nuget.org/packages/dotnext.io/5.9.0">DotNext.IO 5.7.1</a>
* Improved performance of `FileWriter` in some corner cases

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.7.3">DotNext.Net.Cluster 5.7.3</a>
* Fixed [244](https://github.com/dotnet/dotNext/issues/244)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.7.3">DotNext.AspNetCore.Cluster 5.7.3</a>
* Fixed [244](https://github.com/dotnet/dotNext/issues/244)

# 07-01-2024
<a href="https://www.nuget.org/packages/dotnext.threading/5.9.0">DotNext.Threading 5.9.0</a>
* Added `WaitAnyAsync` overload method to wait on a group of cancellation tokens that supports interruption

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.7.2">DotNext.Net.Cluster 5.7.2</a>
* Fixed [244](https://github.com/dotnet/dotNext/issues/244)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.7.2">DotNext.AspNetCore.Cluster 5.7.2</a>
* Fixed [244](https://github.com/dotnet/dotNext/issues/244)

# 06-26-2024
<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.7.1">DotNext.Net.Cluster 5.7.1</a>
* Improved reliability of disk I/O for the new WAL binary format

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.7.1">DotNext.AspNetCore.Cluster 5.7.1</a>
* Improved reliability of disk I/O for the new WAL binary format

# 06-25-2024
<a href="https://www.nuget.org/packages/dotnext.threading/5.8.0">DotNext.Threading 5.8.0</a>
* Introduced `WaitAnyAsync` method to wait on a group of cancellation tokens
* Added cancellation support for `WaitAsync` extension method for [WaitHandle](https://learn.microsoft.com/en-us/dotnet/api/system.threading.waithandle) class

# 06-20-2024
<a href="https://www.nuget.org/packages/dotnext/5.7.0">DotNext 5.7.0</a>
* `Timestamp.ElapsedTicks` returns a value that is always greater than zero
* Fixed hidden copies of value types caused by **in** modifier
* Added support of [TimeProvider](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider) to `Timestamp` and `Timeout` types

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.7.0">DotNext.Metaprogramming 5.7.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.7.0">DotNext.Unsafe 5.7.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.7.0">DotNext.Threading 5.7.0</a>
* Fixed [241](https://github.com/dotnet/dotNext/issues/241)
* Introduced API for implementing leases, see `DotNext.Threading.Leases` namespace

<a href="https://www.nuget.org/packages/dotnext.io/5.7.0">DotNext.IO 5.7.0</a>
* Various performance improvements

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.7.0">DotNext.Net.Cluster 5.7.0</a>
* Fixed [242](https://github.com/dotnet/dotNext/issues/242)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.7.0">DotNext.AspNetCore.Cluster 5.7.0</a>
* Fixed [242](https://github.com/dotnet/dotNext/issues/242)

# 06-07-2024
<a href="https://www.nuget.org/packages/dotnext.threading/5.5.0">DotNext.Metaprogramming 5.5.0</a>
* Fixed [240](https://github.com/dotnet/dotNext/issues/240)

# 05-30-2024
<a href="https://www.nuget.org/packages/dotnext.threading/5.4.1">DotNext.Metaprogramming 5.4.1</a>
* Smallish performance improvements for all synchronization primitives

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.6.0">DotNext.Net.Cluster 5.6.0</a>
* Added support of custom data to be passed to `PersistentState.ApplyAsync` method through WAL processing pipeline

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.6.0">DotNext.AspNetCore.Cluster 5.6.0</a>
* Updated dependencies

# 05-21-2024
<a href="https://www.nuget.org/packages/dotnext.threading/5.4.0">DotNext.Metaprogramming 5.4.0</a>
* Smallish performance improvements of `IndexPool` instance methods
* Added ability to instantiate empty `IndexPool`

# 05-15-2024
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.3.1">DotNext.Metaprogramming 5.3.1</a>
* Fixed [234](https://github.com/dotnet/dotNext/issues/234)
* Updated dependencies

# 05-10-2024
<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.5.1">DotNext.Net.Cluster 5.5.1</a>
* Fixed behavior of `IRaftCluster.ConsensusToken` when a node is in **standby** mode

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.5.1">DotNext.AspNetCore.Cluster 5.5.1</a>
* Updated dependencies

# 05-05-2024
<a href="https://www.nuget.org/packages/dotnext.threading/5.3.1">DotNext.Threading 5.3.1</a>
* Fixed race condition caused by `LinkedCancellationTokenSource.CancellationOrigin` property that leads to incorrectly returned cancellation token

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.5.0">DotNext.Net.Cluster 5.5.0</a>
* Introduced `IRaftCluster.WaitForLeadershipAsync` method that waits for the local node to be elected as a leader of the cluster
* Fixed [233](https://github.com/dotnet/dotNext/issues/233)
* Fixed correctness of appending no-op entry by a leader used as a write barrier

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.5.0">DotNext.AspNetCore.Cluster 5.5.0</a>
* Updated dependencies

# 04-20-2024
<a href="https://www.nuget.org/packages/dotnext.io/5.4.0">DotNext.IO 5.4.0</a>
* Added `FileWriter.WrittenBuffer` property

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.4.0">DotNext.Net.Cluster 5.4.0</a>
* Changed binary file format for WAL for more efficient I/O. A new format is incompatible with all previous versions. To enable legacy format, set `PersistentState.Options.UseLegacyBinaryFormat` property to **true**
* Introduced a new experimental binary format for WAL based on sparse files. Can be enabled with `PersistentState.Options.MaxLogEntrySize` property

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.4.0">DotNext.AspNetCore.Cluster 5.4.0</a>
* Updated dependencies

# 03-20-2024
<a href="https://www.nuget.org/packages/dotnext/5.3.1">DotNext 5.3.1</a>
* Provided support of thread-local storage for `StreamSource.AsSharedStream`
* Remove type cast for `Func.Constant` static method

# 03-19-2024
<a href="https://www.nuget.org/packages/dotnext/5.3.0">DotNext 5.3.0</a>
* Added `StreamSource.AsSharedStream` extension method that allows to obtain read-only stream over memory block which position is local for each consuming async flow or thread. In other words, the stream can be shared between async flows for independent reads.

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.3.0">DotNext.Metaprogramming 5.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.3.0">DotNext.Unsafe 5.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.3.0">DotNext.Threading 5.3.0</a>
* Improved performance of `IndexPool.Take` method

<a href="https://www.nuget.org/packages/dotnext.io/5.3.0">DotNext.IO 5.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.3.0">DotNext.Net.Cluster 5.3.0</a>
* Smallish performance improvements of WAL

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.3.0">DotNext.AspNetCore.Cluster 5.3.0</a>
* Smallish performance improvements of WAL

# 03-08-2024
<a href="https://www.nuget.org/packages/dotnext/5.2.0">DotNext 5.2.0</a>
* Added `Number.IsPrime` static method that allows to check whether the specified number is a prime number
* Fixed AOT compatibility issues

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.2.0">DotNext.Metaprogramming 5.2.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.2.0">DotNext.Unsafe 5.2.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.2.0">DotNext.Threading 5.2.0</a>
* Added specialized `IndexPool` data type that can be useful for implementing fast object pools

<a href="https://www.nuget.org/packages/dotnext.io/5.2.0">DotNext.IO 5.2.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.2.0">DotNext.Net.Cluster 5.2.0</a>
* Fixed [226](https://github.com/dotnet/dotNext/issues/226)
* Fixed [221](https://github.com/dotnet/dotNext/issues/221)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.2.0">DotNext.AspNetCore.Cluster 5.2.0</a>
* Fixed [226](https://github.com/dotnet/dotNext/issues/226)
* Fixed [221](https://github.com/dotnet/dotNext/issues/221)

# 02-28-2024
<a href="https://www.nuget.org/packages/dotnext/5.1.0">DotNext 5.1.0</a>
* Added `Span.Advance<T>` extension method for spans
* `CollectionType.GetItemType` now correctly recognizes enumerable pattern even if target type doesn't implement `IEnumerable<T>`

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.1.0">DotNext.Metaprogramming 5.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.1.0">DotNext.Unsafe 5.1.0</a>
* Added `UnmanagedMemory.AsMemory` static method that allows to wrap unmanaged pointer into [Memory&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.memory-1)

<a href="https://www.nuget.org/packages/dotnext.threading/5.1.0">DotNext.Threading 5.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/5.1.0">DotNext.IO 5.1.0</a>
* Merged [225](https://github.com/dotnet/dotNext/pull/225)
* Added `AsUnbufferedStream` extension method for [SafeFileHandle](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.safehandles.safefilehandle) class

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.1.0">DotNext.Net.Cluster 5.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.1.0">DotNext.AspNetCore.Cluster 5.1.0</a>
* Updated dependencies

# 02-25-2024
<a href="https://www.nuget.org/packages/dotnext/5.0.3">DotNext 5.0.3</a>
* Fixed behavior to no-op when `GCLatencyModeScope` is initialized to default

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.0.3">DotNext.Metaprogramming 5.0.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.0.3">DotNext.Unsafe 5.0.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.0.3">DotNext.Threading 5.0.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/5.0.3">DotNext.IO 5.0.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.0.3">DotNext.Net.Cluster 5.0.3</a>
* Attempt to fix [221](https://github.com/dotnet/dotNext/issues/221)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.0.3">DotNext.AspNetCore.Cluster 5.0.3</a>
* Attempt to fix [221](https://github.com/dotnet/dotNext/issues/221)

# 02-17-2024
<a href="https://www.nuget.org/packages/dotnext/5.0.2">DotNext 5.0.2</a>
* Fixed XML docs

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.0.2">DotNext.Metaprogramming 5.0.2</a>
* Fixed [223](https://github.com/dotnet/dotNext/issues/223)

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.0.2">DotNext.Unsafe 5.0.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.0.2">DotNext.Threading 5.0.2</a>
* Added correct validation for maximum possible timeout for all `WaitAsync` methods

<a href="https://www.nuget.org/packages/dotnext.io/5.0.2">DotNext.IO 5.0.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.0.2">DotNext.Net.Cluster 5.0.2</a>
* Prevent indexing of WAL files on Windows

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.0.2">DotNext.AspNetCore.Cluster 5.0.2</a>
* Updated dependencies

# 01-23-2024
<a href="https://www.nuget.org/packages/dotnext/5.0.1">DotNext 5.0.1</a>
* Smallish performance improvements of dynamic buffers

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.0.1">DotNext.Metaprogramming 5.0.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.0.1">DotNext.Unsafe 5.0.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.0.1">DotNext.Threading 5.0.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/5.0.1">DotNext.IO 5.0.1</a>
* Improved performance of `FileWriter` and `FileBufferingWriter` classes by utilizing Scatter/Gather IO
* Reduced memory allocations required by async methods of `FileWriter` and `FileBufferingWriter` classes
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.0.1">DotNext.Net.Cluster 5.0.1</a>
* Improved IO performance of Persistent WAL due to related improvements in DotNext.IO library
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.0.1">DotNext.AspNetCore.Cluster 5.0.1</a>
* Updated dependencies

# 01-14-2024
.NEXT 5.0.0 has been released! The primary goal of the new release is migration to .NET 8 to fully utilize its features such as [Generic Math](https://learn.microsoft.com/en-us/dotnet/standard/generics/math) and static abstract interface members. 5.x is not fully backward compatible with 4.x because of breaking changes in the API. Most of changes done in DotNext, DotNext.IO, and DotNext.Unsafe libraries. UDP transport for Raft is completely removed in favor of existing TCP implementation. There is a plan to implement multiplexed TCP connection and Raft sharding. New features:
* Numeric ranges for LINQ. Thanks to Generic Math
* Little-endian and big-endian readers/writer for various buffer types. Again thanks to Generic Math
* UTF-8 formatting support for various buffer types

DotNext.Reflection library is deprecated and no longer maintained.

# 11-19-2023
<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.15.3">DotNext.Net.Cluster 4.15.3</a>
* `SustainedLowLatency` GC mode is now applied for heartbeats round initiated by heartbeat timeout. A round forced by replication programmatically doesn't set this mode that allows GC to be more intrusive. This trade-off provides better balance between memory consumption and replication time
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.15.3">DotNext.AspNetCore.Cluster 4.15.3</a>
* Updated dependencies

# 11-16-2023
<a href="https://www.nuget.org/packages/dotnext/4.15.2">DotNext 4.15.2</a>
* Reduced memory allocation caused by async methods using [SpawningAsyncTaskMethodBuilder](https://dotnet.github.io/dotNext/api/DotNext.Runtime.CompilerServices.SpawningAsyncTaskMethodBuilder.html) state machine builder
* Fixed [204](https://github.com/dotnet/dotNext/issues/204)

<a href="https://www.nuget.org/packages/dotnext.threading/4.15.2">DotNext.Threading 4.15.2</a>
* Fixed [205](https://github.com/dotnet/dotNext/issues/205)
* `AsyncCountdownEvent.Reset` now throws `PendingTaskInterruptedException` on every caller suspended by `WaitAsync`

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.15.2">DotNext.Net.Cluster 4.15.2</a>
* Raft performance: reduced memory allocation caused by heartbeat round
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.15.2">DotNext.AspNetCore.Cluster 4.15.2</a>
* Updated dependencies

# 11-13-2023
<a href="https://www.nuget.org/packages/dotnext/4.15.1">DotNext 4.15.1</a>
* Merged PR [203](https://github.com/dotnet/dotNext/pull/203)

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.15.0">DotNext.Net.Cluster 4.15.0</a>
* Raft performance: improved throughput of `IRaftCluster.ReplicateAsync` method when cluster minority is not accessible (faulty node). Now the leader waits for replication from majority of nodes instead of all nodes
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.15.0">DotNext.AspNetCore.Cluster 4.15.0</a>
* Updated dependencies

# 11-08-2023
<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.14.5">DotNext.Net.Cluster 4.14.5</a>
* Fixed leader lease renewal
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.14.5">DotNext.AspNetCore.Cluster 4.14.5</a>
* Updated dependencies

# 10-29-2023
<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.14.4">DotNext.Net.Cluster 4.14.4</a>
* Clarified exception type when `AddMemberAsync` or `RemoveMemberAsync` is called on Follower node
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.14.4">DotNext.AspNetCore.Cluster 4.14.4</a>
* Updated dependencies

# 10-25-2023
<a href="https://www.nuget.org/packages/dotnext/4.15.0">DotNext 4.15.0</a>
* Optimized performance of `PooledArrayBufferWriter<T>` and `PooledBufferWriter<T>` classes as a result of discussion in [192](https://github.com/dotnet/dotNext/issues/192)
* Added `Span.Swap` and `Span.Move` extension methods
* Updated dependencies

# 09-27-2023
<a href="https://www.nuget.org/packages/dotnext.io/4.15.0">DotNext.IO 4.15.0</a>
* Added fast UTF-8 decoding for [streams](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream) and [pipes](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipereader)
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.14.3">DotNext.Net.Cluster 4.14.3</a>
* Deprecation of `partitioning` configuration property
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.14.3">DotNext.AspNetCore.Cluster 4.14.3</a>
* Updated dependencies

# 08-25-2023
<a href="https://www.nuget.org/packages/dotnext.threading/4.14.2">DotNext.Threading 4.14.2</a>
* Removed redundant memory barrier from async locks and reduced size of wait nodes
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.14.2">DotNext.Net.Cluster 4.14.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.14.2">DotNext.AspNetCore.Cluster 4.14.2</a>
* Updated dependencies

# 08-23-2023
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.15.0">DotNext.Metaprogramming 4.15.0</a>
* Fixed broken compatibility introduced in C# 10 at language level. See [189](https://github.com/dotnet/dotNext/discussions/189) discussion. The change provides backward compatibility at source code level, but it's binary compatible. This means that all you need is to rebuild your project without any code changes
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.14.1">DotNext.Threading 4.14.1</a>
* Optimized `AsyncEventHub` and `Scheduler` performance
* Fixed regression: reuse `CancellationTokenSource` used for timeout tracking by all async locks

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.14.1">DotNext.Net.Cluster 4.14.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.14.1">DotNext.AspNetCore.Cluster 4.14.1</a>
* Updated dependencies

# 08-16-2023
<a href="https://www.nuget.org/packages/dotnext/4.14.0">DotNext 4.14.0</a>
* Added implicit conversion from [BoxedValue&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Runtime.BoxedValue-1.html) to [ValueType](https://learn.microsoft.com/en-us/dotnet/api/system.valuetype)
* [SpawningAsyncTaskMethodBuilder](https://dotnet.github.io/dotNext/api/DotNext.Runtime.CompilerServices.SpawningAsyncTaskMethodBuilder.html) reuses the same .NET internals as [AsyncTaskMethodBuilder](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.asynctaskmethodbuilder)
* Added non-generic `TypeMap` and `ConcurrentTypeMap` implementations acting as a set in contrast to existing generic counterparts
* Introduced `Optional<T>.ValueOrDefault` property which is linked with existing `HasValue` property be means of nullability analysis
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.14.0">DotNext.Metaprogramming 4.14.0</a>
* Fixed [187](https://github.com/dotnet/dotNext/issues/187)
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.14.0">DotNext.Unsafe 4.14.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.14.0">DotNext.Threading 4.14.0</a>
* Fixed scheduling of continuation if it is represented by async state machine
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.14.0">DotNext.IO 4.14.0</a>
* Fixed abstract representation of Write-Ahead Log
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.14.0">DotNext.Net.Cluster 4.14.0</a>
* Fixed [185](https://github.com/dotnet/dotNext/issues/185)
* Fixed [186](https://github.com/dotnet/dotNext/issues/186)
* Reduced memory allocations by Raft leader
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.14.0">DotNext.AspNetCore.Cluster 4.14.0</a>
* Fixed [185](https://github.com/dotnet/dotNext/issues/185)
* Updated dependencies

# 08-02-2023
<a href="https://www.nuget.org/packages/dotnext/4.13.1">DotNext 4.13.1</a>
* Removed memory allocation inside of `Sequence.AddAll` extension method
* Smallish performance improvements of `SingletonList` value type

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.13.1">DotNext.Metaprogramming 4.13.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.13.1">DotNext.Unsafe 4.13.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.13.1">DotNext.Threading 4.13.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.13.1">DotNext.IO 4.13.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.13.1">DotNext.Net.Cluster 4.13.1</a>
* Fixed [184](https://github.com/dotnet/dotNext/issues/184)
* Reduced memory allocation when reading single log entry from WAL
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.13.1">DotNext.AspNetCore.Cluster 4.13.1</a>
* Fixed [184](https://github.com/dotnet/dotNext/issues/184)
* Updated dependencies

# 07-13-2023
<a href="https://www.nuget.org/packages/dotnext/4.13.0">DotNext 4.13.0</a>
* Added of `AlignOf` intrinsic method that allows to obtain alignment requirements for the specified type
* `ConcurrentCache` recognizes types with atomic write semantics more precisely that allows to avoid memory allocations for certain generic arguments
* Introduced `TrimLength` overloaded extension method for [Span&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.span-1) data type that allows to retrieve the trimmed part of the span

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.13.0">DotNext.Metaprogramming 4.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.13.0">DotNext.Unsafe 4.13.0</a>
* `Pointer<T>.IsAligned` property is unmarked as _obsolete_ because it is possible to determine memory alignment correctly
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.13.0">DotNext.Threading 4.13.0</a>
* Fixed [183](https://github.com/dotnet/dotNext/issues/183)
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.13.0">DotNext.IO 4.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.13.0">DotNext.Net.Cluster 4.13.0</a>
* Fixed cancellation of some async methods exposed by Raft implementation and WAL
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.13.0">DotNext.AspNetCore.Cluster 4.13.0</a>
* Updated dependencies

# 07-02-2023
<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.12.5">DotNext.Net.Cluster 4.12.5</a>
* Improved Raft metrics over [System.Diagnostics.Metrics](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics)
* Improved performance of AppendEntries consensus message

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.12.5">DotNext.AspNetCore.Cluster 4.12.5</a>
* Improved Raft metrics over [System.Diagnostics.Metrics](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics)
* Updated dependencies

# 06-19-2023
<a href="https://www.nuget.org/packages/dotnext/4.12.4">DotNext 4.12.4</a>
* Fixed: sometimes `ConcurrentCache.TakeSnapshot` method may return evicted key/value pairs 

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.12.4">DotNext.Metaprogramming 4.12.4</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.12.4">DotNext.Unsafe 4.12.4</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.12.4">DotNext.Threading 4.12.4</a>
* Deprecation of `AsyncLock.TryAcquireAsync(CancellationToken)` overload
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.12.4">DotNext.IO 4.12.4</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.12.4">DotNext.Net.Cluster 4.12.4</a>
* Perf: avoid Pre-Vote phase in case of concurrency between inbound Vote request and transition to Candidate state
* Optimized memory consumption by `RaftCluster` implementation
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.12.4">DotNext.AspNetCore.Cluster 4.12.4</a>
* Updated dependencies

# 06-08-2023
<a href="https://www.nuget.org/packages/dotnext/4.12.3">DotNext 4.12.3</a>
* Fixed concurrency between add and update operations of [ConcurrentCache](https://dotnet.github.io/dotNext/api/DotNext.Runtime.Caching.ConcurrentCache-2.html) class

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.12.3">DotNext.Metaprogramming 4.12.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.12.3">DotNext.Unsafe 4.12.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.12.3">DotNext.Threading 4.12.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.12.3">DotNext.IO 4.12.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.12.3">DotNext.Net.Cluster 4.12.3</a>
* Fixed [173](https://github.com/dotnet/dotNext/issues/173)
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.12.3">DotNext.AspNetCore.Cluster 4.12.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/0.2.0">DotNext.MaintenanceServices 0.2.0</a>
* Make probe timeout optional
* Updated dependencies

# 06-07-2023
<a href="https://www.nuget.org/packages/dotnext/4.12.2">DotNext 4.12.2</a>
* Fixed [169](https://github.com/dotnet/dotNext/issues/169)
* Fixed concurrency between add and update operations of [ConcurrentCache](https://dotnet.github.io/dotNext/api/DotNext.Runtime.Caching.ConcurrentCache-2.html) class

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.12.2">DotNext.Net.Cluster 4.12.2</a>
* Fixed [165](https://github.com/dotnet/dotNext/issues/165)
* Merged [170](https://github.com/dotnet/dotNext/pull/170)
* Fixed [168](https://github.com/dotnet/dotNext/issues/168)
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.12.2">DotNext.AspNetCore.Cluster 4.12.2</a>
* Updated dependencies

# 05-29-2023
<a href="https://www.nuget.org/packages/dotnext/4.12.1">DotNext 4.12.1</a>
* Fixed [162](https://github.com/dotnet/dotNext/issues/162)
* Fixed other race conditions in `ConcurrentCache`
* Improved performance of `BitVector` methods

# 03-22-2023
<a href="https://www.nuget.org/packages/dotnext/4.12.0">DotNext 4.12.0</a>
* Performance improvements of interpolated string handlers

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.12.0">DotNext.Metaprogramming 4.12.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.12.0">DotNext.Unsafe 4.12.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.12.0">DotNext.Threading 4.12.0</a>
* Reduced complexity of `QueuedSynchronizer` class internals
* Fixed rare concurrency issues when multiple consumers trying to get task result from `ValueTaskCompletionSource`
* Reduced number of work items submitted by async locks internally
* Provided `ManualResetCompletionSource.Cleanup` protected virtual method for custom cleanup operations
* Heavily reduced monitor lock contention that can be caused by `ValueTaskCompletionSource` or `ValueTaskCompletionSource<T>`
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.12.0">DotNext.IO 4.12.0</a>
* Performance improvements of interpolated string handlers
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.12.0">DotNext.Net.Cluster 4.12.0</a>
* Fixed initialization logic of [PhiAccrualFailureDetector](https://dotnet.github.io/dotNext/api/DotNext.Diagnostics.PhiAccrualFailureDetector.html)
* Partially fixed [153](https://github.com/dotnet/dotNext/issues/153). Optionally, the node which initial state cannot be recognized by failure detector (e.g., node never responds) is treated as dead
* Fixed [155](https://github.com/dotnet/dotNext/issues/155)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.12.0">DotNext.AspNetCore.Cluster 4.12.0</a>
* Fixed initialization logic of [PhiAccrualFailureDetector](https://dotnet.github.io/dotNext/api/DotNext.Diagnostics.PhiAccrualFailureDetector.html)
* Partially fixed [153](https://github.com/dotnet/dotNext/issues/153). Optionally, the node which initial state cannot be recognized by failure detector (e.g., node never responds) is treated as dead
* Fixed [155](https://github.com/dotnet/dotNext/issues/155)

# 03-07-2023
<a href="https://www.nuget.org/packages/dotnext/4.11.0">DotNext 4.11.0</a>
* Adoption of [System.Diagnostics.Metrics](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics) instruments to provide compatibility with [OpenTelemetry](https://opentelemetry.io/)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.11.0">DotNext.Metaprogramming 4.11.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.11.0">DotNext.Unsafe 4.11.0</a>
* Added methods to `Pointer<T>` data type for unaligned memory access

<a href="https://www.nuget.org/packages/dotnext.threading/4.11.0">DotNext.Threading 4.11.0</a>
* Added special methods to [AsyncTrigger](https://dotnet.github.io/dotNext/api/DotNext.Threading.AsyncTrigger.html) class to implement asynchronous _spin-wait_
* [AsyncTrigger&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Threading.AsyncTrigger-1.html) is deprecated in favor of `QueuedSynchronizer<T>`
* Introduced `QueuedSynchronizer<T>` class that provides low-level infrastructure for writing custom synchronization primitives
* Adoption of [System.Diagnostics.Metrics](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics) instruments to provide compatibility with [OpenTelemetry](https://opentelemetry.io/)

<a href="https://www.nuget.org/packages/dotnext.io/4.11.0">DotNext.IO 4.11.0</a>
* Optimized memory allocations caused by `FileBufferingWriter` class
* Added `DotNext.Text.Json.JsonSerializable<T>` wrapper acting as a bridge between [binary DTO](https://dotnet.github.io/dotNext/api/DotNext.Runtime.Serialization.ISerializable-1.html) and JSON serialization infrastructure from .NET BCL
* Adoption of [System.Diagnostics.Metrics](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics) instruments to provide compatibility with [OpenTelemetry](https://opentelemetry.io/)
* Reduced API surface requiring [RequiresPreviewFeatures](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.versioning.requirespreviewfeaturesattribute) attribute

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.11.0">DotNext.Net.Cluster 4.11.0</a>
* Adoption of [System.Diagnostics.Metrics](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics) instruments to provide compatibility with [OpenTelemetry](https://opentelemetry.io/)
* Fixed [151](https://github.com/dotnet/dotNext/issues/151)
* Fixed [153](https://github.com/dotnet/dotNext/issues/153)
* Raft: reduced memory allocations when the node is Leader
* Raft: fixed correctness of `ForceReplication` method when it is used as a write barrier in a distributed environment
* Reduced API surface requiring [RequiresPreviewFeatures](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.versioning.requirespreviewfeaturesattribute) attribute

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.11.0">DotNext.AspNetCore.Cluster 4.11.0</a>
* Adoption of [System.Diagnostics.Metrics](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.metrics) instruments to provide compatibility with [OpenTelemetry](https://opentelemetry.io/)

# 02-02-2023
Starting from the current release, `DotNext.Reflection` library is no longer published on regular basis. See [this post](https://github.com/dotnet/dotNext/discussions/142) for more information.

<a href="https://www.nuget.org/packages/dotnext/4.10.0">DotNext 4.10.0</a>
* Added API discussed and proposed in [143](https://github.com/dotnet/dotNext/issues/143). The requested features are implemented as `DotNext.Buffers.Binary.BinaryTransformations` class.

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.10.0">DotNext.Metaprogramming 4.10.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.10.0">DotNext.Unsafe 4.10.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.10.0">DotNext.Threading 4.10.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.10.0">DotNext.IO 4.10.0</a>
* Optimized memory allocations caused by `FileBufferingWriter` class
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.10.0">DotNext.Net.Cluster 4.10.0</a>
* Fixed [146](https://github.com/dotnet/dotNext/issues/146)
* Fixed [147](https://github.com/dotnet/dotNext/issues/147)
* Reduced memory allocations caused by the implementation of the leader lease
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.10.0">DotNext.AspNetCore.Cluster 4.10.0</a>
* Fixed [146](https://github.com/dotnet/dotNext/issues/146)
* Updated dependencies

# 01-16-2023
<a href="https://www.nuget.org/packages/dotnext/4.9.0">DotNext 4.9.0</a>
* Introduced `SpawningAsyncTaskMethodBuilder` that can be used in combination with [AsyncMethodBuilderAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.asyncmethodbuilderattribute) to force execution of async method in parallel with the current flow
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.9.0">DotNext.Metaprogramming 4.9.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.9.0">DotNext.Reflection 4.9.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.9.0">DotNext.Unsafe 4.9.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.9.0">DotNext.Threading 4.9.0</a>
* Smallish performance improvements of async locks
* Fixed upgrade lock acquisition using `AsyncLock` value type
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.9.0">DotNext.IO 4.9.0</a>
* Use `ValueTask` caching for hot execution paths
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.9.0">DotNext.Net.Cluster 4.9.0</a>
* Reduced memory allocation caused by WAL and TCP/UDP transports
* Reduced managed heap fragmentation
* Added support of [DNS](https://learn.microsoft.com/en-us/dotnet/api/system.net.dnsendpoint) and [Unix Domain Socket](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.unixdomainsocketendpoint) addresses of cluster nodes for better compatibility with containers
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.9.0">DotNext.AspNetCore.Cluster 4.9.0</a>
* HTTP transport: optimized memory allocations
* Updated dependencies

# 12-23-2022
<a href="https://www.nuget.org/packages/dotnext.threading/4.8.3">DotNext.Threading 4.8.3</a>
* Smallish performance improvements of async locks
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.8.3">DotNext.Net.Cluster 4.8.3</a>
* TCP/UDP Raft transport: optimized memory allocations
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.8.3">DotNext.AspNetCore.Cluster 4.8.3</a>
* HTTP transport: optimized memory allocations
* Updated dependencies

# 12-19-2022
<a href="https://www.nuget.org/packages/dotnext.threading/4.8.2">DotNext.Threading 4.8.2</a>
* Optimized memory allocations produced by instances of `TaskCompletionPipe<T>` class
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.8.2">DotNext.IO 4.8.2</a>
* Reduced memory allocations caused by instances of `FileReader` and `FileWriter` classes
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.8.2">DotNext.Net.Cluster 4.8.2</a>
* Optimized memory allocations produced by persistent WAL and Raft algorithm implementation
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.8.2">DotNext.AspNetCore.Cluster 4.8.2</a>
* Updated dependencies

# 12-15-2022
<a href="https://www.nuget.org/packages/dotnext/4.8.1">DotNext 4.8.1</a>
* Improved quality and performance of random string generator exposed as `RandomExtensions.NextString` extension methods ([138](https://github.com/dotnet/dotNext/issues/138))
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.8.1">DotNext.Metaprogramming 4.8.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.8.1">DotNext.Reflection 4.8.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.8.1">DotNext.Unsafe 4.8.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.8.1">DotNext.Threading 4.8.1</a>
* Fixed critical bug [136](https://github.com/dotnet/dotNext/issues/136) that prevents reentrant reads from persistent channel
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.8.1">DotNext.IO 4.8.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.8.1">DotNext.Net.Cluster 4.8.1</a>
* Fixed [139](https://github.com/dotnet/dotNext/issues/139)
* Fixed calculation of Phi performed by Phi Accrual Failure Detector. The bug leads to false positive detection
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.8.1">DotNext.AspNetCore.Cluster 4.8.1</a>
* Updated dependencies

# 12-06-2022
<a href="https://www.nuget.org/packages/dotnext/4.8.0">DotNext 4.8.0</a>
* Added **scoped** keyword to necessary buffer types and extension methods for better compatibility with C# 11
* Added [Builder Pattern](https://en.wikipedia.org/wiki/Builder_pattern) concept as an interface
* Added extra properties to [Timestamp](https://dotnet.github.io/dotNext/api/DotNext.Diagnostics.Timestamp.html) value type for precise measurements
* Introduced additional methods for reading data from [sparse buffer](https://dotnet.github.io/dotNext/api/DotNext.Buffers.SparseBufferWriter-1.html) with help of [SequencePosition](https://learn.microsoft.com/en-us/dotnet/api/system.sequenceposition) cursor
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.8.0">DotNext.Metaprogramming 4.8.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.8.0">DotNext.Reflection 4.8.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.8.0">DotNext.Unsafe 4.8.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.8.0">DotNext.Threading 4.8.0</a>
* `TaskCompletionPipe<T>` doesn't require capacity anymore
* Fix: potential consumer hangs when a number consumers is larger than number of pending tasks
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.8.0">DotNext.IO 4.8.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.8.0">DotNext.Net.Cluster 4.8.0</a>
* Added automatic removal of unresponsive nodes from Raft cluster using Failure Detector
* Added implementation of Phi Accrual Failure Detector
* Added ability to turn cluster node into Standby mode and back to normal mode (see [discussion](https://github.com/dotnet/dotNext/discussions/134))
* Raft functional extensions are grouped as a set of interfaces located in a new `DotNext.Net.Cluster.Consensus.Raft.Extensions` namespace
* Fixed cluster recovery when cold start mode is used ([135](https://github.com/dotnet/dotNext/issues/135))
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.8.0">DotNext.AspNetCore.Cluster 4.8.0</a>
* Added automatic removal of unresponsive nodes from Raft cluster using Failure Detector registered in DI
* Raft functional extensions are available for query through DI as interfaces
* Updated dependencies

# 11-08-2022
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.7.5">DotNext.Metaprogramming 4.7.5</a>
* Fixed [127](https://github.com/dotnet/dotNext/issues/127)

# 10-30-2022
<a href="https://www.nuget.org/packages/dotnext/4.7.4">DotNext 4.7.4</a>
* Fixed [126](https://github.com/dotnet/dotNext/issues/126)
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.7.4">DotNext.Metaprogramming 4.7.4</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.7.4">DotNext.Reflection 4.7.4</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.7.4">DotNext.Unsafe 4.7.4</a>
* Removed redundant type cast in `Pointer<T>` value type
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.7.4">DotNext.Threading 4.7.4</a>
* `Scheduler.DelayedTaskCanceledException` is added to identify graceful cancellation of the scheduled task (when it was canceled without entering the callback)
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.7.4">DotNext.IO 4.7.4</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.7.4">DotNext.Net.Cluster 4.7.4</a>
* Smallish performance improvements when processing command queue in HyParView implementation
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.7.4">DotNext.AspNetCore.Cluster 4.7.4</a>
* Updated dependencies

# 10-22-2022
<a href="https://www.nuget.org/packages/dotnext/4.7.3">DotNext 4.7.3</a>
* Deprecation of [EqualityComparerBuilder&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.EqualityComparerBuilder-1.html) in favor of [C# Records](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.7.3">DotNext.Metaprogramming 4.7.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.7.3">DotNext.Reflection 4.7.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.7.3">DotNext.Unsafe 4.7.3</a>
* Removed redundant type cast in `Pointer<T>` value type
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.7.3">DotNext.Threading 4.7.3</a>
* Fixed parameter name when throwing `ArgumentNullException` in `AsyncLazy<T>` constructor
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.7.3">DotNext.IO 4.7.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.7.3">DotNext.Net.Cluster 4.7.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.7.3">DotNext.AspNetCore.Cluster 4.7.3</a>
* Updated dependencies

# 09-19-2022
<a href="https://www.nuget.org/packages/dotnext/4.7.2">DotNext 4.7.2</a>
* Critical bug fixes for `ConcurrentCache<TKey, TValue>` class: incorrect behavior of LFU policy (wrong sorting order)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.7.2">DotNext.Metaprogramming 4.7.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.7.2">DotNext.Reflection 4.7.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.7.2">DotNext.Unsafe 4.7.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.7.2">DotNext.Threading 4.7.2</a>
* Reduced memory allocation caused by several async lock types
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.7.2">DotNext.IO 4.7.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.7.2">DotNext.Net.Cluster 4.7.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.7.2">DotNext.AspNetCore.Cluster 4.7.2</a>
* Updated dependencies

# 08-24-2022
<a href="https://www.nuget.org/packages/dotnext/4.7.1">DotNext 4.7.1</a>
* Fixed source-level compatibility issues with Roslyn compiler shipped with .NET 6.0.8 (SDK 6.0.400) due to backward incompatible changes in it
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.7.1">DotNext.Metaprogramming 4.7.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.7.1">DotNext.Reflection 4.7.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.7.1">DotNext.Unsafe 4.7.1</a>
* Completed first phase of migration of `Pointer<T>` and related data types to **nint** and **nuint** data types
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.7.1">DotNext.Threading 4.7.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.7.1">DotNext.IO 4.7.1</a>
* Fixed source-level compatibility issues with Roslyn compiler shipped with .NET 6.0.8 (SDK 6.0.400) due to backward incompatible changes in it
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.7.1">DotNext.Net.Cluster 4.7.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.7.1">DotNext.AspNetCore.Cluster 4.7.1</a>
* Updated dependencies

# 08-08-2022
Mac OS is added as a target OS for running tests to track compatibility with this operating system.

<a href="https://www.nuget.org/packages/dotnext/4.7.0">DotNext 4.7.0</a>
* Fixed memory alignment issues
* Added `TaskType.GetIsCompletedGetter` method that allows to obtain [Task.IsCompleted](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.iscompleted) property in the form of closed delegate
* Significantly improved performance of HEX conversion methods with SSSE3 hardware intrinsics
* Introduced `DotNext.Buffers.Text.Hex` class with static methods for efficient conversion to/from hexadecimal representation of binary data with UTF-16 and UTF-8 support
* Introduced `NextChars` extension methods that allows to fill buffer with random characters

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.7.0">DotNext.Metaprogramming 4.7.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.7.0">DotNext.Reflection 4.7.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.7.0">DotNext.Unsafe 4.7.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.7.0">DotNext.Threading 4.7.0</a>
* Reduced memory allocation caused by extension methods declared in [Scheduler](https://dotnet.github.io/dotNext/api/DotNext.Threading.Scheduler.html) class
* Reduced monitor lock contention in async locks
* Added lock stealing methods to some synchronization primitives: `AsyncExclusiveLock`, `AsyncReaderWriterLock`
* Introduced `GetConsumer` extension method for `TaskCompletionPipe<Task<T>>` class that allows to consume task results asynchronously
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.7.0">DotNext.IO 4.7.0</a>
* Removed defensive copies of structs
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.7.0">DotNext.Net.Cluster 4.7.0</a>
* Removed defensive copies of structs
* Adaptation of [Microsoft.AspNetCore.Connections](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.connections) library allows to completely split network transport implementation details from Raft-specific stuff. Now you can implement custom network transport and other network-related concerns much more easier
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.7.0">DotNext.AspNetCore.Cluster 4.7.0</a>
* Removed defensive copies of structs
* Introduced `RaftClusterHttpHost` that provides a way to host multiple Raft clusters in the same process. This feature can be used for implementation of sharding
* Cluster node identification now relies on [UriEndPoint](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.connections.uriendpoint) class instead of [HttpEndPoint](https://dotnet.github.io/dotNext/api/DotNext.Net.Http.HttpEndPoint.html). This allows to use more flexible traffic routing strategies between cluster nodes
* Updated dependencies

# 07-04-2022
<a href="https://www.nuget.org/packages/dotnext/4.6.1">DotNext 4.6.1</a>
* Fixed memory alignment issues

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.6.1">DotNext.Metaprogramming 4.6.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.6.1">DotNext.Reflection 4.6.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.6.1">DotNext.Unsafe 4.6.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.6.1">DotNext.Threading 4.6.1</a>
* Fixed task pooling of some asynchronous methods
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.6.1">DotNext.IO 4.6.1</a>
* Fixed task pooling of some asynchronous methods
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.6.1">DotNext.Net.Cluster 4.6.1</a>
* Fixed task pooling of some asynchronous methods
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.6.1">DotNext.AspNetCore.Cluster 4.6.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/0.1.0">DotNext.MaintenanceServices</a>
* First version of library providing Application Maintenance Inteface via Unix Domain Socket and command-line shell for app administrators
* Added support for [probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/) when running app in Kubernetes

# 06-12-2022
<a href="https://www.nuget.org/packages/dotnext/4.6.0">DotNext 4.6.0</a>
* Added `CharComparer` class that allows to compare single characters in the same way as [StringComparer](https://docs.microsoft.com/en-us/dotnet/api/system.stringcomparer) comparing strings
* Minor performance improvements of static methods declared in [Span](https://dotnet.github.io/dotNext/api/DotNext.Span.html) class
* Added stack manipulation methods to [BufferWriterSlim&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.BufferWriterSlim-1.html) value type
* Introduced [Timeout.Expired](https://dotnet.github.io/dotNext/api/DotNext.Threading.Timeout.html) static property that allows to obtain expired timeout
* Added `LastOrNone` extension methods for various collection types
* Deprecated `DotNext.Runtime.CompilerServices.Shared<T>` value type
* Added a new powerful API for receiving asynchronous notifications from GC (see `DotNext.Runtime.GCNotification` class)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.6.0">DotNext.Metaprogramming 4.6.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.6.0">DotNext.Reflection 4.6.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.6.0">DotNext.Unsafe 4.6.0</a>
* Small performance improvements of unmanaged memory allocator
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.6.0">DotNext.Threading 4.6.0</a>
* Fixed incorrect array bounds check in [AsyncEventHub](https://dotnet.github.io/dotNext/api/DotNext.Threading.AsyncEventHub.html) class
* Optimized completion callback scheduling for all types of asynchronous locks
* Linked token created using `LinkedTokenSourceFactory.LinkTo` extension method now allows to track the originally canceled token
* Added `DotNext.Threading.Scheduler` static class that allows to delay execution of asynchronous tasks
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.6.0">DotNext.IO 4.6.0</a>
* Minor performance improvements of [FileReader](https://dotnet.github.io/dotNext/api/DotNext.IO.FileReader.html) data type
* Reduced memory allocation caused by asynchronous string decoding methods
* Added `IAsyncBinaryReader.TryGetRemainingBytesCount` method that allows to preallocate buffers
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.6.0">DotNext.Net.Cluster 4.6.0</a>
* Optimized read barrier
* Fixed cancellation token propagation in public instance methods declared in [IRaftCluster](https://dotnet.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.IRaftCluster.html) interface
* Introduced a simple framework for rumor spreading for peer-to-peer applications based on [Lamport timestamps](https://en.wikipedia.org/wiki/Lamport_timestamp): `DotNext.Net.Cluster.Messaging.Gossip.RumorTimestamp` and `DotNext.Net.Cluster.Messaging.Gossip.RumorSpreadingManager` classes. Also you can check out modified example of P2P application based on HyParView protocol in `src/examples` folder
* Added compatibility of `DotNext.Net.Cluster.Messaging.JsonMessage<T>` class with JSON Source Generator
* Introduced `DotNext.Net.Cluster.Messaging.IOutputChannel.SendMessageAsync` overload that directly supports data types implementing `DotNext.Runtime.Serialization.ISerializable<T>` interface
* Raft vote and pre-vote requests will be rejected if the requester is not a known cluster member (applicable for all transports: HTTP, UDP, TCP)
* Fixed race conditions between Raft state transitions
* Added `ILeaderLease.Token` property that allows to control linearizable asynchronous reads on the leader node

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.6.0">DotNext.AspNetCore.Cluster 4.6.0</a>
* Added explicit implementation of newly introduced `DotNext.Net.Cluster.Messaging.IOutputChannel.SendMessageAsync` overload
* Updated dependencies

# 05-12-2022
<a href="https://www.nuget.org/packages/dotnext/4.5.0">DotNext 4.5.0</a>
* Added `Base64Encoder.MaxCharsToFlush` constant for convenient allocation of the buffer to be passed to `Base64Encoder.Flush` method
* Added static methods to `Base64Encoder` and `Base64Decoder` types that allow to convert large data asynchronously with low memory consumption
* Added `DotNext.Runtime.CompilerServices.Scope` type that allows to attach callbacks to the lexical scope

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.5.0">DotNext.Metaprogramming 4.5.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.5.0">DotNext.Reflection 4.5.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.5.0">DotNext.Unsafe 4.5.0</a>
* Small performance improvements of unmanaged memory allocator
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.5.0">DotNext.Threading 4.5.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.5.0">DotNext.IO 4.5.0</a>
* Added ability to asynchronously enumerate [streams](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream), [pipes](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipereader), and [text streams](https://docs.microsoft.com/en-us/dotnet/api/system.io.textreader) using async enumerator pattern (`ReadAllAsync` extension method)
* Added implementation of [IAsyncEnumerable&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerable-1) to `FileReader` class

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.5.0">DotNext.Net.Cluster 4.5.0</a>
* Attempt to modify cluster membership concurrently now leads to exception
* Added `ICluster.WaitForLeaderAsync` method for convenience
* Fixed [108](https://github.com/dotnet/dotNext/issues/108)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.5.0">DotNext.AspNetCore.Cluster 4.5.0</a>
* Updated dependencies

# 04-23-2022
<a href="https://www.nuget.org/packages/dotnext/4.4.1">DotNext 4.4.1</a>
* Added memory threshold option to `SoftReferenceOptions`

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.4.1">DotNext.Metaprogramming 4.4.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.4.1">DotNext.Reflection 4.4.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.4.1">DotNext.Unsafe 4.4.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.4.1">DotNext.Threading 4.4.1</a>
* Fixed issue that ignores the value of `PersistentChannelOptions.BufferSize` property
* Fixed critical bug in `PersistentChannel` that leads to incorrect position of the reader within the file with stored messages
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.4.1">DotNext.IO 4.4.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.4.1">DotNext.Net.Cluster 4.4.1</a>
* Improved logging in case of critical faults during Raft state transitions
* Fixed [105](https://github.com/dotnet/dotNext/issues/105)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.4.1">DotNext.AspNetCore.Cluster 4.4.1</a>
* Updated dependencies

# 03-30-2022
<a href="https://www.nuget.org/packages/dotnext/4.4.0">DotNext 4.4.0</a>
* Added efficient way to concate multiple strings and represent the result as a rented buffer. See `Span.Concat` method.
* String concatenation support is added to [BufferWriterSlim&lt;char&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.BufferWriterSlim-1.html) as well
* Added `DotNext.Text.InterpolatedString` class with factory methods to create interpolated strings using rented memory

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.4.0">DotNext.Metaprogramming 4.4.0</a>
* Added support of [with operator](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/operators/with-expression) from C#
* Added support of [object initializer](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers#object-initializers)

<a href="https://www.nuget.org/packages/dotnext.reflection/4.4.0">DotNext.Reflection 4.4.0</a>
* Added `Record<T>` concept class to work with [record types](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.4.0">DotNext.Unsafe 4.4.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.4.0">DotNext.Threading 4.4.0</a>
* Added support of channel completion to [PersistentChannel&lt;TInput, TOutput&gt;](https://dotnet.github.io/dotNext/api/DotNext.Threading.Channels.PersistentChannel-2.html) class
* Added `PersistentChannelOptions.ReliableEnumeration` option that allows transactional reads
* Fixed token linkage represented by extension methods from [LinkedTokenSourceFactory](https://dotnet.github.io/dotNext/api/DotNext.Threading.LinkedTokenSourceFactory.html) class

<a href="https://www.nuget.org/packages/dotnext.io/4.4.0">DotNext.IO 4.4.0</a>
* Added extension methods that allow to write interpolated strings efficiently to [TextWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.textwriter)

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.4.0">DotNext.Net.Cluster 4.4.0</a>
* Fixed exception type for cancellation of replication
* Fixed incorrect behavior when `IRaftCluster.LeaderChanged` fired but `IRaftCluster.LeadershipToken` indicates that leader is not yet elected
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.4.0">DotNext.AspNetCore.Cluster 4.4.0</a>
* Updated dependencies

# 02-28-2022
<a href="https://www.nuget.org/packages/dotnext/4.3.0">DotNext 4.3.0</a>
* Introduced `DotNext.Runtime.Caching.ConcurrentCache<TKey, TValue>` class with LRU/LFU cache eviction policies
* Improved performance of atomic operations based on CAS (Compare-And-Swap)
* Fixed behavior of optimistic read lock in [ReaderWriterSpinLock](https://dotnet.github.io/dotNext/api/DotNext.Threading.ReaderWriterSpinLock.html) class

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.3.0">DotNext.Metaprogramming 4.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.3.0">DotNext.Reflection 4.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.3.0">DotNext.Unsafe 4.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.3.0">DotNext.Threading 4.3.0</a>
* Fixed behavior of optimistic read lock in [AsyncReaderWriterLock](https://dotnet.github.io/dotNext/api/DotNext.Threading.AsyncReaderWriterLock.html) class
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.3.0">DotNext.IO 4.3.0</a>
* Added _flushToDisk_ option to `FileBufferingWriter.Flush` and `FileBufferingWriter.FlushAsync` methods
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.3.0">DotNext.Net.Cluster 4.3.0</a>
* Improved startup time of persistent WAL
* Default value of [PersistentState.Options.WriteMode](https://dotnet.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.Options.html#DotNext_Net_Cluster_Consensus_Raft_PersistentState_Options_WriteMode) is changed to `AutoFlush`
* Fixed transfer of custom cancellation token passed to `RaftCluster.ReplicateAsync` method
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.3.0">DotNext.AspNetCore.Cluster 4.3.0</a>
* Fixed [103](https://github.com/dotnet/dotNext/issues/103)
* Updated dependencies

# 02-07-2022
Many thanks to [Copenhagen Atomics](https://www.copenhagenatomics.com/) for supporting this release.

<a href="https://www.nuget.org/packages/dotnext/4.2.0">DotNext 4.2.0</a>
* Improved scalability of mechanism that allows to attach custom data to arbitrary objects using `UserDataStorage` and `UserDataSlot<T>` types. The improvement works better in high workloads without the risk of lock contention but requires a bit more CPU cycles to obtain the data attached to the object
* Added ability to enumerate values stored in `TypeMap<T>` or `ConcurrentTypeMap<T>`
* Improved debugging experience of `UserDataStorage` type
* Added `Dictionary.Empty` static method that allows to obtain a singleton of empty [IReadOnlyDictionary&lt;TKey, TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlydictionary-2)
* Fixed decoding buffer oveflow in `Base64Decoder` type
* Added `Base64Encoder` type for fast encoding of large binary data
* Deprecation of `Sequence.FirstOrEmpty` extension methods in favor of `Sequence.FirstOrNone`
* Fixed [#91](https://github.com/dotnet/dotNext/pull/91)
* Public constructors of `PooledBufferWriter` and `PooledArrayBufferWriter` with parameters are obsolete in favor of init-only properties
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members
* Optimized performance of `Timeout`, `Optional<T>`, `Result<T>` and `Result<T, TError>` types
* Introduced `DotNext.Runtime.SoftReference` data type in addition to [WeakReference](https://docs.microsoft.com/en-us/dotnet/api/system.weakreference) from .NET

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.2.0">DotNext.Metaprogramming 4.2.0</a>
* Improved overall performance of some scenarios where `UserDataStorage` is used
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members

<a href="https://www.nuget.org/packages/dotnext.reflection/4.2.0">DotNext.Reflection 4.2.0</a>
* Improved overall performance of some scenarios where `UserDataStorage` is used
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.2.0">DotNext.Unsafe 4.2.0</a>
* Updated dependencies
* Reduced size of the compiled assembly: omit private and internal member's nullability attributes

<a href="https://www.nuget.org/packages/dotnext.threading/4.2.0">DotNext.Threading 4.2.0</a>
* Reduced execution time of `CreateTask` overloads declared in `ValueTaskCompletionSource` and `ValueTaskCompletionSource<T>` classes
* Added overflow check to `AsyncCounter` class
* Improved debugging experience of all asynchronous locks
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members
* Reduced lock contention that can be caused by asynchronous locks in concurrent scenarios
* Added `Reset()` method to `TaskCompletionPipe<T>` that allows to reuse the pipe

<a href="https://www.nuget.org/packages/dotnext.io/4.2.0">DotNext.IO 4.2.0</a>
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members
* `FileWriter` now implements [IBufferWriter&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1)

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.2.0">DotNext.Net.Cluster 4.2.0</a>
* Improved compatibility with IL trimming
* Reduced size of the compiled assembly: omit private and internal member's nullability attributes
* Completely rewritten implementation of TCP transport: better buffering and less network overhead. This version of protocol is not binary compatible with any version prior to 4.2.0
* Increased overall stability of the cluster
* Fixed bug with incorrect calculation of the offset within partition file when using persistent WAL. The bug could prevent the node to start correctly with non-empty WAL
* Added Reflection-free support of JSON log entries powered by JSON Source Generator from .NET
* Introduced _Incremental_ log compaction mode to achieve the best performance when the snapshot is relatively small
* Reduced network overhead caused by read barrier used on the follower side for linearizable reads

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.2.0">DotNext.AspNetCore.Cluster 4.2.0</a>
* Improved compatibility with IL trimming
* Reduced size of the compiled assembly: omit nullability attributes for private and internal members

# 12-20-2021
<a href="https://www.nuget.org/packages/dotnext/4.1.3">DotNext 4.1.3</a>
* Smallish performance improvements

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.1.3">DotNext.Metaprogramming 4.1.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.1.3">DotNext.Reflection 4.1.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.1.3">DotNext.Unsafe 4.1.3</a>
* Performance improvements of `Pointer<T>` public methods

<a href="https://www.nuget.org/packages/dotnext.threading/4.1.3">DotNext.Threading 4.1.3</a>
* Fixed potential concurrency issue than can be caused by `AsyncBridge` public methods when cancellation token or wait handle is about to be canceled or signaled

<a href="https://www.nuget.org/packages/dotnext.io/4.1.3">DotNext.IO 4.1.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.1.3">DotNext.Net.Cluster 4.1.3</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.1.3">DotNext.AspNetCore.Cluster 4.1.3</a>
* Updated dependencies

# 12-12-2021
<a href="https://www.nuget.org/packages/dotnext/4.1.2">DotNext 4.1.2</a>
* Minor performance improvements

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.1.2">DotNext.Metaprogramming 4.1.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.1.2">DotNext.Reflection 4.1.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.1.2">DotNext.Unsafe 4.1.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.1.2">DotNext.Threading 4.1.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/4.1.2">DotNext.IO 4.1.2</a>
* Minor performance improvements of `FileBufferingWriter` class

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.1.2">DotNext.Net.Cluster 4.1.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.1.2">DotNext.AspNetCore.Cluster 4.1.2</a>
* Updated dependencies

# 12-09-2021
<a href="https://www.nuget.org/packages/dotnext/4.1.1">DotNext 4.1.1</a>
* Minor performance improvements

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.1.1">DotNext.Metaprogramming 4.1.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/4.1.1">DotNext.Reflection 4.1.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.1.1">DotNext.Unsafe 4.1.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/4.1.1">DotNext.Threading 4.1.1</a>
* Minor performance improvements

<a href="https://www.nuget.org/packages/dotnext.io/4.1.1">DotNext.IO 4.1.1</a>
* Minor performance improvements

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.1.1">DotNext.Net.Cluster 4.1.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.1.1">DotNext.AspNetCore.Cluster 4.1.1</a>
* Updated dependencies

# 12-05-2021
<a href="https://www.nuget.org/packages/dotnext/4.1.0">DotNext 4.1.0</a>
* Optimized bounds check in growable buffers
* Changed behavior of exceptions capturing by `DotNext.Threading.Tasks.Synchronization.GetResult` overloaded methods
* Added `DotNext.Threading.Tasks.Synchronization.TryGetResult` method
* Added `DotNext.Buffers.ReadOnlySequencePartitioner` static class with methods for [ReadOnlySequence&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) partitioning in [parallel](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.parallel) processing scenarios
* Enabled support of IL trimming

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.1.0">DotNext.Metaprogramming 4.1.0</a>
* IL trimming is explicitly disabled because the library highly relied on Reflection API

<a href="https://www.nuget.org/packages/dotnext.reflection/4.1.0">DotNext.Reflection 4.1.0</a>
 IL trimming is explicitly disabled because the library highly relied on Reflection API

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.1.0">DotNext.Unsafe 4.1.0</a>
* Enabled support of IL trimming

<a href="https://www.nuget.org/packages/dotnext.threading/4.1.0">DotNext.Threading 4.1.0</a>
* Reduced memory allocation by async locks
* Added cancellation support to `AsyncLazy<T>` class
* Introduced `TaskCompletionPipe<T>` class that allows to consume tasks as they complete
* Removed _Microsoft.Extensions.ObjectPool_ dependency
* Enabled support of IL trimming

<a href="https://www.nuget.org/packages/dotnext.io/4.1.0">DotNext.IO 4.1.0</a>
* Enabled support of IL trimming

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.1.0">DotNext.Net.Cluster 4.1.0</a>
* Enabled support of IL trimming

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.1.0">DotNext.AspNetCore.Cluster 4.1.0</a>
* Enabled support of IL trimming

# 11-25-2021
.NEXT 4.0.0 major release is out! Its primary focus is .NET 6 support as well as some other key features:
* Native support of [C# 10 Interpolated Strings](https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6/) across various buffer types, streams and other I/O enhancements. String building and string encoding/decoding with zero allocation overhead is now a reality
* All asynchronous locks do not allocate [tasks](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task) anymore in case of lock contention. Instead, they are moved to [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask) pooling
* `ValueTaskCompletionSource` and `ValueTaskCompletionSource<T>` classes are stabilized and used as a core of [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask) pooling
* Introduced Raft-native cluster membership management as proposed in Diego's original paper instead of external discovery mechanism
* Introduced Gossip-based messaging framework

Use [this](https://dotnet.github.io/dotNext/migration/index.html) guide to migrate from 3.x.

<a href="https://www.nuget.org/packages/dotnext/4.0.0">DotNext 4.0.0</a>
* Added `DotNext.Span.Shuffle` and `DotNext.Collections.Generic.List.Shuffle` extension methods that allow to randomize position of elements within span/collection
* Added `DotNext.Collections.Generic.Sequence.Copy` extension method for making copy of the original enumerable collection. The memory for the copy is always rented from the pool
* Added `DotNext.Collections.Generic.Collection.PeekRandom` extension method that allows to select random element from the collection
* Improved performance of `DotNext.Span.TrimLength` and `StringExtensions.TrimLength` extension methods
* Introduced `DotNext.Buffers.BufferHelpers.TrimLength` extension methods for [ReadOnlyMemory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.readonlymemory-1) and [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1) data types
* Improved performance of `DotNext.Buffers.BufferWriter<T>.AddAll` method
* Reduced memory allocations by `ElementAt`, `FirstOrEmpty`, `FirstOrNull`, `ForEach` extension methods in `DotNext.Collections.Generic.Sequence` class
* Added `DotNext.Numerics.BitVector` that allows to convert **bool** vectors into integral types
* Added ability to write interpolated strings to `IBufferWriter<char>` without temporary allocations
* Added ability to write interpolated strings to `BufferWriterSlim<char>`. This makes `BufferWriterSlim<char>` type as allocation-free alternative to [StringBuilder](https://docs.microsoft.com/en-us/dotnet/api/system.text.stringbuilder)
* Introduced a concept of binary-formattable types. See `DotNext.Buffers.IBinaryFormattable<TSelf>` interface for more information
* Introduced `Reference<T>` type as a way to pass the reference to the memory location in asynchronous scenarios
* `Box<T>` is replaced with `Reference<T>` value type
* `ITypeMap<T>` interface and implementing classes allow to associate an arbitrary value with the type
* Added overloaded `Result<T, TError>` value type for C-style error handling

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/4.0.0">DotNext.Metaprogramming 4.0.0</a>
* Added support of interpolated string expression as described in [this article](https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6/) using `InterpolationExpression.Create` static method
* Added support of task pooling to async lambda expressions
* Migration to C# 10 and .NET 6

<a href="https://www.nuget.org/packages/dotnext.reflection/4.0.0">DotNext.Reflection 4.0.0</a>
* Migration to C# 10 and .NET 6

<a href="https://www.nuget.org/packages/dotnext.unsafe/4.0.0">DotNext.Unsafe 4.0.0</a>
* Unmanaged memory pool has moved to [NativeMemory](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.nativememory) class instead of [Marshal.AllocHGlobal](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.allochglobal) method

<a href="https://www.nuget.org/packages/dotnext.threading/4.0.0">DotNext.Threading 4.0.0</a>
* Polished `ValueTaskCompletionSource` and `ValueTaskCompletionSource<T>` data types. Also these types become a foundation for all synchronization primitives within the library
* Return types of all methods of asynchronous locks now moved to [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask) and [ValueTask&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1) types
* Together with previous change, all asynchronous locks are written on top of `ValueTaskCompletionSource` and `ValueTaskCompletionSource<T>` data types. It means that these asynchronous locks use task pooling that leads to zero allocation on the heap and low GC latency
* Added `AsyncEventHub` synchronization primitive for asynchronous code
* Introduced diagnostics and debugging tools for all synchronization primitives: lock contentions, information about suspended callers, et. al.

<a href="https://www.nuget.org/packages/dotnext.io/4.0.0">DotNext.IO 4.0.0</a>
* Added `DotNext.IO.SequenceBinaryReader.Position` property that allows to obtain the current position of the reader in the underlying sequence
* Added `DotNext.IO.SequenceBinaryReader.Read(Span<byte>)` method
* Optimized performance of some `ReadXXX` methods of `DotNext.IO.SequenceReader` type
* All `WriteXXXAsync` methods of `IAsyncBinaryWriter` are replaced with a single `WriteFormattableAsync` method supporting [ISpanFormattable](https://docs.microsoft.com/en-us/dotnet/api/system.ispanformattable) interface. Now you can encode efficiently any type that implements this interface
* Added `FileWriter` and `FileReader` classes that are tuned for fast file I/O with the ability to access the buffer explicitly
* Introduced a concept of a serializable Data Transfer Objects represented by `ISerializable<TSelf>` interface. The interface allows to control the serialization/deserialization behavior on top of `IAsyncBinaryWriter` and `IAsyncBinaryReader` interfaces. Thanks to static abstract interface methods, the value of the type can be easily reconstructed from its serialized state
* Added support of binary-formattable types to `IAsyncBinaryWriter` and `IAsyncBinaryReader` interfaces
* Improved performance of `FileBufferingWriter` I/O operations with preallocated file size feature introduced in .NET 6
* `StreamExtensions.Combine` allows to represent multiple streams as a single stream

<a href="https://www.nuget.org/packages/dotnext.net.cluster/4.0.0">DotNext.Net.Cluster 4.0.0</a>
* Optimized memory allocation for each hearbeat message emitted by Raft node in leader state
* Fixed compatibility of WAL Interpreter Framework with TCP/UDP transports
* Added support of Raft-native cluster configuration management that allows to use Raft features for managing cluster members instead of external discovery protocol
* Persistent WAL has moved to new implementation of asynchronous locks to reduce the memory allocation
* Added various snapshot building strategies: incremental and inline
* Optimized file I/O performance of persistent WAL
* Reduced the number of opened file descriptors required by persistent WAL
* Improved performance of partitions allocation in persistent WAL with preallocated file size feature introduced in .NET 6
* Fixed packet loss for TCP/UDP transports
* Added read barrier for linearizable reads on Raft follower nodes
* Added transport-agnostic implementation of [HyParView](https://asc.di.fct.unl.pt/~jleitao/pdf/dsn07-leitao.pdf) membership protocol suitable for Gossip-based messaging

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/4.0.0">DotNext.AspNetCore.Cluster 4.0.0</a>
* Added configurable HTTP protocol version selection policy
* Added support of leader lease in Raft implementation for optimized read operations
* Added `IRaftCluster.LeadershipToken` property that allows to track leadership transfer
* Introduced `IRaftCluster.Readiness` property that represents the readiness probe. The probe indicates whether the cluster member is ready to serve client requests

# 08-12-2021
<a href="https://www.nuget.org/packages/dotnext/3.3.1">DotNext 3.3.1</a>
* `DotNext.Threading.Tasks.Synchronization.WaitAsync` doesn't suspend the exception associated with faulty input task anymore

<a href="https://www.nuget.org/packages/dotnext.threading/3.3.1">DotNext.Threading 3.3.1</a>
* Fixed [73](https://github.com/dotnet/dotNext/issues/73)

# 07-28-2021
<a href="https://www.nuget.org/packages/dotnext/3.3.0">DotNext 3.3.0</a>
* Added `ValueTypeExtensions.Normalize` extension methods that allow to normalize numbers of different types
* Improved overall performance of extension methods declaring in `RandomExtensions` class
* Added `Func.IsTypeOf<T>()` and `Predicate.IsTypeOf<T>()` cached predicates
* Deprecation of `CallerMustBeSynchronizedAttribute`
* Fixed backward compatibility issues when _DotNext 3.2.x_ or later used in combination with _DotNext.IO 3.1.x_
* Fixed LGTM warnings

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/3.3.0">DotNext.Metaprogramming 3.3.0</a>
* Added `CodeGenerator.Statement` static method to simplify migration from pure Expression Trees
* Fixed LGTM warnings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/3.3.0">DotNext.Reflection 3.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/3.3.0">DotNext.Unsafe 3.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/3.3.0">DotNext.Threading 3.3.0</a>
* Introduced a new asynchronous primitive `AsyncCorrelationSource` for synchronization
* Added `ValueTaskCompletionSource<T>` as reusable source of tasks suitable for pooling

<a href="https://www.nuget.org/packages/dotnext.io/3.3.0">DotNext.IO 3.3.0</a>
* `FileBufferingWriter.GetWrittenContentAsync` overload returning `ReadOnlySequence<T>` now ensures that the buffer tail is flushed to the disk
* `FileBufferingWriter.Flush` and `FileBufferingWriter.FlushAsync` methods ensure that the buffer tail is flushed to the disk

<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.3.0">DotNext.Net.Cluster 3.3.0</a>
* Added implementation of [Jump](https://arxiv.org/pdf/1406.2294.pdf) consistent hash
* Added support of typed message handlers. See `MessagingClient` and `MessageHandler` classes for more information

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.3.0">DotNext.AspNetCore.Cluster 3.3.0</a>
* Added ETW counter for response time of nodes in the cluster

# 06-09-2021
<a href="https://www.nuget.org/packages/dotnext/3.2.1">DotNext 3.2.1</a>
* Fixed implementation of `Optional<T>.GetHashCode` to distinguish hash code of undefined and null values

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/3.2.1">DotNext.Metaprogramming 3.2.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/3.2.1">DotNext.Reflection 3.2.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/3.2.1">DotNext.Unsafe 3.2.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/3.2.1">DotNext.Threading 3.2.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/3.2.1">DotNext.IO 3.2.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.2.1">DotNext.Net.Cluster 3.2.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.2.1">DotNext.AspNetCore.Cluster 3.2.1</a>
* Updated dependencies

# 06-07-2021
<a href="https://www.nuget.org/packages/dotnext/3.2.0">DotNext 3.2.0</a>
* Added `TryDetachBuffer` method to `BufferWriterSlim<T>` type that allows to flow buffer in async scenarios
* Added `TryGetWrittenContent` method to `SparseBufferWriter<T>` that allows to obtain the written buffer if it is represented by contiguous memory block
* Added `OptionalConverterFactory` class that allows to use `Optional<T>` data type in JSON serialization. This type allows to hide data from JSON if the property of field has undefined value. Useful for designing DTOs for REST API with partial resource updates via PATCH method. Available only when target is .NET 5.
* Added `TryResize` and `Resize` methods to `MemoryOwner<T>` value type
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/3.2.0">DotNext.Metaprogramming 3.2.0</a>
* Call site optimization for `AsDynamic()` extension method that allows to construct LINQ expression tree on-the-fly using C# expressions
* Fixed [70](https://github.com/dotnet/dotNext/issues/70)

<a href="https://www.nuget.org/packages/dotnext.reflection/3.2.0">DotNext.Reflection 3.2.0</a>
* Respect volatile modifier when reading/writing field

<a href="https://www.nuget.org/packages/dotnext.unsafe/3.2.0">DotNext.Unsafe 3.2.0</a>
* Added additional overloads to `Pointer<T>` value type with **nuint** parameter

<a href="https://www.nuget.org/packages/dotnext.threading/3.2.0">DotNext.Threading 3.2.0</a>
* Added `EnsureState` to `AsyncTrigger` class as synchronous alternative with fail-fast behavior

<a href="https://www.nuget.org/packages/dotnext.io/3.2.0">DotNext.IO 3.2.0</a>
* Improved performance of all `IAsyncBinaryReader` interface implementations
* Added `TryReadBlock` extension method that allows to read the block of memory from pipe synchronously
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.2.0">DotNext.Net.Cluster 3.2.0</a>
* Smallish improvements of I/O operations related to log entries
* Improved performance of background compaction algorithm
* Persistent WAL now supports concurrent read/write. Appending of new log entries to the log tail doesn't suspend readers anymore
* Added event id and event name to all log messages

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.2.0">DotNext.AspNetCore.Cluster 3.2.0</a>
* Improved performance of log entries decoding on receiver side
* Added event id and event name to all log messages

# 05-14-2021
<a href="https://www.nuget.org/packages/dotnext/3.1.1">DotNext 3.1.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/3.1.1">DotNext.Metaprogramming 3.1.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/3.1.1">DotNext.Reflection 3.1.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/3.1.1">DotNext.Unsafe 3.1.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/3.1.1">DotNext.Threading 3.1.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/3.1.1">DotNext.IO 3.1.1</a>
* `FileBufferingWriter.Options` is refactored as value type to avoid heap allocation
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.1.1">DotNext.Net.Cluster 3.1.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.1.1">DotNext.AspNetCore.Cluster 3.1.1</a>
* Updated dependencies

# 05-11-2021
This release is primarily focused on improvements of stuff related to cluster programming and Raft: persistent WAL, transferring over the wire, buffering and reducing I/O overhead. Many ideas for this release were proposed by [potrusil-osi](https://github.com/potrusil-osi) in the issue [57](https://github.com/dotnet/dotNext/issues/57).

<a href="https://www.nuget.org/packages/dotnext/3.1.0">DotNext 3.1.0</a>
* Added async support to `IGrowableBuffer<T>` interface
* Added indexer to `MemoryOwner<T>` supporting **nint** data type
* Added more members to `SpanReader<T>` and `SpanWriter<T>` types

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/3.1.0">DotNext.Metaprogramming 3.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/3.1.0">DotNext.Reflection 3.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/3.1.0">DotNext.Unsafe 3.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/3.1.0">DotNext.Threading 3.1.0</a>
* `AsyncTigger` now supports fairness policy when resuming suspended callers
* Added support of diagnostics counters

<a href="https://www.nuget.org/packages/dotnext.io/3.1.0">DotNext.IO 3.1.0</a>
* Added `SkipAsync` method to `IAsyncBinaryReader` interface
* Added `TryGetBufferWriter` to `IAsyncBinaryWriter` interface that allows to avoid async overhead when writing to in-memory buffer
* Added more performance optimization options to `FileBufferingWriter` class
* Fixed bug in `StreamSegment.Position` property setter causes invalid position in the underlying stream

<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.1.0">DotNext.Net.Cluster 3.1.0</a>
* Added support of three log compaction modes to `PersistentState` class:
   * _Sequential_ which is the default compaction mode in 3.0.x and earlier versions. Provides best optimization of disk space by the cost of the performance of adding new log entries
   * _Background_ which allows to run log compaction in parallel with write operations
   * _Foreground_ which runs log compaction in parallel with commit operation
* Small performance improvements when passing log entries over the wire for TCP and UDP protocols
* Added buffering API for log entries
* Added optional buffering of log entries and snapshot when transferring using TCP or UDP protocols
* Introduced _copy-on-read_ behavior to `PersistentState` class to reduce lock contention between writers and the replication process
* Introduced in-memory cache of log entries to `PersistentState` class to eliminate I/O overhead when appending and applying new log entries
* Reduced number of reads from Raft audit trail during replication
* Interpreter Framework: removed overhead caused by deserialization of command identifier from the log entry. Now the identifier is a part of log entry metadata which is usually pre-cached by underlying WAL implementation

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.1.0">DotNext.AspNetCore.Cluster 3.1.0</a>
* Added ability to override cluster members discovery service. See `IMembersDiscoveryService` interface
* Small performance improvements when passing log entries over the wire for HTTP/1, HTTP/2 and HTTP/3 protocols
* Added optional buffering of log entries and snapshot when transferring over the wire. Buffering allows to reduce lock contention of persistent WAL
* Introduced incremental compaction of committed log entries which is running by special background worker 

**Breaking Changes**: Binary format of persistent WAL has changed. `PersistentState` class from 3.1.0 release is unable to parse the log that was created by earlier versions.

# 02-28-2021
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.0.2">DotNext.AspNetCore.Cluster 3.0.2</a>
* Fixed IP address filter when white list of allowed networks is in use

# 02-26-2021
<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.0.1">DotNext.Net.Cluster 3.0.1</a>
* Minor performance optimizations of Raft heartbeat processing

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.0.1">DotNext.AspNetCore.Cluster 3.0.1</a>
* Unexpected HTTP response received from Raft RPC call cannot crash the node anymore (see [54](https://github.com/dotnet/dotNext/issues/54))

# 01-30-2021
The next major version is out! Its primary focus is .NET 5 support while keeping compatibility with .NET Standard 2.1. As a result, .NEXT libraries built for multiple target frameworks. Additional changes include performance optimizations, polishing of existing API, dropping support of members that were deprecated in 2.x, expanding usage of nullable reference types.

Migration guide for 2.x users is [here](https://dotnet.github.io/dotNext/migration/2.html). Please consider that this version is not fully backward compatible with 2.x.

<a href="https://www.nuget.org/packages/dotnext/3.0.0">DotNext 3.0.0</a>
* Improved performance of [SparseBufferWriter&lt;T&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/SparseBufferWriter%601), [BufferWriterSlim&lt;T&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/BufferWriterSlim%601), [PooledArrayBufferWriter&lt;T&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/PooledArrayBufferWriter%601), [PooledBufferWriter&lt;T&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/PooledBufferWriter%601)
* Fixed nullability attributes
* `ArrayRental<T>` type is replaced by [MemoryOwner&lt;T&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/MemoryOwner%601) type
* Removed obsolete members and classes
* Removed `UnreachableCodeExecutionException` exception
* Completely rewritten implementation of extension methods provided by [AsyncDelegate](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Threading/AsyncDelegate) class
* Added [Base64Decoder](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Text/Base64Decoder) type for efficient decoding of base64-encoded bytes in streaming scenarios
* Removed `Future&lt;T&gt;` type
* Added `ThreadPoolWorkItemFactory` static class with extension methods for constructing [IThreadPoolWorkItem](https://docs.microsoft.com/en-us/dotnet/api/system.threading.ithreadpoolworkitem) instances from method pointers. Available only for .NET 5 target
* Introduced factory methods for constructing delegate instances from the pointers to the managed methods
* `DOTNEXT_STACK_ALLOC_THRESHOLD` environment variable can be used to override stack allocation threshold for all .NEXT routines
* Dropped support of value delegates. They are replaced by functional interfaces. However, they are hiddent from the library consumer so every public API that was based on value delegates now has at least two overloads: CLS-compliant version using regular delegate type and unsafe version using function pointer syntax.
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/3.0.0">DotNext.IO 3.0.0</a>
* Changed behavior of `FileBufferingWriter.GetWrittenContentAsStream` and `FileBufferingWriter.GetWrittenContentAsStreamAsync` in a way which allows you to use synchronous/asynchronous I/O for writing and reading separately
* Introduced extension methods for [BufferWriterSlim&lt;char&gt;](https://www.fuget.org/packages/DotNext/3.0.0/lib/net5.0/DotNext.dll/DotNext.Buffers/BufferWriterSlim%601) type for encoding of primitive data types
* Fixed nullability attributes
* Added advanced encoding/decoding methods to [IAsyncBinaryWriter](https://www.fuget.org/packages/DotNext.IO/3.0.0/lib/net5.0/DotNext.IO.dll/DotNext.IO/IAsyncBinaryWriter) and [IAsyncBinaryReader](https://www.fuget.org/packages/DotNext.IO/3.0.0/lib/net5.0/DotNext.IO.dll/DotNext.IO/IAsyncBinaryReader) interfaces
* Removed obsolete members and classes
* Simplified signature of `AppendAsync` methods exposed by [IAuditTrail&lt;TEntry&gt;](https://www.fuget.org/packages/DotNext.IO/3.0.0/lib/net5.0/DotNext.IO.dll/DotNext.IO.Log/IAuditTrail%601) interface
* Improved performances of extension methods declared in [PipeExtensions](https://www.fuget.org/packages/DotNext.IO/3.0.0/lib/net5.0/DotNext.IO.dll/DotNext.IO.Pipelines/PipeExtensions) class
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/3.0.0">DotNext.Metaprogramming 3.0.0</a>
* Fixed nullability attributes
* Fixed [issue 23](https://github.com/dotnet/dotNext/issues/23)
* Fixed code generation of **finally** blocks inside of asynchronous lambda expressions
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/3.0.0">DotNext.Reflection 3.0.0</a>
* Improved performance of reflective calls
* [DynamicInvoker](https://www.fuget.org/packages/DotNext.Reflection/3.0.0/lib/net5.0/DotNext.Reflection.dll/DotNext.Reflection/DynamicInvoker) delegate allows to pass arguments for dynamic invocation as [Span&lt;object&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) instead of `object[]`
* Fixed nullability attributes

<a href="https://www.nuget.org/packages/dotnext.threading/3.0.0">DotNext.Threading 3.0.0</a>
* Modified ability to await on [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken) and [WaitHandle](https://docs.microsoft.com/en-us/dotnet/api/system.threading.waithandle). [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask) is the primary return type of the appropriate methods
* Fixed nullability attributes
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/3.0.0">DotNext.Unsafe 3.0.0</a>
* Removed obsolete members and classes
* Fixed nullability attributes
* Added `PinnedArray<T>` as a wrapper of pinned arrays from .NET 5
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/3.0.0">DotNext.Net.Cluster 3.0.0</a>
* Improved performance of [persistent WAL](https://www.fuget.org/packages/DotNext.Net.Cluster/3.0.0/lib/net5.0/DotNext.Net.Cluster.dll/DotNext.Net.Cluster.Consensus.Raft/PersistentState)
* Added support of active-standby configuration of Raft cluster. Standby node cannot become a leader but can be used for reads
* Introduced [framework](https://www.fuget.org/packages/DotNext.Net.Cluster/3.0.0/lib/net5.0/DotNext.Net.Cluster.dll/DotNext.Net.Cluster.Consensus.Raft.Commands/CommandInterpreter) for writing interpreters of log entries stored in persistent write-ahead log
* Added support of JSON-serializable log entries (available for .NET 5 only)
* Fixed bug causing long shutdown of Raft node which is using TCP transport
* Added support of **PreVote** extension for Raft preventing _term inflation_

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/3.0.0">DotNext.AspNetCore.Cluster 3.0.0</a>
* Added `UsePersistenceEngine` extension method for correct registration of custom persistence engine derived from [PersistentState](https://www.fuget.org/packages/DotNext.Net.Cluster/3.0.0/lib/net5.0/DotNext.Net.Cluster.dll/DotNext.Net.Cluster.Consensus.Raft/PersistentState) class
* Added support of HTTP/3 (available for .NET 5 only)
* Significantly optimized performance and traffic volume of **AppendEntries** Raft RPC call. Now replication performance is comparable to TCP/UDP transports
* Added DNS support. Now cluster member address can be specified using its name instead of IP address

`DotNext.Augmentation` IL weaver add-on for MSBuild is no longer supported.

# 01-07-2021
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.12.2">DotNext.Metaprogramming 2.12.2</a>
* Fixed [46](https://github.com/dotnet/dotNext/issues/46)

# 12-16-2020
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.12.1">DotNext.Metaprogramming 2.12.1</a>
* Fixed invalid detection of the collection item type inside of [CollectionAccessExpression](https://dotnet.github.io/dotNext/api/DotNext.Linq.Expressions.CollectionAccessExpression.html)

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.12.1">DotNext.Net.Cluster 2.12.1</a>
* Fixed issue [24](https://github.com/dotnet/dotNext/issues/24)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.12.1">DotNext.AspNetCore.Cluster 2.12.1</a>
* Fixed issue [24](https://github.com/dotnet/dotNext/issues/24)

# 12-04-2020
<a href="https://www.nuget.org/packages/dotnext/2.12.0">DotNext 2.12.0</a>
* Added consuming enumerator for [IProducerConsumerCollection&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.iproducerconsumercollection-1)
* Introduced `ServiceProviderFactory` class and its factory methods for producing [Service Providers](https://docs.microsoft.com/en-us/dotnet/api/system.iserviceprovider)
* Significant performance improvements of `StringExtensions.Reverse` method
* Introduced a new class `SparseBufferWriter<T>` in addition to existing buffer writes which acts as a growable buffer without memory reallocations
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/2.12.0">DotNext.IO 2.12.0</a>
* Introduced `TextBufferReader` class inherited from [TextReader](https://docs.microsoft.com/en-us/dotnet/api/system.io.textreader) that can be used to read the text from [ReadOnlySequence&lt;char&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) or [ReadOnlyMemory&lt;char&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.readonlymemory-1)
* Added `SequenceBuilder<T>` type for building [ReadOnlySequence&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) instances from the chunk of memory blocks
* Added `GetWrittenContentAsStream` and `GetWrittenContentAsStreamAsync` methods to [FileBufferingWriter](https://dotnet.github.io/dotNext/api/DotNext.IO.FileBufferingWriter.html) class
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.12.0">DotNext.Metaprogramming 2.12.0</a>
* Added support of `await using` statement
* Added support of `await foreach` statement
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/2.12.0">DotNext.Reflection 2.12.0</a>
* More performance optimizations in code generation mechanism responsible for the method or constructor calls
* Added ability to reflect abstract and interface methods
* Added support of volatile access to the field via reflection

<a href="https://www.nuget.org/packages/dotnext.threading/2.12.0">DotNext.Threading 2.12.0</a>
* Added support of `Count` and `CanCount` properties inherited from [ChannelReader&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels.channelreader-1) by persistent channel reader
* Added support of diagnostics counters for persistent channel
* Fixed resuming of suspended callers in [AsyncTrigger](https://dotnet.github.io/dotNext/api/DotNext.Threading.AsyncTrigger.html) class
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/2.12.0">DotNext.Unsafe 2.12.0</a>
* Fixed ignoring of array offset in `ReadFrom` and `WriteTo` methods of [Pointer&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Runtime.InteropServices.Pointer-1.html) type
* Added `ToArray` method to [Pointer&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Runtime.InteropServices.Pointer-1.html) type
* Added indexer property to [IUnmanagedArray&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Runtime.InteropServices.IUnmanagedArray-1.html) interface
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.12.0">DotNext.Net.Cluster 2.12.0</a>
* Updated dependencies shipped with .NET Core 3.1.10

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.12.0">DotNext.AspNetCore.Cluster 2.12.0</a>
* Updated dependencies shipped with .NET Core 3.1.10

# 11-11-2020
<a href="https://www.nuget.org/packages/dotnext.reflection/2.11.2">DotNext.Reflection 2.11.2</a>
* More performance optimizations in code generation mechanism responsible for construction dynamic method or constructor calls

# 11-08-2020
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.11.1">DotNext.Metaprogramming 2.11.1</a>
* Fixed issue [19](https://github.com/dotnet/dotNext/issues/19)

<a href="https://www.nuget.org/packages/dotnext.reflection/2.11.1">DotNext.Reflection 2.11.1</a>
* `Reflector.Unreflect` now can correctly represents **void** method or property setter as [DynamicInvoker](https://dotnet.github.io/dotNext/api/DotNext.Reflection.DynamicInvoker.html) delegate
* Unreflected members via [DynamicInvoker](https://dotnet.github.io/dotNext/api/DotNext.Reflection.DynamicInvoker.html) delegate correctly handles boxed value types
* Improved performance of [DynamicInvoker](https://dotnet.github.io/dotNext/api/DotNext.Reflection.DynamicInvoker.html) for by-ref argument of value type

# 11-01-2020
<a href="https://www.nuget.org/packages/dotnext/2.11.0">DotNext 2.11.0</a>
* Added `Span<T>.CopyTo` and `ReadOnlySpan<T>.CopyTo` extension methods to support cases when the source span can be larger than the destination
* Added `Span.AsSpan` and `Span.AsReadOnlySpan` for value tuples
* Deprecated [EnumerableTuple](https://dotnet.github.io/dotNext/api/DotNext.EnumerableTuple-2.html) data type
* Minor performance improvements
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.11.0">DotNext.Metaprogramming 2.11.0</a>
* Overloaded `CodeGenerator.AsyncLambda` supports _Pascal_-style return (issue [13](https://github.com/dotnet/dotNext/issues/13))
* Fixed suppression of exceptions raised by generated async lambda (issue [14](https://github.com/dotnet/dotNext/issues/14))
* Fixed invalid behavior of async lambda body rewriter (issue [17](https://github.com/dotnet/dotNext/issues/17))
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/2.11.0">DotNext.Reflection 2.11.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/2.11.0">DotNext.Threading 2.11.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/2.11.0">DotNext.Unsafe 2.11.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.11.0">DotNext.Net.Cluster 2.11.0</a>
* Added `requestTimeout` configuration property for TCP/UDP transports
* Stabilized shutdown of Raft server for TCP/UDP transports
* Added SSL support for TCP transport
* Updated dependencies shipped with .NET Core 3.1.9

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.11.0">DotNext.AspNetCore.Cluster 2.11.0</a>
* Added `requestTimeout` and `rpcTimeout` configuration properties for precise control over timeouts used for communication between Raft nodes (issue [12](https://github.com/dotnet/dotNext/issues/12))
* Updated dependencies shipped with .NET Core 3.1.9

# 09-28-2020
<a href="https://www.nuget.org/packages/dotnext/2.10.1">DotNext 2.10.1</a>
* Fixed correctness of `Clear(bool)` method overridden by `PooledArrayBufferWriter<T>` and `PooledBufferWriter<T>` classes
* Added `RemoveLast` and `RemoveFirst` methods to `PooledArrayBufferWriter<T>` class
* `Optional<T>` type distinguishes **null** and undefined value
* [DotNext.Sequence](https://dotnet.github.io/dotNext/api/DotNext.Sequence.html) class is now deprecated and replaced with [DotNext.Collections.Generic.Sequence](https://dotnet.github.io/dotNext/api/DotNext.Collections.Generic.Sequence.html) class. It's binary compatible but source incompatible change
* Added [new API](https://dotnet.github.io/dotNext/api/DotNext.Resources.ResourceManagerExtensions.html) for writing resource string readers. It utilizes [Caller Info](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/caller-information) feature in C# to resolve resource entry name using accessor method or property
* Introduced [BufferWriterSlim&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.BufferWriterSlim-1.html) type as lightweight and stackalloc-friendly version of [PooledBufferWriter&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.PooledBufferWriter-1.html) type
* Introduced [SpanReader&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.SpanReader-1.html) and [SpanWriter&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.SpanWriter-1.html) types that can be used for sequential access to the elements in the memory span
* Removed unused resource strings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.10.1">DotNext.Metaprogramming 2.10.1</a>
* Added extension methods of [ExpressionBuilder](https://dotnet.github.io/dotNext/api/DotNext.Linq.Expressions.ExpressionBuilder.html) class for constructing expressions of type [Optional&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Optional-1.html), [Result&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Result-1.html) or [Nullable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1)
* Fixed bug with expression building using **dynamic** keyword
* [UniversalExpression](https://dotnet.github.io/dotNext/api/DotNext.Linq.Expressions.UniversalExpression.html) is superseded by _ExpressionBuilder.AsDynamic_ extension method
* Removed unused resource strings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/2.10.1">DotNext.Reflection 2.10.1</a>
* Removed unused resource strings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/2.10.1">DotNext.Threading 2.10.1</a>
* [AsyncExchanger&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Threading.AsyncExchanger-1.html) class now has a method for fast synchronous exchange
* [AsyncTimer](https://dotnet.github.io/dotNext/api/DotNext.Threading.AsyncTimer.html) implements [IAsyncDisposable](https://docs.microsoft.com/en-us/dotnet/api/system.iasyncdisposable) for graceful shutdown
* Removed unused resource strings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/2.10.1">DotNext.Unsafe 2.10.1</a>
* [Pointer&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Runtime.InteropServices.Pointer-1.html) value type now implements [IPinnable](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ipinnable) interface
* Added interop between [Pointer&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Runtime.InteropServices.Pointer-1.html) and [System.Reflection.Pointer](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.pointer)
* Removed unused resource strings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.10.1">DotNext.Net.Cluster 2.10.1</a>
* Removed unused resource strings
* Updated dependencies shipped with .NET Core 3.1.8

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.10.1">DotNext.AspNetCore.Cluster 2.10.1</a>
* Removed unused resource strings
* Updated dependencies shipped with .NET Core 3.1.8

# 08-16-2020
<a href="https://www.nuget.org/packages/dotnext/2.9.6">DotNext 2.9.6</a>
* Improved performance of [Enum Member API](https://dotnet.github.io/dotNext/features/core/enum.html)

<a href="https://www.nuget.org/packages/dotnext.io/2.7.6">DotNext.IO 2.7.6</a>
* Fixed compiler warnings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.6.6">DotNext.Metaprogramming 2.6.6</a>
* Fixed compiler warnings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/2.6.6">DotNext.Reflection 2.6.6</a>
* Fixed compiler warnings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/2.9.6">DotNext.Threading 2.9.6</a>
* Fixed compiler warnings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/2.7.6">DotNext.Unsafe 2.7.6</a>
* Fixed compiler warnings
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.6.6">DotNext.Net.Cluster 2.6.6</a>
* Fixed unstable behavior of Raft TCP transport on Windows. See issue [#10](https://github.com/dotnet/dotNext/issues/10) for more info.
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.6.6">DotNext.AspNetCore.Cluster 2.6.6</a>
* Updated dependencies

# 08-08-2020
<a href="https://www.nuget.org/packages/dotnext/2.9.5">DotNext 2.9.5</a>
* Added support of custom attributes to [Enum Member API](https://dotnet.github.io/dotNext/features/core/enum.html)

# 08-06-2020
<a href="https://www.nuget.org/packages/dotnext/2.9.1">DotNext 2.9.1</a>
* Added `Continuation.ContinueWithTimeout<T>` extension method that allows to produce the task from the given task with attached timeout and, optionally, token

<a href="https://www.nuget.org/packages/dotnext.threading/2.9.0">DotNext.Threading 2.9.0</a>
* Fixed graceful shutdown for async locks if they are not in locked state
* Added  [AsyncExchanger&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Threading.AsyncExchanger-1.html) synchronization primitive that allows to organize pipelines
* [AsyncTrigger](https://dotnet.github.io/dotNext/api/DotNext.Threading.AsyncTrigger.html) now has additional `SignalAndWaitAsync` overloads

# 07-30-2020
<a href="https://www.nuget.org/packages/dotnext/2.9.0">DotNext 2.9.0</a>
* Added `Sequence.ToAsyncEnumerable()` extension method that allows to convert arbitrary [IEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1) to [IAsyncEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerable-1)
* Added extension methods to `Sequence` class for working with [async streams][IAsyncEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerable-1)

<a href="https://www.nuget.org/packages/dotnext.io/2.7.3">DotNext.IO 2.7.3</a>
* Fixed behavior of `GetObjectDataAsync` method in [StreamTransferObject](https://dotnet.github.io/dotNext/api/DotNext.IO.StreamTransferObject.html). Now it respects the value of `IsReusable` property.

# 07-27-2020
<a href="https://www.nuget.org/packages/dotnext/2.8.0">DotNext 2.8.0</a>
* Added `MemoryTemplate<T>` value type that represents pre-compiled template with placeholders used for fast creation of `Memory<T>` and **string** objects

# 07-24-2020
<a href="https://www.nuget.org/packages/dotnext.io/2.7.2">DotNext.IO 2.7.2</a>
* Added `BufferWriter.WriteLine` overloaded extension method that allows to specify `ReadOnlySpan<char>` as an input argument

# 07-15-2020
<a href="https://www.nuget.org/packages/dotnext.io/2.7.1">DotNext.IO 2.7.1</a>
* Text writer constructed with `TextWriterSource.AsTextWriter` extension method can be converted to string containing all written characters

# 07-13-2020
<a href="https://www.nuget.org/packages/dotnext.unsafe/2.7.1">DotNext.Unsafe 2.7.1</a>
* Optimized `UnmanagedMemoryPool<T>.GetAllocator` method

# 07-11-2020
<a href="https://www.nuget.org/packages/dotnext.unsafe/2.7.0">DotNext.Unsafe 2.7.0</a>
* `UnmanagedMemoryPool<T>.GetAllocator` public static method is added for compatibility with [MemoryAllocator&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.MemoryAllocator-1.html) delegate

# 07-09-2020
This release is mainly focused on `DotNext.IO` library to add new API unifying programming experience across I/O pipelines, streams, [sequences](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) and [buffer writers](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1).

<a href="https://www.nuget.org/packages/dotnext/2.7.0">DotNext 2.7.0</a>
* Introduced extension methods in [Span](https://dotnet.github.io/dotNext/api/DotNext.Span.html) class for concatenation of memory spans
* Removed allocation of [Stream](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) in the extension methods of [StreamSource](https://dotnet.github.io/dotNext/api/DotNext.IO.StreamSource.html) class when passed [ReadOnlySequence&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) is empty
* [StreamSource](https://dotnet.github.io/dotNext/api/DotNext.IO.StreamSource.html) has additional methods to create streams from various things
* [PooledArrayBufferWriter&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.PooledArrayBufferWriter-1.html) and [PooledBufferWriter&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.PooledBufferWriter-1.html) support reuse of the internal buffer using overloaded `Clear(bool)` method

<a href="https://www.nuget.org/packages/dotnext.io/2.7.0">DotNext.IO 2.7.0</a>
* [BufferWriter](https://dotnet.github.io/dotNext/api/DotNext.Buffers.BufferWriter.html) now contains extension methods that allow to use any object implementing [IBufferWriter&lt;char&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) as pooled string builder
* [IAsyncBinaryReader](https://dotnet.github.io/dotNext/api/DotNext.IO.IAsyncBinaryReader.html), [IAsyncBinaryWriter](https://dotnet.github.io/dotNext/api/DotNext.IO.IAsyncBinaryWriter.html), [PipeExtensions](https://dotnet.github.io/dotNext/api/DotNext.IO.Pipelines.PipeExtensions.html), [StreamExtensions](https://dotnet.github.io/dotNext/api/DotNext.IO.StreamExtensions.html), [SequenceBinaryReader](https://dotnet.github.io/dotNext/api/DotNext.IO.SequenceBinaryReader.html) types now containing methods for encoding/decoding primitive types, [DateTime](https://docs.microsoft.com/en-us/dotnet/api/system.datetime), [DateTimeOffset](https://docs.microsoft.com/en-us/dotnet/api/system.datetimeoffset), [Guid](https://docs.microsoft.com/en-us/dotnet/api/system.guid) to/from string representation contained in underlying stream, pipe or [sequence](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) in the binary form
* Fixed pooled memory leaks in [SequenceBinaryReader](https://dotnet.github.io/dotNext/api/DotNext.IO.SequenceBinaryReader.html)
* [TextWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.textwriter) over [IBufferWriter&lt;char&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) interface using extension method in [TextWriterSource](https://dotnet.github.io/dotNext/api/DotNext.IO.TextWriterSource.html) class

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.6.1">DotNext.Metaprogramming 2.6.1</a>
* Enabled consistent build which is recommended for SourceLink

<a href="https://www.nuget.org/packages/dotnext.reflection/2.6.1">DotNext.Reflection 2.6.1</a>
* Optimized construction of getter/setter for the reflected field
* Enabled consistent build which is recommended for SourceLink

<a href="https://www.nuget.org/packages/dotnext.threading/2.6.1">DotNext.Threading 2.6.1</a>
* Enabled consistent build which is recommended for SourceLink

<a href="https://www.nuget.org/packages/dotnext.unsafe/2.6.1">DotNext.Unsafe 2.6.1</a>
* Enabled consistent build which is recommended for SourceLink

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.6.1">DotNext.Net.Cluster 2.6.1</a>
* Enabled consistent build which is recommended for SourceLink

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.6.1">DotNext.AspNetCore.Cluster 2.6.1</a>
* Reduced memory allocation caused by replication of log entries
* Enabled consistent build which is recommended for SourceLink

# 06-14-2020
<a href="https://www.nuget.org/packages/dotnext/2.6.0">DotNext 2.6.0</a>
* More ways to create `MemoryOwner<T>`
* Removed copying of synchronization context when creating continuation for `Future` object
* Introduced APM helper methods in `AsyncDelegate` class

<a href="https://www.nuget.org/packages/dotnext.io/2.6.0">DotNext.IO 2.6.0</a>
* Improved performance of `FileBufferingWriter`
* `FileBufferingWriter` now contains correctly implemented `BeginWrite` and `EndWrite` methods
* `FileBufferingWriter` ables to return written content as [ReadOnlySequence&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1)
* Introduced `BufferWriter` class with extension methods for [IBufferWriter&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) aimed to encoding strings, primitive and blittable types
* Support of `ulong`, `uint` and `ushort` data types available for encoding/decoding in `SequenceBinaryReader` and `PipeExtensions` classes
* Ability to access memory-mapped file content via [ReadOnlySequence&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.6.0">DotNext.Metaprogramming 2.6.0</a>
* Introduced null-coalescing assignment expression
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.reflection/2.6.0">DotNext.Reflection 2.6.0</a>
* Introduced null-coalescing assignment expression
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/2.6.0">DotNext.Threading 2.6.0</a>
* Fixed race-condition caused by `AsyncTrigger.Signal` method
* `AsyncLock` now implements [IAsyncDisposable](https://docs.microsoft.com/en-us/dotnet/api/system.iasyncdisposable) interface
* `AsyncExclusiveLock`, `AsyncReaderWriterLock` and `AsyncSharedLock` now have support of graceful shutdown implemented via [IAsyncDisposable](https://docs.microsoft.com/en-us/dotnet/api/system.iasyncdisposable) interface

<a href="https://www.nuget.org/packages/dotnext.unsafe/2.6.0">DotNext.Unsafe 2.6.0</a>
* Optimized performance of methods in `MemoryMappedFileExtensions` class
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.6.0">DotNext.Net.Cluster 2.6.0</a>
* Fixed behavior of `PersistentState.DisposeAsync` so it suppress finalization correctly

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/2.6.0">DotNext.AspNetCore.Cluster 2.6.0</a>
* Respect shutdown timeout inherited from parent host in Hosted Mode
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.augmentation.fody/2.1.0">DotNext.Augmentation.Fody 2.1.0</a>
* Removed usage of obsolete methods from `Fody`
* Updated `Fody` version

# 06-01-2020
<a href="https://www.nuget.org/packages/dotnext/2.5.0">DotNext 2.5.0</a>
* Improved performance of `PooledBufferWriter`
* `MemoryAllocator<T>` now allows to allocate at least requested number of elements

<a href="https://www.nuget.org/packages/dotnext.io/2.5.0">DotNext.IO 2.5.0</a>
* Ability to represent stream as [IBufferWriter&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1)
* `FileBufferingWriter` class is one more growable buffer backed by file in case of very large buffer size

# 05-29-2020
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/2.4.1">DotNext.Metaprogramming 2.4.1</a>
* Fixed dynamic construction of tuples using `ValueTupleBuilder` class (PR [#8](https://github.com/dotnet/dotNext/pull/8))

# 05-20-2020
<a href="https://www.nuget.org/packages/dotnext/2.4.2">DotNext 2.4.2</a>
* Reduced memory allocation caused by continuations in `Future` class
* Improved performance of some methods in `MemoryRental<T>` and `DelegateHelpers` classes
* Reduced amount of memory re-allocations in `PooledBufferWriter<T>` and `PooledArrayBufferWriter<T>` classes

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
* Introduced new [MemoryOwner&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.MemoryOwner-1.html) value type that unifies working with memory and array pools
* Path MTU [discovery](https://dotnet.github.io/dotNext/api/DotNext.Net.NetworkInformation.MtuDiscovery.html)
* Pooled buffer writes: [PooledBufferWriter&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.PooledBufferWriter-1.html) and [PooledArrayBufferWriter&lt;T&gt;](https://dotnet.github.io/dotNext/api/DotNext.Buffers.PooledArrayBufferWriter-1.html)
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
* Added [AsyncTrigger](https://dotnet.github.io/dotNext/api/DotNext.Threading.AsyncTrigger.html) synchronization primitive

<a href="https://www.nuget.org/packages/dotnext.reflection/2.3.0">DotNext.Reflection 2.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/2.3.0">DotNext.Net.Cluster 2.3.0</a>
* TCP transport for Raft
* UDP transport for Raft
* Fixed bug in [PersistentState](https://dotnet.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.html) class that leads to incorrect usage of rented memory and unexpected result during replication between nodes
* Methods for handling Raft messages inside of [RaftCluster&lt;TMember&gt;](https://dotnet.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.RaftCluster-1.html) class now support cancellation via token

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
Major release of version 2.0 is completely finished and contains polished existing and new API. All libraries in .NEXT family are upgraded. Migration guide for 1.x users is [here](https://dotnet.github.io/dotNext/migration/1.html). Please consider that this version is not fully backward compatible with 1.x.

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
    1. Additional optimizations of performance in [Write-Ahead Log](https://dotnet.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.html)
    1. Fixed issue [#4](https://github.com/dotnet/dotNext/issues/4)
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
* Fixed NRE when `Dispose` method of [PersistentChannel](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.Channels.PersistentChannel-2.html) class called multiple times

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/1.2.5">DotNext.AspNetCore.Cluster 1.2.5</a>
* Fixed bug when log entry may have invalid content when retrieved from persistent audit trail. Usually this problem can be observed in case of concurrent read/write and caused by invalid synchronization of multiple file streams.

# 11-18-2019
<a href="https://www.nuget.org/packages/dotnext.threading/1.3.0">DotNext.Threading 1.3.0</a>
* [PersistentChannel](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.Channels.PersistentChannel-2.html) is added as an extension of **channel** concept from [System.Threading.Channels](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.threading.channels). It allows to use disk memory instead of RAM for storing messages passed from producer to consumer. Read more [here](https://dotnet.github.io/dotNext/features/threading/channel.html)
* [AsyncCounter](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncCounter.html) allows to simplify asynchronous coordination in producer/consumer scenario

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
* Fixed type modifier of `Current` property declared in [CopyOnWriteList&lt;T&gt;.Enumerator](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Collections.Concurrent.CopyOnWriteList-1.Enumerator.html)

# 10-31-2019
<a href="https://www.nuget.org/packages/dotnext/1.2.0">DotNext 1.2.0</a>
* Fixed memory leaks caused by methods in [StreamExtensions](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.IO.StreamExtensions.html) class
* [MemoryRental](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Buffers.MemoryRental-1.html) type is introduced to replace memory allocation with memory rental in some scenarios
* [ArrayRental](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Buffers.ArrayRental-1.html) type is extended
* Value Delegates now are protected from _dangling pointer_ issue caused by dynamic assembly loading 
* Reduced amount of memory utilized by random string generation methods
* Strict package versioning rules are added to avoid accidental upgrade to major version
* Improved performance of [AtomicEnum](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.AtomicEnum.html) methods
* Improved performance of [Atomic&lt;T&gt;](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.Atomic-1.html) using optimistic read locks
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
* [AsyncReaderWriterLock](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncReaderWriterLock.html) now supports optimistic reads

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.2.0">DotNext.Unsafe 1.2.0</a>
* [UnmanagedMemoryPool](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Buffers.UnmanagedMemoryPool-1.html) is added
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
* [ReaderWriterSpinLock](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.ReaderWriterSpinLock.html) type is introduced
* Improved performance of [UserDataStorage](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.UserDataStorage.html)

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
* Introduced [AsyncSharedLock](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncSharedLock.html) as combination of reader/write lock and semaphore
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
* Optimized methods of [Memory](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Runtime.InteropServices.Memory.html) class
* Extension methods for I/O are introduced. Now you don't need to instantiate [BinaryReader](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.io.binaryreader) or [BinaryWriter](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.io.binarywriter) for high-level parsing of stream content. Encoding and decoding of strings are fully supported. Moreover, these methods are asynchronous in contrast to methods of `BinaryReader` and `BinaryWriter`.

<a href="https://www.nuget.org/packages/dotnext.reflection/1.0.0">DotNext.Reflection 1.0.0</a>
* API is stabilized

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/1.0.0">DotNext.Metaprogramming 1.0.0</a>
* API is stabilized

<a href="https://www.nuget.org/packages/dotnext.threading/1.0.0">DotNext.Threading 1.0.0</a>
* [AsyncManualResetEvent](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncManualResetEvent.html) has auto-reset optional behavior which allows to repeatedly unblock many waiters

<a href="https://www.nuget.org/packages/dotnext.unsafe/1.0.0">DotNext.Unsafe 1.0.0</a>
* [MemoryMappedFileExtensions](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.IO.MemoryMappedFiles.MemoryMappedFileExtensions.html) allows to work with virtual memory associated with memory-mapped file using unsafe pointer or [Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.span-1) to achieve the best performance.

<a href="https://www.nuget.org/packages/dotnext.net.cluster/1.0.0">DotNext.Net.Cluster 1.0.0</a>
* Audit trail programming model is redesigned
* Persistent and high-performance Write Ahead Log (WAL) is introduced. Read more [here](https://dotnet.github.io/dotNext/features/cluster/aspnetcore.html#replication)
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
* [Timestamp](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Diagnostics.Timestamp.html) type is introduced as allocation-free alternative to [Stopwatch](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.diagnostics.stopwatch)
* [Memory](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Runtime.InteropServices.Memory.html) class now have methods for reading and writing null-terminated UTF-16 string from/to unmanaged or pinned managed memory
* Updated InlineIL dependency to 1.3.1

<a href="https://www.nuget.org/packages/dotnext.threading/0.14.0">DotNext.Threading 0.14.0</a>
* [AsyncTimer](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncTimer.html) is completely rewritten in backward-incompatible way. Wait handle are no longer used.

<a href="https://www.nuget.org/packages/dotnext.unsafe/0.14.0">DotNext.Unsafe 0.14.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.reflection/0.14.0">DotNext.Reflection 0.14.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.metaprogramming/0.14.0">DotNext.Metaprogramming 0.14.0</a>
* Small code fixes
* Updated `DotNext` dependency to 0.14.0
* Updated `Fody` dependency to 6.0.0
* Updated augmented compilation to 0.14.0

<a href="https://www.nuget.org/packages/dotnext.net.cluster/0.5.0">DotNext.Net.Cluster 0.5.0</a><br/>
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.5.0">DotNext.AspNetCore.Cluster 0.5.0</a>
* Measurement of runtime metrics are introduced and exposed through [MetricsCollector](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Net.Cluster.Consensus.Raft.MetricsCollector.html) and [HttpMetricsCollector](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Net.Cluster.Consensus.Raft.Http.HttpMetricsCollector.html) classes

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
* Fixed bug with equality comparison of **null** arrays inside of [EqualityComparerBuilder](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.EqualityComparerBuilder-1.html)
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
* Improved performance of methods declared in [EnumConverter](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.EnumConverter.html)
* Improved performance of atomic operations
* Improved performance of bitwise equality and bitwise comparison methods for value types
* Improved performance of [IsDefault](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Runtime.Intrinsics.html#DotNext_Runtime_Intrinsics_IsDefault__1_) method which allows to check whether the arbitrary value of type `T` is `default(T)`
* GetUnderlyingType() method is added to obtain underlying type of Result&lt;T&gt;
* [TypedReference](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.typedreference) can be converted into managed pointer (type T&amp;, or ref T) using [Memory](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Runtime.InteropServices.Memory.html) class

This release introduces a new feature called Value Delegates which are allocation-free alternative to regular .NET delegates. Value Delegate is a value type which holds a pointer to the managed method and can be invoked using `Invoke` method in the same way as regular .NET delegate. Read more [here](https://dotnet.github.io/dotNext/features/core/valued.html).

`ValueType<T>` is no longer exist and most of its methods moved into [BitwiseComparer](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.BitwiseComparer-1.html) class.

<a href="https://www.nuget.org/packages/dotnext.reflection/0.12.0">DotNext.Reflection 0.12.0</a>
* Ability to obtain managed pointer (type T&amp;, or `ref T`) to static or instance field from [FieldInfo](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.reflection.fieldinfo) using [Reflector](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Reflection.Reflector.html) class

<a href="https://www.nuget.org/packages/dotnext.threading/0.12.0">DotNext.Threading 0.12.0</a>
* [AsyncLazy&lt;T&gt;](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Threading.AsyncLazy-1.html) is introduced as asynchronous alternative to [Lazy&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/versions/1.x/api/system.lazy-1) class

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/0.12.0">DotNext.Metaprogramming 0.12.0</a>
* [Null-safe navigation expression](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Linq.Expressions.NullSafetyExpression.html) is introduced

<a href="https://www.nuget.org/packages/dotnext.unsafe/0.12.0">DotNext.Unsafe 0.12.0</a>
* [UnmanagedFunction](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Runtime.InteropServices.UnmanagedFunction.html) and [UnmanagedFunction&lt;R&gt;](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Runtime.InteropServices.UnmanagedFunction-1.html) classes are introduced to call unmanaged functions by pointer

<a href="https://www.nuget.org/packages/dotnext.net.cluster/0.2.0">DotNext.Net.Cluster 0.2.0</a>
<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/0.2.0">DotNext.AspNetCore.Cluster 0.2.0</a>
* Raft client is now capable to ensure that changes are committed by leader node using [WriteConcern](https://dotnet.github.io/dotNext/versions/1.x/api/DotNext.Net.Cluster.Replication.WriteConcern.html)