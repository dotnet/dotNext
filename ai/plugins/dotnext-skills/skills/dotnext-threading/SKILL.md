---
name: dotnext-threading
description: Async synchronization primitives from the DotNext.Threading NuGet package - async
  exclusive and reader/writer locks (with sync acquisition for mixed callers), auto/manual reset
  events, countdown event, barrier, AsyncTrigger, awaiting WaitHandle/CancellationToken, and
  custom primitives via QueuedSynchronizer<T>. Use when writing async .NET code that needs mutual
  exclusion or signaling without blocking threads, or that must await a WaitHandle or a
  CancellationToken; and when converting lock/Monitor, Monitor.Wait/Pulse, ReaderWriterLockSlim,
  ManualResetEventSlim, AutoResetEvent, CountdownEvent, Barrier or WaitHandle.WaitOne to async,
  e.g. after "CS1996 cannot await in the body of a lock statement".
license: MIT
metadata:
  dotnext-version: "6.x"
---

# Async Programming Patterns and Primitives

## Before you start

1. Check which version of `DotNext.Threading` the project references (`.csproj` or
   `Directory.Packages.props`). This skill describes 6.x. For older versions, verify each
   member you use against the XML docs/IntelliSense of the referenced version.
2. This skill is applicable in the following scenarios:
   - Writing new async code
   - Converting existing synchronous code

All types live in the `DotNext.Threading` namespace or nested namespaces.

## Use Cases

- Use `AsyncExclusiveLock` if you need exclusive lock behavior in async code, or you need async-friendly replacement of **lock** or `System.Threading.Monitor`. Consider this type as well when `System.Threading.Channels.Channel<T>` is used to organize the behavior similar to exclusive lock: once the item is placed in the channel, it cannot be removed or canceled without the actual processing by the consumer, but `AsyncExclusiveLock` supports cancellation of the suspended caller
- Use `AsyncReaderWriterLock` if you need reader/writer lock behavior in async code, or you need async-friendly replacement of `System.Threading.ReaderWriterLock` or `System.Threading.ReaderWriterLockSlim` classes.
- Use `AsyncAutoResetEvent` or `AsyncManualResetEvent` for signal-based synchronization in async code, or as async-friendly replacement of `System.Threading.AutoResetEvent` or `System.Threading.ManualResetEvent` classes.
- Use `AsyncCountdownEvent` if you need async-friendly replacement of `System.Threading.CountdownEvent` class.
- Use `AsyncBarrier` if you need async-friendly replacement of `System.Threading.Barrier` class. Post-phase handling is provided via `PostPhase` virtual method, which replaces `postPhaseAction` delegate in the constructor of `System.Threading.Barrier` class.
- Use `AsyncTrigger` to suspend and resume async flows on demand, or as async-friendly replacement of `Monitor.Wait`/`Monitor.Pulse`/`Monitor.PulseAll`: `Pulse` → `Signal()`, `PulseAll` → `Signal(resumeAll: true)`, `Wait` → `WaitAsync(token)`.
  - The trigger is not tied to a lock and has no memory: `Signal()` resumes only callers that are already waiting and returns **false** if there are none. Where a signal must not be lost, use `AsyncAutoResetEvent` or `AsyncManualResetEvent` instead.
  - For "wait until condition" loops (`while (!condition) Monitor.Wait(obj)`), use `SpinWaitAsync(condition, token)`. The condition implements `DotNext.ISupplier<bool>` (a struct avoids allocation). It is checked before suspending, and a signal resumes the caller only if the condition is true at that moment.
  - `SignalAndWaitAsync(resumeAll, throwOnEmptyQueue, token)` resumes waiters and suspends the caller in one step, the async equivalent of the `Pulse` + `Wait` handoff.
