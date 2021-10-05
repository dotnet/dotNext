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

var semaphore = new SemaphoreSlim(0, 1);
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
using DotNext.Threading;
using System.Text;

var builder = new StringBuilder();

//reader
using (builder.AcquireReadLockAsync(CancellationToken.None))
{
    Console.WriteLine(builder.ToString());
}

//writer
using (builder.AcquireWriteLockAsync(CancellationToken.None))
{
    builder.Append("Hello, world!");
}
```

For more information check extension methods inside of [AsyncLockAcquisition](xref:DotNext.Threading.LockAcquisition) class.