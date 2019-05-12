Async Reader/Writer Lock
====
[AsyncReaderWriterLock](../../api/DotNext.Threading.AsyncReaderWriterLock.yml) is a non-blocking asynchronous alternative to [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim) with the same semantics.

This class supports methods to determine whether locks are held or contended. These methods are designed for monitoring system state, not for synchronization control. 

The reader lock and writer lock both support interruption during lock acquisition using [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken).

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

await rwlock.EnterUpgradeableReadLock(TimeSpan.FromSecond(2), CancellationToken.None);
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

Exclusive lock should be destroyed if no longer needed by calling `Dispose` method which is not thread-safe.

## Acquisition Order
This lock does not impose a reader or writer preference ordering for lock access. However, it respects fairness policy. It means that callers contend for entry using an approximately arrival-order policy. When the currently held lock is released either the longest-waiting single writer will be assigned the write lock, or if there is a group of readers waiting longer than all waiting writers, that group will be assigned the read lock. 

A caller that tries to acquire a read lock (non-reentrantly) will enqueued if either the write lock is held, or there is a waiting writer. The caller will not acquire the read lock until after the oldest currently waiting writer has acquired and released the write lock. Of course, if a waiting writer abandons its wait, leaving one or more readers as the longest waiters in the queue with the write lock free, then those readers will be assigned the read lock.

A caller that tries to acquire a write lock (non-reentrantly) will block unless both the read lock and write lock are free (which implies there are no waiters).