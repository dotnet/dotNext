Release Notes
====

# 11-24-2026
<a href="https://www.nuget.org/packages/dotnext/6.14.0">DotNext 6.14.0</a>
* Fixed LEB128 encoder: now it detects incorrect input
* Fixed UNC path encoding provided by `FileUri` class
* `FileUri.TryCreateFromFileName` returns **false** correctly when the input buffer is too small
* Finalized target is no longer returned by `SoftReference<T>`
* Introduced `ExceptionExtensions` with extensions for [Exception](https://learn.microsoft.com/en-us/dotnet/api/system.exception) class

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.14.0">DotNext.Metaprogramming 6.14.0</a>
* Fixed evaluation order in async lambda lowering

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.14.0">DotNext.Unsafe 6.14.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.14.0">DotNext.Threading 6.14.0</a>
* Introduced `AsyncCoalescer` synchronization primitive which allows to organize barriers based on the timer rather than number of participants
* Added `QueuedSynchronized<T>.IsFair` property for advanced scenarios when the whole wait queue needs to be resumed

<a href="https://www.nuget.org/packages/dotnext.io/6.14.0">DotNext.IO 6.14.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.14.0">DotNext.Net.Cluster 6.14.0</a>
* Introduced `LeadershipTicket` property and `ConfirmLeadershipAsync` method in `RaftCluster<TMember>` class. These members allow to ensure leadership of the current node without applying the read barrier
* Fixed `ObjectDisposedException` exception that can be thrown by `ForceReplicationAsync` method due to race conditions

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.14.0">DotNext.AspNetCore.Cluster 6.14.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.14.0">DotNext.MaintenanceServices 1.14.0</a>
* Updated dependencies

# 11-22-2026
<a href="https://www.nuget.org/packages/dotnext/6.13.0">DotNext 6.13.0</a>
* Introduced `Atomic.Read` and `Atomic.Write` for arbitrary enum types
* Fixed memory aliasing bug in `Atomic<T>` class
* Deprecation of `Atomic.Read` for **long**, **ulong**, and **double** data types
* Fixed nullability attributes in `Optional<T>` class
* Various performance improvements of `Optional<T>` implementation
* `SpanOwner<T>` removes allocation when passed [MemoryPool&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1) is [shared pool](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.memorypool-1.shared)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.13.0">DotNext.Metaprogramming 6.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.13.0">DotNext.Unsafe 6.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.13.0">DotNext.Threading 6.13.0</a>
* Performance improvements of optimistic read lock in `AsyncReaderWriterLock`
* Fixed correctness of `LeaseConsumer` and `LeaseProvider` when implementing a distributed lease. Changes are backward incompatible.
* Introduced `LeaseProvider.ObserveAsync` that allows to observe the lease state without waiting for the real request for renewal

<a href="https://www.nuget.org/packages/dotnext.io/6.13.0">DotNext.IO 6.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.13.0">DotNext.Net.Cluster 6.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.13.0">DotNext.AspNetCore.Cluster 6.13.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.13.0">DotNext.MaintenanceServices 1.13.0</a>
* Updated dependencies

# 11-16-2026
<a href="https://www.nuget.org/packages/dotnext/6.12.2">DotNext 6.12.2</a>
* `Variant` data type becomes more usable for serialization frameworks where AOT-compatible fast reflection is needed: `TryCopyTo` and `TrySetValue` methods are introduced.

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.12.2">DotNext.Metaprogramming 6.12.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.12.2">DotNext.Unsafe 6.12.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.12.2">DotNext.Threading 6.12.2</a>
* Merged [316](https://github.com/dotnet/dotNext/pull/316)

<a href="https://www.nuget.org/packages/dotnext.io/6.12.2">DotNext.IO 6.12.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.12.2">DotNext.Net.Cluster 6.12.2</a>
* Partially fixed [315](https://github.com/dotnet/dotNext/issues/315)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.12.2">DotNext.AspNetCore.Cluster 6.12.2</a>
* Partially fixed [315](https://github.com/dotnet/dotNext/issues/315)

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.12.2">DotNext.MaintenanceServices 1.12.2</a>
* Updated dependencies

# 11-09-2026
<a href="https://www.nuget.org/packages/dotnext/6.12.1">DotNext 6.12.1</a>
* Added **params** keyword to some methods of `PoolingArrayBufferWriter` class

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.12.1">DotNext.Metaprogramming 6.12.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.12.1">DotNext.Unsafe 6.12.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.12.1">DotNext.Threading 6.12.1</a>
* Optimized memory allocations required for timeout support in `CancellationTokenMultiplexer` class

<a href="https://www.nuget.org/packages/dotnext.io/6.12.1">DotNext.IO 6.12.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.12.1">DotNext.Net.Cluster 6.12.1</a>
* Fixed [313](https://github.com/dotnet/dotNext/issues/313)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.12.1">DotNext.AspNetCore.Cluster 6.12.1</a>
* Fixed [313](https://github.com/dotnet/dotNext/issues/313)

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.12.1">DotNext.MaintenanceServices 1.12.1</a>
* Updated dependencies

# 11-06-2026
<a href="https://www.nuget.org/packages/dotnext/6.12.0">DotNext 6.12.0</a>
* Introduced `Task.Run` overloads as a replacement of `SpawningAsyncTaskMethodBuilder` for better compatibility with Runtime Async
* `SpawningAsyncTaskMethodBuilder` skips any ambient synchronization context

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.12.0">DotNext.Metaprogramming 6.12.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.12.0">DotNext.Unsafe 6.12.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.12.0">DotNext.Threading 6.12.0</a>
* Small performance improvements of `RandomAccessCache<TValue>` class
* Introduced `YieldAsync` method in `AsyncExclusiveLock` and `AsyncReaderWriterLock` classes
* Added `SwitchToAsync` extension method for [TaskScheduler](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler) class which allows to switch task scheduler safely within the method body
* Removed memory allocations caused by continuations in async primitives

<a href="https://www.nuget.org/packages/dotnext.io/6.12.0">DotNext.IO 6.12.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.12.0">DotNext.Net.Cluster 6.12.0</a>
* Fixed [308](https://github.com/dotnet/dotNext/issues/308)
* Fixed [309](https://github.com/dotnet/dotNext/issues/309)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.12.0">DotNext.AspNetCore.Cluster 6.12.0</a>
* Fixed [309](https://github.com/dotnet/dotNext/issues/309)

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.12.0">DotNext.MaintenanceServices 1.12.0</a>
* Smallish performance improvements

# 10-29-2026
<a href="https://www.nuget.org/packages/dotnext/6.11.1">DotNext 6.11.1</a>
* Fixed `Volatile.Read` and `Volatile.Write` static extension methods for pointer-to-pointer locations, so now these methods are compatible with managed pointers too

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.11.1">DotNext.Metaprogramming 6.11.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.11.1">DotNext.Unsafe 6.11.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.11.1">DotNext.Threading 6.11.1</a>
* `RandomAccessCache<TValue>` now uses Pinned Object Heap to allocate _static_ parts of the cache when instantiated first time. Cache entries are still in unmanaged heap. This change allows to monitor memory consumption via .NET built-in metrics, while cache entry allocation has its own metrics

<a href="https://www.nuget.org/packages/dotnext.io/6.11.1">DotNext.IO 6.11.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.11.1">DotNext.Net.Cluster 6.11.1</a>
* Preparation for Runtime Async: removed `async void` methods

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.11.1">DotNext.AspNetCore.Cluster 6.11.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.11.1">DotNext.MaintenanceServices 1.11.1</a>
* Preparation for Runtime Async: removed `async void` methods

# 10-26-2026
<a href="https://www.nuget.org/packages/dotnext/6.11.0">DotNext 6.11.0</a>
* Added `ApplicationLock` class that allows only one running instance of the application, based on the exclusively opened lock file
* `Epoch` now supports deferred release of unmanaged resources through `INativeResourceManager` interface and `RecycleBin.Defer(nuint)` method, without extra allocations
* Replaced `Epoch.UnsafeClear` method with thread-safe `TryClear` and `ClearAsync` methods
* `Epoch.RecycleBin` no longer allocates memory for the deferred actions
* Added `Volatile.Read` and `Volatile.Write` static extension methods for pointer-to-pointer locations
* Added `MemoryMarshal.AsReadOnlyBytes` static extension method to represent a string as a span over bytes
* Added `IEqualityComparer<ReadOnlySpan<byte>>.Default` static extension property
* Fixed memory leaks in `BufferWriterSlim<T>.DetachOrCopyBuffer` and `BufferWriter<T>.DetachBuffer` methods

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.11.0">DotNext.Metaprogramming 6.11.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.11.0">DotNext.Unsafe 6.11.0</a>
* Introduced `Pointer<T>.IsStackAllocated` property

<a href="https://www.nuget.org/packages/dotnext.threading/6.11.0">DotNext.Threading 6.11.0</a>
* Added `RandomAccessCache<TValue>`, the unmanaged version of SIEVE-based cache with keys represented by bytes. Its entries are allocated in the native memory and reclaimed with `Epoch`
* Added `AsyncParkingLot` synchronization primitive that represents a set of wait queues shared by many lightweight asynchronous locks
* `RandomAccessCache<TKey, TValue>` uses `AsyncParkingLot` instead of per-bucket locks to reduce memory footprint. The concurrency level can be specified in the constructor
* Fixed eviction state corruption in `RandomAccessCache<TKey, TValue>`
* Fixed lost promotion and race condition between the reader and the resize process in `RandomAccessCache<TKey, TValue>`
* Improved CPU cache locality of `RandomAccessCache<TKey, TValue>`
* Fixed [304](https://github.com/dotnet/dotNext/issues/304)

<a href="https://www.nuget.org/packages/dotnext.io/6.11.0">DotNext.IO 6.11.0</a>
* Fixed potential race condition in `DiskSpacePool` that could lead to returning the same segment twice
* Removed `AppendAndCommitAsync` default method from `IAuditTrail<TEntry>` interface

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.11.0">DotNext.Net.Cluster 6.11.0</a>
* Fixed [305](https://github.com/dotnet/dotNext/issues/305)
* Fixed [307](https://github.com/dotnet/dotNext/pull/307)
* `WriteAheadLog` now produces checkpoint on every commit to ensure consistency. `FlushInterval` and `FlushOnCommit` configuration properties are deprecated and have no effect
* `WriteAheadLog.Options.Allocator` property returns default allocator if not specified explicitly

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.11.0">DotNext.AspNetCore.Cluster 6.11.0</a>
* Fixed [305](https://github.com/dotnet/dotNext/issues/305)

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.11.0">DotNext.MaintenanceServices 1.11.0</a>
* `CommandLineMaintenanceInterfaceHost` now supports custom Unix file mode for Unix Domain Socket pseudofile

# 10-13-2026
<a href="https://www.nuget.org/packages/dotnext/6.10.0">DotNext 6.10.0</a>
* Added conversion from [TypedReference](https://learn.microsoft.com/en-us/dotnet/api/system.typedreference) to `Variant` data type
* Interpolation string handlers do not box enum values anymore
* `Optional<T>` and other monadic types don't box the underlying value on `ToString` call
* Fixed [303](https://github.com/dotnet/dotNext/issues/303)
* Added static extension method for on-stack string interpolation without converting the result to **string**. Instead, it can be consumed as [ReadOnlySpan&lt;char&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.readonlyspan-1)
* Added [CompositeFormat](https://learn.microsoft.com/en-us/dotnet/api/system.text.compositeformat) interning: `IsInterned` or `Of` static extensions can return parsed template

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.10.0">DotNext.Metaprogramming 6.10.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.10.0">DotNext.Unsafe 6.10.0</a>
* Added `ExecutionStack` with extension properties and methods for execution stack inspection

<a href="https://www.nuget.org/packages/dotnext.threading/6.10.0">DotNext.Threading 6.10.0</a>
* `PersistentChannel` now uses system page size as default buffer size
* Improved performance of `CancellationTokenMultiplexer` class

<a href="https://www.nuget.org/packages/dotnext.io/6.10.0">DotNext.IO 6.10.0</a>
* `FileBufferingWriter` and `PoolingBufferedStream` now use system page size as default buffer size
* Introduced `ModernTextReader` and `ModernTextWriter` classes with all necessary overloads to implement text readers/writer

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.10.0">DotNext.Net.Cluster 6.10.0</a>
* Improved performance of `MessagingClient` and `MessageHandler` message dispatching

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.10.0">DotNext.AspNetCore.Cluster 6.10.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.10.0">DotNext.MaintenanceServices 1.10.0</a>
* Updated dependencies

# 10-06-2026
<a href="https://www.nuget.org/packages/dotnext/6.9.0">DotNext 6.9.0</a>
* Fixed stack allocation threshold in `SpanOwner`
* String interpolation helpers are available as static extension methods for [String](https://learn.microsoft.com/en-us/dotnet/api/system.string) class
* Added `SkipNulls` extension method overload for nullable value types
* Added `Flatten` extension method to skip empty values of Optional monadic type in enumerations
* Introduced `Infer` extension method to force automatic delegate type inference in C#
* Added `ReferenceCounted<T>` class which implements concurrent reference counting for a generic resource

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.9.0">DotNext.Metaprogramming 6.9.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.9.0">DotNext.Unsafe 6.9.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.9.0">DotNext.Threading 6.9.0</a>
* Fixed XML docs for `AsyncStateTracker` class
* Added collection builder support to `AsyncEventHub.EventGroup` inner type
* Fixed [302](https://github.com/dotnet/dotNext/issues/302)

<a href="https://www.nuget.org/packages/dotnext.io/6.9.0">DotNext.IO 6.9.0</a>
* Removed null check in hot execution paths for `FileReader` and `FileWriter` types
* Default buffer size for `FileReader` and `FileWriter` is equal to the system page size

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.9.0">DotNext.Net.Cluster 6.9.0</a>
* Fixed [301](https://github.com/dotnet/dotNext/issues/301)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.9.0">DotNext.AspNetCore.Cluster 6.9.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.9.0">DotNext.MaintenanceServices 1.9.0</a>
* Updated dependencies

# 09-22-2026
<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.8.1">DotNext.Net.Cluster 6.8.1</a>
* Fixed [299](https://github.com/dotnet/dotNext/issues/299)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.8.1">DotNext.AspNetCore.Cluster 6.8.1</a>
* Fixed [299](https://github.com/dotnet/dotNext/issues/299)

# 09-20-2026
<a href="https://www.nuget.org/packages/dotnext/6.8.0">DotNext 6.8.0</a>
* Added read-only concurrent access to custom struct field in `Atomic<T>` container

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.8.0">DotNext.Metaprogramming 6.8.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.8.0">DotNext.Unsafe 6.8.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.8.0">DotNext.Threading 6.8.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/6.8.0">DotNext.IO 6.8.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.8.0">DotNext.Net.Cluster 6.8.0</a>
* Added `FlushOnCommit` option to `WriteAheadLog.Options`: guarantees that a log entry is durably flushed to disk before `AppendAsync`/`CommitAsync` returns, even if not yet committed — closes a gap where a follower could lose an uncommitted entry on restart and later end up with a conflicting committed entry once a new leader replicates a different value at the same index. Trades away eager durability of the checkpoint's commit index (which is safely recomputed from committed entries on restart) for this guarantee
* Fixed a race in `WriteAheadLog`'s background flusher where the commit index could be considered "flushed" before it was actually written to the checkpoint, causing `FlushAsync` to return prematurely
* Fixed a bug where installing a snapshot that skips over a range of log indices (e.g. via `InstallSnapshot`) could leave the flusher and page-compaction logic reading uninitialized metadata for that boundary index, in rare cases causing background flushes to stall for extended periods or, more seriously, causing compaction to delete data pages still needed by log entries appended after the snapshot
* Fixed a bug in `WriteAheadLog`'s cleanup scheduling that could repeatedly re-trigger page compaction for the same snapshot on every flush cycle instead of once per snapshot advancement
* `WriteAheadLog` writes the checkpoint in a durable way on `DisposeAsync` call, no need to call `FlushAsync` explicitly before it

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.8.0">DotNext.AspNetCore.Cluster 6.8.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.8.0">DotNext.MaintenanceServices 1.8.0</a>
* Updated dependencies

# 09-11-2026
<a href="https://www.nuget.org/packages/dotnext/6.7.2">DotNext 6.7.2</a>
* Minor performance improvements of static extension methods declared in `AdvancedHelpers` class

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.7.2">DotNext.Metaprogramming 6.7.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.7.2">DotNext.Unsafe 6.7.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.7.2">DotNext.Threading 6.7.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/6.7.2">DotNext.IO 6.7.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.7.2">DotNext.Net.Cluster 6.7.2</a>
* Added synchronous access to the cached metadata via `TryGetMetadata` public method in `RaftClusterMember` class
* Fixed and merged [298](https://github.com/dotnet/dotNext/pull/298)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.7.2">DotNext.AspNetCore.Cluster 6.7.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.7.2">DotNext.MaintenanceServices 1.7.2</a>
* Updated dependencies

# 09-05-2026
<a href="https://www.nuget.org/packages/dotnext/6.7.1">DotNext 6.7.1</a>
* `FromPointer` migrated from handwritten IL code to fast reflection using [UnsafeAccessor](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafeaccessorattribute), which is more portable across .NET versions

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.7.1">DotNext.Metaprogramming 6.7.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.7.1">DotNext.Unsafe 6.7.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.7.1">DotNext.Threading 6.7.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/6.7.1">DotNext.IO 6.7.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.7.1">DotNext.Net.Cluster 6.7.1</a>
* Optimized catch up of the temporary unavailable node when it becomes available again for the leader. Outdated leader now can report last WAL index, so the leader can adjust replication position for the node much faster than just decrementing it speculatively in every replication round
* Added stricter control for the order of state machine recovery and WAL initialization

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.7.1">DotNext.AspNetCore.Cluster 6.7.1</a>
* Optimized catch up of the temporary unavailable node when it becomes available again for the leader. Outdated leader now can report last WAL index, so the leader can adjust replication position for the node much faster than just decrementing it speculatively in every replication round
* Added stricter control for the order of state machine recovery and WAL initialization
* Added appropriate XML remarks to the DI configuration methods

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.7.1">DotNext.MaintenanceServices 1.7.1</a>
* Updated dependencies

# 09-01-2026
<a href="https://www.nuget.org/packages/dotnext/6.7.0">DotNext 6.7.0</a>
* Improved performance of internal buffer reallocation in `BufferWriterSlim<T>` and `PoolingBufferWriter<T>` types
* Added comprehensive extensions for [BitArray](https://learn.microsoft.com/en-us/dotnet/api/system.collections.bitarray) class
* Added `IsZeroed` extension property for [ReadOnlySpan&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.readonlyspan-1) type which indicates that the span references a memory containing only zero bytes
* Added reversed `AndNot` binary transformation to `BinaryTransformations` class

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.7.0">DotNext.Metaprogramming 6.7.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.7.0">DotNext.Unsafe 6.7.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.7.0">DotNext.Threading 6.7.0</a>
* `AsyncEventHub` has no limit on the number of events anymore

<a href="https://www.nuget.org/packages/dotnext.io/6.7.0">DotNext.IO 6.7.0</a>
* Fixed telemetry reporting when reallocation of the internal buffer inside `FileBufferingWriter` class is requested

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.7.0">DotNext.Net.Cluster 6.7.0</a>
* Added description for `InvalidOperationException` exception that can be raised by `ICluster.WaitForLeaderAsync` when the cluster quorum is not available

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.7.0">DotNext.AspNetCore.Cluster 6.7.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.7.0">DotNext.MaintenanceServices 1.7.0</a>
* Updated dependencies

# 08-29-2026
<a href="https://www.nuget.org/packages/dotnext/6.6.5">DotNext 6.6.5</a>
* All `FromPointer` static extension methods for delegates are marked with [RequiresDynamicCodeAttribute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.requiresdynamiccodeattribute), because delegate construction from a method pointer is not supported in AOT

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.6.5">DotNext.Metaprogramming 6.6.5</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.6.5">DotNext.Unsafe 6.6.5</a>
* Improved performance of `GetPageAlignedOffset` method

<a href="https://www.nuget.org/packages/dotnext.threading/6.6.5">DotNext.Threading 6.6.5</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/6.6.5">DotNext.IO 6.6.5</a>
* Fixed [296](https://github.com/dotnet/dotNext/issues/296)

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.6.5">DotNext.Net.Cluster 6.6.5</a>
* Clamp follower commit index to the last entry delivered in `AppendEntries` Raft RPC
* Fixed freeze of `WaitForLeaderAsync` and `WaitForLeadershipAsync` methods when the node is requested to stop

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.6.5">DotNext.AspNetCore.Cluster 6.6.5</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.6.5">DotNext.MaintenanceServices 1.6.5</a>
* Updated dependencies

Also, AOT compatibility tests are added.

# 08-21-2026
<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.6.4">DotNext.Net.Cluster 6.6.4</a>
* Fixed durability and atomicity guarantees for file operations in `SimpleStateMachine` implementation

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.6.4">DotNext.AspNetCore.Cluster 6.6.4</a>
* Raft request handler aborts any incoming HTTP requests as soon as the application stop is requested. It doesn't wait for completion of the in-flight requests

# 08-20-2026
<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.6.3">DotNext.Net.Cluster 6.6.3</a>
* Added support of Raft cluster rolling updates, when a new log entry types need to be introduced

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.6.3">DotNext.AspNetCore.Cluster 6.6.3</a>
* Updated dependencies

# 08-17-2026
<a href="https://www.nuget.org/packages/dotnext/6.6.2">DotNext 6.6.2</a>
* `Constant` static extension method for [Func&lt;TResult&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.func-1) delegate provides cached version for default value of the primitive types

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.6.2">DotNext.Metaprogramming 6.6.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.6.2">DotNext.Unsafe 6.6.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.6.2">DotNext.Threading 6.6.2</a>
* Added `CancellationTokenHandle` that can be used to expose [CancellationToken](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken) to the unmanaged code efficiently

<a href="https://www.nuget.org/packages/dotnext.io/6.6.2">DotNext.IO 6.6.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.6.2">DotNext.Net.Cluster 6.6.2</a>
* Fixed [293](https://github.com/dotnet/dotNext/issues/293)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.6.2">DotNext.AspNetCore.Cluster 6.6.2</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.6.2">DotNext.MaintenanceServices 1.6.2</a>
* Updated dependencies

# 08-11-2026
<a href="https://www.nuget.org/packages/dotnext/6.6.1">DotNext 6.6.1</a>
* Optimized `RoundUp` and `RoundDown` for the case when multiplier is a power of 2

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.6.1">DotNext.Metaprogramming 6.6.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.6.1">DotNext.Unsafe 6.6.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.6.1">DotNext.Threading 6.6.1</a>
* `IndexPool` increases the chance to reuse the same index returned to the pool recently

<a href="https://www.nuget.org/packages/dotnext.io/6.6.1">DotNext.IO 6.6.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.6.1">DotNext.Net.Cluster 6.6.1</a>
* `WriteAheadLog.Options` has `NoBuffering` configuration that can be used in combination with `MemoryManagementStrategy.PrivateMemory` to enable direct disk write. This option prevents OS page cache pollution
* Randomize election timeout on failed candidacy and quorum loss
* Fix replication queue full-detection to actually detect fullness

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.6.1">DotNext.AspNetCore.Cluster 6.6.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.6.1">DotNext.MaintenanceServices 1.6.1</a>
* Updated dependencies

# 08-04-2026
<a href="https://www.nuget.org/packages/dotnext/6.6.0">DotNext 6.6.0</a>
* Added `Listen` extension method to consume [IAsyncEnumerable&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerable-1) interface in the background

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.6.0">DotNext.Metaprogramming 6.6.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.6.0">DotNext.Unsafe 6.6.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.6.0">DotNext.Threading 6.6.0</a>
* Added `AsyncMulticastSequence` class which implements [IAsyncEnumerable&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerable-1) interface for single-producer-multiple-consumers scenario. It is useful to organize async stream of events with multiple subscribers
* Minor performance improvements of `CancellationTokenMultiplexer`

<a href="https://www.nuget.org/packages/dotnext.io/6.6.0">DotNext.IO 6.6.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.6.0">DotNext.Net.Cluster 6.6.0</a>
* Removed memory allocation in the hot execution path related to the communication between cluster nodes
* Fixed [292](https://github.com/dotnet/dotNext/issues/292)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.6.0">DotNext.AspNetCore.Cluster 6.6.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.6.0">DotNext.MaintenanceServices 1.6.0</a>
* Updated dependencies

# 07-26-2026
<a href="https://www.nuget.org/packages/dotnext/6.5.0">DotNext 6.5.0</a>
* Improved performance of `Atomic<T>` type: multiple readers don't contend anymore
* Introduced write lock scope to `Atomic<T>` data type
* `ReaderWriterSpinLock` is deprecated in favor of `Atomic<T>` struct
* Fixed AOT compatibility for `BoxedValue<T>` and `UserDataStorage` types
* Removed lock contention when reading from `UserDataStorage`
* Improved performance of `TypeMap` and `ConcurrentTypeMap` types
* `BufferWriterSlim<T>` can be constructed with a fixed capacity

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.5.0">DotNext.Metaprogramming 6.5.0</a>
* All methods accepting variadic arguments as `params Expression[] args` now changed to `params IEnumerable<Expression> args`
* `AsDynamic()` extension method is deprecated in favor of `Quoted` extension property and extension operators

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.5.0">DotNext.Unsafe 6.5.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.5.0">DotNext.Threading 6.5.0</a>
* Improved performance of `AsyncStateTracker` type
* Perf improvements: `ManualResetCompletionSource` internals moved to `BoxedValue<T>` type to avoid redundant type check

<a href="https://www.nuget.org/packages/dotnext.io/6.5.0">DotNext.IO 6.5.0</a>
* Introduced `Limit` extension method for all classes implementing [IBufferWriter&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) interface. The method returns the wrapper that limits the capacity of the underlying writer
* `EndPointFormatter` type is moved from `DotNext.Net.Cluster` library

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.5.0">DotNext.Net.Cluster 6.5.0</a>
* `EndPointFormatter` type is moved to `DotNext.IO` library

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.5.0">DotNext.AspNetCore.Cluster 6.5.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.5.0">DotNext.MaintenanceServices 1.5.0</a>
* Updated dependencies

# 07-12-2026
<a href="https://www.nuget.org/packages/dotnext/6.4.1">DotNext 6.4.1</a>
* Fixed race conditions occurred in async methods of the streams produced by `StreamSource` factory methods
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.4.1">DotNext.Metaprogramming 6.4.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.4.1">DotNext.Unsafe 6.4.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.4.1">DotNext.Threading 6.4.1</a>
* Reduced memory allocations in `ManualResetCompletionSource` class. **short** version token is no longer allocated on every `Reset` call. Additionally, even if timeout is happened, the internal timer can be reused again. It means that `ManualResetCompletionSource` class, it's derived classes and async locks do not allocate on all execution paths including worst cases
* `ManualResetCompletionSource.Reset` now throws an exception when the caller tries to reuse the completion source in the invalid state. Check `Reset` method remarks for more information

<a href="https://www.nuget.org/packages/dotnext.io/6.4.1">DotNext.IO 6.4.1</a>
* Fixed race conditions occurred in async methods of `FileReader` and `FileWriter` classes
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.4.1">DotNext.Net.Cluster 6.4.1</a>
* Replication loop on leader doesn't allocate memory anymore when heartbeat timeout is happened. This is possible due to performance improvements in `ManualResetCompletionSource` class mentioned above
* Fixed issue when TCP multiplexer can hang

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.4.1">DotNext.AspNetCore.Cluster 6.4.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.4.1">DotNext.MaintenanceServices 1.4.1</a>
* Updated dependencies

# 06-30-2026
<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.4.0">DotNext.Net.Cluster 6.4.0</a>
* Fixed potential [NullReferenceException](https://learn.microsoft.com/en-us/dotnet/api/system.nullreferenceexception) in `WriteAheadLog` constructor

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.4.0">DotNext.AspNetCore.Cluster 6.4.0</a>
* Updated dependencies

# 06-22-2026
<a href="https://www.nuget.org/packages/dotnext.threading/6.4.0">DotNext.Threading 6.4.0</a>
* Added `AsyncStateTracker` synchronization primitive that allows to produce a stream of state changes

# 06-02-2026
<a href="https://www.nuget.org/packages/dotnext/6.3.0">DotNext 6.3.0</a>
* Added `SequenceEqual` and `SequenceCompareTo` extension methods for [ReadOnlySequence&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) type

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.3.0">DotNext.Metaprogramming 6.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.3.0">DotNext.Unsafe 6.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.3.0">DotNext.Threading 6.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.io/6.3.0">DotNext.IO 6.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.3.0">DotNext.Net.Cluster 6.3.0</a>
* For single node setup, Raft node starts as leader without waiting for the candidate timeout

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.3.0">DotNext.AspNetCore.Cluster 6.3.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.3.0">DotNext.MaintenanceServices 1.3.0</a>
* Updated dependencies

# 05-20-2026
<a href="https://www.nuget.org/packages/dotnext.threading/6.2.3">DotNext.Threading 6.2.3</a>
* `PendingTaskInterruptedException.Reason` property returns `InnerException` if another reason is not defined

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.2.3">DotNext.Net.Cluster 6.2.3</a>
* Improved logging and error reporting in `WriteAheadLog` and `RaftCluster` classes in case of critical failures

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.2.3">DotNext.AspNetCore.Cluster 6.2.3</a>
* Updated dependencies

# 05-17-2026
<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.2.2">DotNext.Net.Cluster 6.2.2</a>
* Fix: snapshot file produced by `SimpleStateMachine` is opened with `FileOptions.Asynchronous` flag

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.2.2">DotNext.AspNetCore.Cluster 6.2.2</a>
* Updated dependencies

# 05-12-2026
<a href="https://www.nuget.org/packages/dotnext/6.2.1">DotNext 6.2.1</a>
* Improved performance of `Number.RoundUp` extension method

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.2.1">DotNext.Metaprogramming 6.2.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.2.1">DotNext.Unsafe 6.2.1</a>
* Improved performance of views produced by extension methods in `MemoryMappedFileExtensions` class

<a href="https://www.nuget.org/packages/dotnext.threading/6.2.1">DotNext.Threading 6.2.1</a>
* `ScheduleAsync` methods are now available as static extension methods for [TaskScheduler](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler) class

<a href="https://www.nuget.org/packages/dotnext.io/6.2.1">DotNext.IO 6.2.1</a>
* `FileBufferingWriter` uses memory-mapped file rather than private memory to represent the written content if it's larger than memory threshold

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.2.1">DotNext.Net.Cluster 6.2.1</a>
* Improved accuracy of `WriteAheadLog` metrics
* Added `ReplicationLag` configuration property for Raft nodes
* Lease-based linearizable read is disabled by default for optimization purposes. Use `IsLeaderLeaseEnabled` configuration property to enable it
* Fixed heartbeat timeout: it should be less than the lower election timeout to satisfy Timing Requirement in Raft

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.2.1">DotNext.AspNetCore.Cluster 6.2.1</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.2.1">DotNext.MaintenanceServices 1.2.1</a>
* Updated dependencies

# 05-07-2026
This release contains major update of Raft implementation: leader can move forward to the next heartbeat round when the majority committed the log entry. Before `6.2.0`, leader was able to commit the log entry when the majority confirmed the replication, however, for the next heartbeat round it had to wait all cluster members. The overall latency was impacted. Now, slow cluster member cannot impact the latency. The core of the replication is significantly modified to achieve this behavior:
1. WAL binary format remains the same, so you can migrate existing data in the log smoothly
2. Cluster configuration storage format is changed. It's not backward compatible
3. Wire protocol for communication between cluster nodes is changed. It's not backward compatible

<a href="https://www.nuget.org/packages/dotnext/6.2.0">DotNext 6.2.0</a>
* Added `T.ZeroBytes` static extension property which returns zero bytes for particular numeric type
*

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.2.0">DotNext.Metaprogramming 6.2.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.2.0">DotNext.Unsafe 6.2.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.threading/6.2.0">DotNext.Threading 6.2.0</a>
* Increased chance of object reusability when it's rented from and returned to `BoundedObjectPool<T>`
* Added `CancellationToken.Combine` static extension method which allows to combine multiple cancellation tokens without `CancellationTokenMultiplexer` instance
* Fixed documentation for SIEVE cache: [283](https://github.com/dotnet/dotNext/pull/283)

<a href="https://www.nuget.org/packages/dotnext.io/6.2.0">DotNext.IO 6.2.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.2.0">DotNext.Net.Cluster 6.2.0</a>
* Major performance improvement: leader advances forward to the next heartbeat round when the majority is committed the log entry, without waiting for the rest of nodes
* Major performance improvement: AppendEntries RPC message doesn't carry cluster configuration data anymore, which reduces the network overhead
* Introduced different types of read barriers in Raft
* Cluster configuration system is now tightly integrated with WAL and replication mechanism
* `IRaftCluster.ReplicateAsync` method return type is changed from `ValueTask<bool>` to `ValueTask` for simplicity
* Smallish perf improvements of `WriteAheadLog` class

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.2.0">DotNext.AspNetCore.Cluster 6.2.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.2.0">DotNext.MaintenanceServices 1.2.0</a>
* Updated dependencies

# 03-29-2026
<a href="https://www.nuget.org/packages/dotnext/6.1.0">DotNext 6.1.0</a>
* Fixed cancellation support in `BufferWriterSlim<T>`
* `BoxedValue<T>` cannot be derived anymore (which was a bug)

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.1.0">DotNext.Metaprogramming 6.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.1.0">DotNext.Unsafe 6.1.0</a>
* Added cleanup callback for `OpaqueValue<T>` that can be passed to the unmanaged code

<a href="https://www.nuget.org/packages/dotnext.threading/6.1.0">DotNext.Threading 6.1.0</a>
* Added alternate lookup to `RandomAccessCache<TKey, TValue>` class
* Fixed false positive when the object cannot be returned to the object pool even if there is a free space in it

<a href="https://www.nuget.org/packages/dotnext.io/6.1.0">DotNext.IO 6.1.0</a>
* Updated dependencies

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.1.0">DotNext.Net.Cluster 6.1.0</a>
* Fixed memory leak [280](https://github.com/dotnet/dotNext/pull/280)
* Refresh election timer when vote is granted [281](https://github.com/dotnet/dotNext/pull/281)

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.1.0">DotNext.AspNetCore.Cluster 6.1.0</a>
* Updated dependencies

# 03-09-2026
.NEXT 6.0.0 has been released! Migration guide is [here](https://dotnet.github.io/dotNext/migration/4.html). All changes are mostly driven by [Extension Members](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14#extension-members) feature in C# 14. Most of the things in .NEXT now expressed naturally as extensions for existing .NET classes.

<a href="https://www.nuget.org/packages/dotnext/6.0.0">DotNext 6.0.0</a>
* Introduced convenient operators to work with spans: `>>>` for safe copying and `%` for trimming
* `BufferWriterSlim<T>` now implements [IBufferWriter&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) interface which allows to use it in generic scenarios
* Greatly improved performance of bit vector conversion methods exposed by `Number` class
* Removed memory allocation caused by `Bind` extension methods
* Added convenient static extension methods to [Stream](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream) class
* Improved AOT compatibility
* Introduced `Variant` type that represents the functionality similar to [TypedReference](https://learn.microsoft.com/en-us/dotnet/api/system.typedreference) but without related compiler restrictions
* Added **allows ref struct** anti-constraint to many parts of the library
* Improved performance of `ConcurrentTypeMap` class
* Built-in delegate types from .NET are extended with static extension methods
* Added static extensions to [ArgumentOutOfRangeException](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception) class to cover more validation scenarios
* Introduced `Enum<T>` to enable the power of the Generic Math when working with enums
* All zero-alloc and encoding interpolation string handlers now in one place: `DotNext.Text` namespace
* Added `+=` operator overloading to `SpanWriter<T>` and `BufferWriterSlim<T>` types
* Added cancellation token support to [Lock](https://learn.microsoft.com/en-us/dotnet/api/system.threading.lock) and [Thread](https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread) classes

<a href="https://www.nuget.org/packages/dotnext.metaprogramming/6.0.0">DotNext.Metaprogramming 6.0.0</a>
* Expression nodes can be combined with built-in operators: `+`, `-`, `/`, etc.

<a href="https://www.nuget.org/packages/dotnext.unsafe/6.0.0">DotNext.Unsafe 6.0.0</a>
* `OpaqueValue<T>` type is introduced to unify the way of passing value types and reference types to unmanaged code. It's especially useful for passing .NET data types to the unmanaged callbacks implemented in C#
* Added static extension properties to `MemoryAllocator<T>` delegate to expose unmanaged memory allocators

<a href="https://www.nuget.org/packages/dotnext.threading/6.0.0">DotNext.Threading 6.0.0</a>
* `CancellationTokenMultiplexer` has improved CTS pooling mechanism
* Introduced fast bounded object pool
* Significantly reduced contention overhead in async locks

<a href="https://www.nuget.org/packages/dotnext.io/6.0.0">DotNext.IO 6.0.0</a>
* Added static extension methods to [Stream](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream) class
* Improved integration between `IJsonSerializable<T>` interface and JSON serialization infrastructure in .NET

<a href="https://www.nuget.org/packages/dotnext.net.cluster/6.0.0">DotNext.Net.Cluster 6.0.0</a>
* Old WAL implementation represented by `MemoryBasedStateMachine` and `DiskBasedStateMachine` classes is completely removed in favor of `WriteAheadLog` class
* Improved performance of the log entry serialization
* Improved AOT compatibility

<a href="https://www.nuget.org/packages/dotnext.aspnetcore.cluster/6.0.0">DotNext.AspNetCore.Cluster 6.0.0</a>
* Improved AOT compatibility

<a href="https://www.nuget.org/packages/dotnext.maintenanceservices/1.0.0">DotNext.MaintenanceServices 1.0.0</a>
* Upgrade to `System.CommandLine` stable release
* Stabilized public API surface