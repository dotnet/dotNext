Asynchronous Locks
====
Lock acquisition operation may blocks the caller thread. Reader/writer lock from .NET library doesn't have async versions of lock acquisition methods as well as Monitor. To avoid this, DotNext Threading library provides asynchronous non-blocking alternatives of these types of locks.

> [!CAUTION]
> Non-blocking and blocking locks are two different worlds. It is not recommended to mix these API in the single application. The lock acquired with blocking API located in [Lock](../../api/DotNext.Threading.Lock.yml), [Monitor](https://docs.microsoft.com/en-us/dotnet/api/system.threading.monitor) or [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim) is not aware about the lock acquired asynchronously with [AsyncLock](../../api/DotNext.Threading.AsyncLock.yml), [AsyncExclusiveLock](../../api/DotNext.Threading.AsyncExclusiveLock.yml) or [AsyncReaderWriterLock](../../api/DotNext.Threading.AsyncReaderWriterLock.yml). The only exception is [SemaphoreSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) because it contains acquisition methods in blocking and non-blocking manner at the same time.

[AsyncLock](../../api/DotNext.Threading.AsyncLock.yml) is a unified representation of the all supported asynchronous locks:
* Exclusive lock
* Reader lock
* Writer lock
* Upgradeable lock
* Semaphore

The only one synchronization object can be shared between blocking and non-blocking representations of the lock.
```csharp
using DotNext.Threading;
using System.Threading;

var semaphore = new SemaphoreSlim(0, 1);
var syncLock = Lock.Semaphore(semaphore);
var asyncLock = AsyncLock.Semaphore(semaphore);

//thread #1
using(syncLock.Acquire())
{

}

//thread #2
using(await asyncLock.Acquire(CancellationToken.None))
{

}
```

# Built-in Reader/Writer Synchronization