- Use `await AsyncBridge.WaitAsync(CancellationToken, bool)` to replace `CancellationToken.WaitHandle.WaitOne()` synchronous calls to wait for the cancellation in async methods
- Use `await AsyncBridge.WaitAsync(WaitHandle, TimeSpan, CancellationToken)` or `await AsyncBridge.WaitAsync(WaitHandle, CancellationToken)` to replace `WaitHandle.WaitOne()` variations of synchronous calls in async methods
- If custom synchronization is needed for async scenarios, consider `QueuedSynchronizer<T>` as a base class

Consider [rules](references/common_rules.md) when using these primitives.

## Migration Tips
Read this when existing code uses blocking synchronization and needs to become async.
Typical triggers: `CS1996` (await inside `lock`), thread-pool starvation, or `.Wait()`/`.Result` in async code.

1. Find every access to the protected state, not just the method that failed to compile. All of them must
   switch to the new primitive in the same change. Protecting the same state with the old primitive in some places
   and the new one in others leaves it unprotected.
2. Replace the primitive and convert each critical section to the `await ...; try { } finally { Release(); }` pattern.
3. Make the containing methods `async` and return `ValueTask`/`Task`. Propagate `async` up the call chain.
   Do not work around it with `.Result`, `.Wait()` or `GetAwaiter().GetResult()`.
   **Callers that can't become async yet** (public sync API, sync interface implementations, `Dispose()`): if the
   primitive is `AsyncExclusiveLock` or `AsyncReaderWriterLock`, keep those callers synchronous on the *same* lock
   instance with `TryAcquire(timeout)` / `TryEnterReadLock(timeout)` / `TryEnterWriteLock(timeout)`. Use them instead of
   blocking on the async overload. This allows migrating one call chain at a time. See "Mixed sync and async callers"
   below for the rules. Note that `Monitor.Enter`-style unbounded waits become a timeout plus a `bool` check.
4. Add a `CancellationToken` parameter and flow it into every acquisition.
5. Check the timeout semantics: `Monitor.TryEnter(obj, timeout)` → `TryAcquireAsync(timeout)` (returns `bool`).
   `AcquireAsync(timeout)` throws `TimeoutException` instead.
6. Check for reentrancy (below) before finishing.
7. If the owner type is `IDisposable`, dispose the new primitive there, preferably via `IAsyncDisposable`.

## Mixed sync and async callers

`AsyncExclusiveLock` and `AsyncReaderWriterLock` can also be acquired synchronously. One lock instance can then
serve callers that have to stay synchronous (sync interface implementations, `Dispose()`, legacy APIs, code
you are migrating gradually) alongside async callers. Sync and async holders exclude each other and hand the
lock over in both directions.

| Lock | Blocking, with thread ownership | Non-blocking attempt, no thread ownership |
|---|---|---|
| `AsyncExclusiveLock` | `TryAcquire(timeout, token)` | `TryAcquire()` |
| `AsyncReaderWriterLock` | `TryEnterReadLock(timeout, token)`, `TryEnterWriteLock(timeout, token)` | `TryEnterReadLock()`, `TryEnterWriteLock()`, `TryUpgradeToWriteLock()`, `TryEnterWriteLock(in stamp)` |

Release is always `Release()`, the same as for async acquisition.

```csharp
public void Flush() // must stay synchronous
{
    if (!syncRoot.TryAcquire(TimeSpan.FromSeconds(5)))
        throw new TimeoutException();

    try
    {
        FlushCore();
    }
    finally
    {
        syncRoot.Release();
    }
}
```

- **They return `bool` and never throw on timeout or cancellation.** A canceled token also returns `false`.
  Check the result every time.
- **The blocking overloads block the calling thread.** Use them only where the caller really can't be async, always with a
  finite timeout, and never from async code: there, `await` the async overload instead. Blocking thread-pool threads
  while async holders need the pool to finish can starve the pool.
