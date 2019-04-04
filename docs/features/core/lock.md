Synchronization Enhancements
====
.NET class library provides different ways to organize synchronized access to the resource shared between multiple threads. These synchronization mechanisms are have differences in their API and require to pay more attention to how to acquire and release resources. For example, [lock statement](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement) can acquire and release resource in more safely way than [Semaphore](https://docs.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) or [ReaderWriterLock](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim). Last two synchronization mechanisms force the developer to use **try-finally** statement manually.

DotNext provides unified representation of the resource lock using the following mechanisms:
* Monitor
* Reader lock
* Upgradable lock
* Writer lock
* Semaphore

This unification is implemented in the form of the single type [Lock](../../api/DotNext.Threading.Lock.yml). It exposes the same set of acquisition methods for each lock type described above. The acquired lock is represented by [lock holder](../../api/DotNext.Threading.Lock.Holder.yml). The only purpose of this object is to hold the acquired lock. The lock can be released using `Dispose` method of the holder.

```csharp
using DotNext.Threading;
using System.Threading;

var semaphore = new SemaphoreSlim();
using(var @lock = Lock.Semaphore(semaphore))
{
    //acquires the lock
    using(@lock.Acquire())
    {
    }
}

var rwlock = new ReaderWriterLockSlim();
using(var @lock = Lock.WriteLock(rwLock))
{
    //acquires the writer lock
    if(@lock.TryAcquire(out Lock.Holder holder))
        using(holder)
        {

        }
}
```

# Built-in Reader/Writer Synchronization
Exclusive lock such as monitor lock may not be applicable due to performance reasons to some data types. For example, exclusive lock for dictionary or list is redundant because there are two consumers of these objects: writers and readers.

DotNext library provides several extension methods for more granular control over synchronization for any reference type:
* `AcquireReadLock` acquires reader lock
* `AcquireWriteLock` acquires exclusive lock
* `AcquireUpgradableReadLock` acquires read lock which can be upgraded to write lock

These methods allow to turn any thread-unsafe object into thread-safe object with precise control of multithreading access.

```csharp
using System.Text;
using DotNext.Threading;

var builder = new StringBuilder();

//reader
using(builder.AcquireReadLock())
{
    Console.WriteLine(builder.ToString());
}

//writer
using(builder.AcquireWriteLock())
{
    builder.Append("Hello, world!");
}
```

# Asynchronous Locks
Lock acquisition operation may blocks the caller thread. Reader/writer lock from .NET library doesn't have async versions of lock acquisition methods as well as Monitor. To avoid this, DotNext Threading library provides asynchronous non-blocking alternatives of these types of locks.

> [!CAUTION]
> Non-blocking and blocking locks are two different worlds. It is not recommended to mix these API in the single application. The lock acquired with blocking API located in [Lock](../../api/DotNext.Threading.Lock.yml), [Monitor](https://docs.microsoft.com/en-us/dotnet/api/system.threading.monitor) or [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim) is not aware about the lock acquired asynchronously with [AsyncLock](../../api/DotNext.Threading.AsyncLock.yml), [AsyncExclusiveLock](../../api/DotNext.Threading.AsyncExclusiveLock.yml) or [AsyncReaderWriterLock](../../api/DotNext.Threading.AsyncReaderWriterLock.yml). The only exception is [SemaphoreSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) because it contains acquisition methods in blocking and non-blocking manner at the same time.

[AsyncLock](../../api/DotNext.Threading.AsyncLock.yml) is a unified representation of the all supported asynchronous locks:
* Exclusive lock
* Reader lock
* Writer lock
* Upgradable lock
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

## Exclusive Lock
[Exclusive Lock](../../api/DotNext.Threading.AsyncExclusiveLock.yml) provides asynchronous acquisition operation compatible with **await** operator. The only one caller can acquire the lock at a time. Behavior of this lock is similar to the monitor lock.

```csharp
using DotNext.Threading;
using System.Threading;

var @lock = new AsyncExclusiveLock();

await @lock.Acquire(CancellationToken.None);
try
{
    //synchronized access to the resource
}
finally
{
    @lock.Release();
}

//or with 'using' statement:
using(await @lock.AcquireLock(CancellationToken.None))
{
    //synchronized access to the resource
}
```

Asynchronous mutually exclusive lock is available for every reference type through extension methods using the same approach as built-in reader/writer synchronization:
```csharp
using System.Text;
using System.Threading;
using DotNext.Threading;

var builder = new StringBuilder();

//obtains exclusive lock
using(await builder.AcquireLockAsync(CancellationToken.None))
{
    builder.Append("Inside of synchronized region");
}

```

For more information check extension methods inside of [AsyncLockAcquisition](../../api/DotNext.Threading.AsyncLockAcquisition.yml) class.

Asynchronous exclusive lock is not reentrant because there is no concept of lock owner thread. The lock can be requested in one thread and acquired in another due to nature of asynchronous operations.

## Reader/Writer Lock
[AsyncReaderWriterLock](../../api/DotNext.Threading.AsyncReaderWriterLock.yml) is a non-blocking asynchronous alternative to [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim) with the same semantics.

```csharp
using System.Threading;
using DotNext.Threading;

var rwlock = new AsyncReaderWriterLock();

await rwlock.EnterReadLock(CancellationToken.None);
try
{
    //reader stuff here
}
finally
{
    rwlock.ExitReadLock();
}

await rwlock.EnterWriteLock(TimeSpan.FromSecond(2));
try
{
    //writer stuff here
}
finally
{
    rwlock.ExitWriteLock();
}

await rwlock.EnterUpgradableReadLock(TimeSpan.FromSecond(2), CancellationToken.None);
try
{
    //reader stuff here
    await rwlock.EnterWriteLock(CancellationToken.None);
    //writer stuff here
    rwlock.ExitWriteLock();
}
finally
{
    rwlock.ExitWriteLock();
}

//or with 'using statement'
using(await rwlock.AcquireReadLock(CancellationToken.None))
{
    //reader stuff here
}

using(await rwlock.AcquireWriteLock(TimeSpan.FromSecond(2)))
{
    //writer stuff here
}
```

Any reference type can be synchronized with reader/writer lock using extension methods from [AsyncLockAcquisition](../../api/DotNext.Threading.AsyncLockAcquisition.yml) class.

```csharp
using System.Text;
using DotNext.Threading;

var builder = new StringBuilder();

//reader
using(await builder.AcquireReadLockAsync(TimeSpan.FromSeconds(1)))
{
    Console.WriteLine(builder.ToString());
}

//writer
using(await builder.AcquireWriteLockAsync(CancellationToken.None))
{
    builder.Append("Hello, world!");
}
```

Asynchronous reader/writer lock is not reentrant because there is no concept of lock owner thread. The lock can be requested by one thread and acquired by another due to nature of asynchronous operations.