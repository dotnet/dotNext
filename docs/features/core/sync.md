Synchronization Enhancements
====
.NET class library provides different ways to organize synchronized access to the resource shared between multiple threads. These synchronization mechanisms are have differences in their API and require to pay more attention to how to acquire and release resources. For example, [lock statement](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement) can acquire and release resource in more safely way than [Semaphore](https://docs.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) or [ReaderWriterLock](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim). Last two synchronization mechanisms force the developer to use **try-finally** statement manually.

.NEXT provides unified representation of the resource lock using the following mechanisms:
* Monitor
* Reader lock
* Upgradeable lock
* Writer lock
* Semaphore

This unification is implemented in the form of the single type [Lock](../../api/DotNext.Threading.Lock.yml). It exposes the same set of acquisition methods for each lock type described above. The acquired lock is represented by [lock holder](../../api/DotNext.Threading.Lock.Holder.yml). The only purpose of this object is to hold the acquired lock. The lock can be released using `Dispose` method of the holder.

```csharp
using DotNext.Threading;
using System.Threading;

using var semaphore = new SemaphoreSlim();
using var @lock = Lock.Semaphore(semaphore);
//acquires the lock
using(@lock.Acquire())
{
}

using var rwlock = new ReaderWriterLockSlim();
using var @lock = Lock.WriteLock(rwLock);
//acquires the writer lock
if(@lock.TryAcquire(out Lock.Holder holder))
    using(holder)
    {

    }
```

# Built-in Reader/Writer Synchronization
Exclusive lock such as monitor lock may not be applicable due to performance reasons for some data types. For example, exclusive lock for dictionary or list is redundant because there are two consumers of these objects: writers and readers.

DotNext library provides several extension methods for more granular control over synchronization of any reference type:
* `AcquireReadLock` acquires reader lock
* `AcquireWriteLock` acquires exclusive lock
* `AcquireUpgradeableReadLock` acquires read lock which can be upgraded to write lock

These methods allow to turn any thread-unsafe object into thread-safe object with precise control in context of multithreading access.

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

For more information check extension methods inside of [LockAcquisition](../../api/DotNext.Threading.LockAcquisition.yml) class.