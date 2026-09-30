---
name: dotnext-concurrency
description: Concurrent data structures and allocation-free async building blocks from the
  DotNext.Threading NuGet package - AsyncLazy, TaskCompletionPipe, reusable
  ValueTaskCompletionSource, CancellationTokenMultiplexer, AsyncCounter (throttling),
  AsyncStateTracker, distributed leases, BoundedObjectPool and the SIEVE-based RandomAccessCache.
  Use when replacing Lazy<T> or Lazy<Task<T>> with async initialization; when reducing
  allocations of TaskCompletionSource, linked CancellationTokenSource or Task.WhenEach in hot
  paths; when implementing rate limiting, change notification streams, an object pool, an
  LRU/concurrent cache, or a distributed lease.
license: MIT
metadata:
  dotnext-version: "6.x"
---

# Concurrent data structures

## Before you start

1. Check which version of `DotNext.Threading` the project references (`.csproj` or
   `Directory.Packages.props`). This skill describes 6.x. For older versions, verify each
   member you use against the XML docs/IntelliSense of the referenced version.
2. The types below are in several namespaces. Add the `using` shown next to each type.

## Use Cases

- Consider `AsyncLazy<T>` (`DotNext.Threading`) as async-friendly replacement of `System.Lazy<T>` class:
  - Failures are cached unless `resettable: true`, and the first caller's token cancels the computation for everyone
- Consider `TaskCompletionPipe<T>` (`DotNext.Threading.Tasks`) as alternative to `Task.WhenEach` method call in the following cases
  - There is no way to supply a list of tasks to be awaited in the form of the list. Pipe supports adding new tasks dynamically via `Add` method
  - To reuse the same pipe between enumerations (which leads to reduced memory allocation) by calling `Reset` method
  - Ability to attach user data to every task to be tracked
- Consider `ValueTaskCompletionSource` or `ValueTaskCompletionSource<T>` (`DotNext.Threading.Tasks`) as alternative to `System.Threading.Tasks.TaskCompletionSource` or `System.Threading.Tasks.TaskCompletionSource<T>` classes in cases when the source can be reused between completions to reduce memory allocation
  -  Each `ValueTask` or `ValueTask<T>` it produces must be awaited exactly once and never after `Reset()`, or the consumer sees the next completion or an exception.
- Consider `CancellationTokenMultiplexer` (`DotNext.Threading`) when multiple cancellation tokens need to be linked with each other and/or with the timeout in hot execution paths to replace memory allocation with pooling
- Consider `AsyncCounter` (`DotNext.Threading`) to organize rate limiting or throttling. Back-pressure is controlled by `HasConcurrencyLimit` and `ConcurrencyLevel` properties inherited from `QueuedSynchronizer` base class
- Consider `AsyncStateTracker` (`DotNext.Threading`) to track object changes asynchronously. It can be used to implement `System.Collections.Generic.IAsyncEnumerable<T>` interface to supply changed versions of the object. The producer calls `TryAdvance()` on each change and `TryComplete()` at the end. The consumer takes `CurrentState` and awaits `WaitNextAsync(state, token)`. That returns **false** once the tracker is completed. After it returns **true**, read the resource and take `CurrentState` again.
- Consider `LeaseConsumer` and `LeaseProvider<TMetadata>` (`DotNext.Threading.Leases`) abstract types for client-side and server-side distributed leases accordingly
  - `LeaseProvider<TMetadata>` needs `GetStateAsync` and `TryUpdateStateAsync` implemented over the shared storage (DB, blob, etc.).
  - `LeaseConsumer` needs `TryAcquireCoreAsync`, `TryRenewCoreAsync` and `ReleaseCoreAsync`, usually as calls to the provider.
- Object pooling can be organized with `BoundedObjectPool<T>` (`DotNext.Collections.Concurrent`) instead of a custom pool implementation
  - `TryGet()` returns null when the pool is empty, so the caller creates the object itself.
  - `TryReturn()` returns false when the pool is full, so the caller must dispose or drop the object.
  - The capacity is rounded to a power of 2.
- Replace LRU cache with async-friendly SIEVE algorithm implementation exposed by `RandomAccessCache<TKey, TValue>` (`DotNext.Runtime.Caching`) class
  - SIEVE has no contention over concurrent readers
  - SIEVE is not scan resistant: enumerating the cache doesn't update the recency of entries
  - It doesn't behave like dictionary. Instead, it exposes read or write session, which guarantee liveness of the cache entry during read or modification in case of concurrent eviction.
  - Use [this example](https://github.com/dotnet/dotNext/blob/master/src/examples/RandomAccessCacheBenchmark/Program.cs) to see how to consume its API.
