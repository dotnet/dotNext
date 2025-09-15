Asynchronous Locks
====
Lock acquisition operation may blocks the caller thread. Reader/writer lock from .NET library doesn't have async versions of lock acquisition methods as well as [Monitor](https://docs.microsoft.com/en-us/dotnet/api/system.threading.monitor). To avoid this, DotNext Threading library provides asynchronous non-blocking alternatives of these locks.

> [!CAUTION]
> Non-blocking and blocking locks are two different worlds. It is not recommended to mix these API in the same part of application. The lock acquired with blocking API located in [Lock](xref:DotNext.Threading.Lock), [Monitor](https://docs.microsoft.com/en-us/dotnet/api/system.threading.monitor) or [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim) is not aware about the lock acquired asynchronously with [AsyncLock](xref:DotNext.Threading.AsyncLock), [AsyncExclusiveLock](xref:DotNext.Threading.AsyncExclusiveLock) or [AsyncReaderWriterLock](xref:DotNext.Threading.AsyncReaderWriterLock). The only exception is [SemaphoreSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) because it contains acquisition methods in blocking and non-blocking manner at the same time.

All non-blocking synchronization mechanisms are optimized in terms of memory allocations. If lock acquisitions are not caused in the same time from different application tasks running concurrently then heap allocation associated with waiting queue will not happen.

Asynchronous locks don't rely on the caller thread. The caller thread never blocks so there is no concept of lock owner thread. As a result, these locks are not reentrant.

It is hard to detect root cause of deadlocks occurred by asynchronous locks so use them carefully.

[AsyncLock](xref:DotNext.Threading.AsyncLock) is a unified representation of the all supported asynchronous locks:
* Exclusive lock
* [Shared lock](xref:DotNext.Threading.AsyncSharedLock)
* Reader lock
* Writer lock
* Semaphore

The only one synchronization object can be shared between blocking and non-blocking representations of the lock.
```csharp
using DotNext.Threading;
using System.Threading;

var semaphore = new SemaphoreSlim(1, 1);
var syncLock = Lock.Semaphore(semaphore);
var asyncLock = AsyncLock.Semaphore(semaphore);

//thread #1
using (syncLock.Acquire())
{

}

//thread #2
using (await asyncLock.AcquireAsync(CancellationToken.None))
{

}
```

`AsyncLock` implementing [IAsyncDisposable](https://docs.microsoft.com/en-us/dotnet/api/system.iasyncdisposable) interface for graceful shutdown if supported by underlying lock type. The following lock types have graceful shutdown:
* [AsyncExclusiveLock](./exclusive.md)
* [AsyncReaderWriterLock](./rwlock.md)
* [AsyncSharedLock](xref:DotNext.Threading.AsyncExclusiveLock)
* [AsyncExchanger](./exchanger.md)

Details of graceful shutdown described in related articles.

# Built-in Reader/Writer Synchronization
Exclusive lock may not be applicable due to performance reasons for some data types. For example, exclusive lock for dictionary or list is redundant because there are two consumers of these objects: writers and readers.

.NEXT Threading library provides several extension methods for more granular control over synchronization of any reference type:
* `AcquireReadLockAsync` acquires reader lock asynchronously
* `AcquireWriteLockAsync` acquires exclusive lock asynchronously

These methods allow to turn any thread-unsafe object into thread-safe object with precise control in context of multithreading access.

```csharp
using System.Text;
using static DotNext.Threading.AsyncLockAcquisition;

var builder = new StringBuilder();

//reader
using (AcquireReadLockAsync(builder, CancellationToken.None))
{
    Console.WriteLine(builder.ToString());
}

//writer
using (AcquireWriteLockAsync(builder, CancellationToken.None))
{
    builder.Append("Hello, world!");
}
```

For more information check extension methods inside the [AsyncLockAcquisition](xref:DotNext.Threading.AsyncLockAcquisition) class.

# Custom synchronization primitive
[QueuedSynchronizer&lt;TContext&gt;](xref:DotNext.Threading.QueuedSynchronizer`1) provides low-level infrastructure for writing custom synchronization primitives for asynchronous code. It uses the same [synchronization engine](xref:DotNext.Threading.QueuedSynchronizer) as other primitives shipped with the library: [AsyncExclusiveLock](xref:DotNext.Threading.AsyncExclusiveLock), [AsyncReaderWriterLock](xref:DotNext.Threading.AsyncReaderWriterLock), etc. The following example demonstrates how to write custom async-aware reader-writer lock:
```csharp
using DotNext.Threading;

// bool indicates lock type:
// false - read lock
// true - write lock
class MyExclusiveLock : QueuedSynchronizer<bool>
{
    // = 0 - no lock acquired
    // > 0 - read lock
    // < 0 - write lock
    private int readersCount;

    public MyExclusiveLock()
        : base(null)
    {
    }

    public ValueTask AcquireReadLockAsync(CancellationToken token)
        => base.AcquireAsync(false, token);

    public void ReleaseReadLock(CancellationToken token)
        => base.Release(false);

    public ValueTask AcquireWriteLockAsync(CancellationToken token)
        => base.AcquireAsync(true, token);

    public void ReleaseWriteLock()
        => base.Release(true);

    // write lock cannot be acquired if there is at least one read lock, or single write lock
    protected override bool CanAcquire(bool writeLock)
        => writeLock ? readersCount is 0 : readersCount >= 0;

    protected override void AcquireCore(bool writeLock)
        => readersCount = writeLock ? -1 : readersCount + 1;

    protected override void ReleaseCore(bool writeLock)
        => readersCount = writeLock ? 0 : readersCount - 1;
}
```

# Concurrency Limit
`AcquireAsync` or `WaitAsync` methods exposed by async synchronization primitives, suspend the caller. The suspended caller is a slot within the internal wait queue maintained by [QueuedSynchronizer](xref:DotNext.Threading.QueuedSynchronizer) class. By default, the number of suspended callers in the queue is unlimited. For rate limiting purposes, the limit on the queue size can be established:
```csharp
var exclusiveLock = new AsyncExclusiveLock()
{
    ConcurrencyLevel = 10, // the number of the suspended callers in the queue
    HasConcurrencyLimit = true, // tells that the queue size is limited
};
```

In the example above, if 11th caller cannot acquire the lock immediately, `AcquireAsync` method throws [ConcurrencyLimitReachedException](xref:DotNext.Threading.ConcurrencyLimitReachedException) exception. If `HasConcurrencyLimit` is set to **false**, the `ConcurrencyLevel` only limits the internal pool of the wait nodes, but not the queue size. 

# Diagnostics
All synchronization primitives for asynchronous code mostly derive from [QueuedSynchronizer](xref:DotNext.Threading.QueuedSynchronizer) class that exposes a set of important diagnostics counters:
* `LockContentionCounter` allows to measure a number of lock contentions detected in the specified time period

# Debugging
In addition to diagnostics tools, [QueuedSynchronizer](xref:DotNext.Threading.QueuedSynchronizer) and all its derived classes support a rich set of debugging tools:
* `TrackSuspendedCallers` method allows to enable tracking information about suspended caller. This method has effect only when building project using `Debug` configuration
* `SetCallerInformation` method allows to associate information with the caller if it will be suspended during the call of `WaitAsync`. This method has effect only when building project using `Debug` configuration
* `GetSuspendedCallers` method allows to capture a list of all suspended callers. The method is working only if tracking is enabled via `TrackSuspendedCallers` method. Typically, this method should be used in debugger's _Watch_ window when all threads are paused