- **Release on the same thread** when the lock was acquired with the blocking overloads. They record the owning thread.
- **Recursion detection works only between blocking sync acquisitions on the same thread** (`LockRecursionException`).
  A sync acquisition on a thread that already holds the lock via `await`, or the other way round, is not detected and
  just waits: `false` after the timeout for sync, forever for async.
- **`TryUpgradeToWriteLock()` that returns `false` has already released the caller's read lock.** Don't call `Release()`
  for the read lock afterward. Reacquire if you still need it.

## Canonical example

```csharp
using DotNext.Threading;

public sealed class Inventory : IAsyncDisposable
{
    private readonly AsyncExclusiveLock syncRoot = new();
    private readonly Dictionary<string, int> stock = new();

    public async ValueTask AddAsync(string item, int count, CancellationToken token)
    {
        await syncRoot.AcquireAsync(token);
        try
        {
            AddCore(item, count); // helper assumes the lock is held; the lock is not reentrant
        }
        finally
        {
            syncRoot.Release();
        }
    }

    public async ValueTask<bool> TryRemoveAsync(string item, int count, TimeSpan timeout, CancellationToken token)
    {
        if (!await syncRoot.TryAcquireAsync(timeout, token))
            return false; // timed out; the lock is not held, so don't release it

        try
        {
            if (!stock.TryGetValue(item, out var current) || current < count)
                return false;

            stock[item] = current - count;
            return true;
        }
        finally
        {
            syncRoot.Release();
        }
    }

    private void AddCore(string item, int count)
        => stock[item] = stock.GetValueOrDefault(item) + count;

    public ValueTask DisposeAsync() => syncRoot.DisposeAsync();
}
```

## Don't use this skill for

- CPU-bound parallelism → `Parallel`, PLINQ.
- Producer/consumer queues → `System.Threading.Channels`
- Code that is fully synchronous, never awaits while holding the lock, and shares no state with async code.
  `lock` is fine there and faster.

## Pitfalls

- **Reentrancy.** `lock`/`Monitor` and `ReaderWriterLockSlim` (with `LockRecursionPolicy.SupportsRecursion`) allow the
  owning thread to re-enter. DotNext async locks do not. Async acquisition has no thread owner, so re-entering
  simply waits forever. Look for public locked methods calling other public locked methods, and refactor them into
  lock-taking entry points plus private helpers that assume the lock is held.
- **Thread affinity is gone.** After `await`, the code may continue on another thread. A lock acquired
  asynchronously can be released from any thread. Don't keep thread-local or `[ThreadStatic]` assumptions inside the critical section.
- **`AsyncLock.AcquireLockAsync(obj)` restrictions.** It throws `InvalidOperationException` for `ReaderWriterLockSlim`,
  `WaitHandle` and `ReaderWriterLock` instances, and for objects on the runtime's frozen (non-GC) heap such as
  string literals. Never lock on strings. For `SemaphoreSlim`, `AsyncExclusiveLock`, `AsyncReaderWriterLock`
  and `AsyncSharedLock` it uses the object itself as the lock. When in doubt, use an explicit `AsyncExclusiveLock` field.
- **Don't keep `lock` on sync paths that share state with async ones.** If a code path is purely synchronous and hot,
  it may be tempting to keep `lock` there. That is only safe if no async path touches the same state. Otherwise, use
  the sync API of the new lock (step 3).
- **Upgradeable read locks.** `ReaderWriterLockSlim.EnterUpgradeableReadLock` keeps the read lock while waiting for the
  upgrade. `AsyncReaderWriterLock` upgrades by releasing the read lock first, so other writers may run in between. A failed
  `TryUpgradeToWriteLock()` / `TryUpgradeToWriteLockAsync()` leaves the caller holding nothing. Re-validate the state
  you read before the upgrade.
- **Performance trade-off.** Async locks don't block threads while waiting, which improves throughput under contention,
  at the cost of higher latency per acquisition than `lock`. For short, fully synchronous critical sections that never
  share state with async code, `lock` remains faster.