# 02-10-2026
<a href="https://www.nuget.org/packages/dotnext.io/5.26.3">DotNext.IO 5.26.3</a>
* Fixed incorrect position adjustment in `PoolingBufferedStream` class if internal buffer is not empty and flush is called

# 01-14-2026
<a href="https://www.nuget.org/packages/dotnext.threading/5.26.3">DotNext.Threading 5.26.3</a>
* Fixed race condition in `CancellationTokenMultiplexer` class

# 01-11-2026
<a href="https://www.nuget.org/packages/dotnext/5.26.3">DotNext 5.26.3</a>
* Fixed partial Base64 decoding in `Base64Decoder` class

# 01-09-2026
<a href="https://www.nuget.org/packages/dotnext/5.26.2">DotNext 5.26.2</a>
* Fixed race condition in `Epoch` class

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/5.26.2">DotNext.Metaprogramming 5.26.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/5.26.2">DotNext.Unsafe 5.26.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/5.26.2">DotNext.Threading 5.26.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/5.26.2">DotNext.IO 5.26.2</a>
* Fixed handling of small buffer in the default methods of `IAsyncBinaryWriter` interface

<a href="https://www.nuget.org/packages/dotnext.net.cluster/5.26.2">DotNext.Net.Cluster 5.26.2</a>
* Optimized write performance of `WriteAheadLog` class when the length of the custom log entry is not known

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/5.26.2">DotNext.AspNetCore.Cluster 5.26.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/0.8.0">DotNext.MaintenanceServices 0.8.0</a>
* Migration to `System.CommandLine` release package

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
* Improved support of timeouts in `CancellationTokenMultiplexer`. The scope object now has explicit property to detect whether the multiplexed token source is canceled due to timeout

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
