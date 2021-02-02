Async Reader/Writer Lock
====
[AsyncReaderWriterLock](xref:DotNext.Threading.AsyncReaderWriterLock) is a non-blocking asynchronous alternative to [ReaderWriteLockSlim](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim) with the same semantics.

This class supports methods to determine whether locks are held or contended. These methods are designed for monitoring system state, not for synchronization control. 

The reader lock and writer lock both support interruption during lock acquisition using [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken).

```csharp
using System.Threading;
using DotNext.Threading;

var rwlock = new AsyncReaderWriterLock();

await rwlock.EnterReadLockAsync(CancellationToken.None);
try
{
    //reader stuff here
}
finally
{
    rwlock.ExitReadLock();
}

await rwlock.EnterWriteLockAsync(TimeSpan.FromSecond(2));
try
{
    //writer stuff here
}
finally
{
    rwlock.ExitWriteLock();
}

await rwlock.EnterUpgradeableReadLockAsync(TimeSpan.FromSecond(2), CancellationToken.None);
try
{
    //reader stuff here
    await rwlock.EnterWriteLockAsync(CancellationToken.None);
    //writer stuff here
    rwlock.ExitWriteLock();
}
finally
{
    rwlock.ExitWriteLock();
}

//or with 'using statement'
using(await rwlock.AcquireReadLockAsync(CancellationToken.None))
{
    //reader stuff here
}

using(await rwlock.AcquireWriteLockAsync(TimeSpan.FromSecond(2)))
{
    //writer stuff here
}
```

Exclusive lock should be destroyed if no longer needed by calling `Dispose` method which is not thread-safe.

## Acquisition Order
This lock does not impose a reader or writer preference ordering for lock access. However, it respects fairness policy. It means that callers contend for entry using an approximately arrival-order policy. When the currently held lock is released either the longest-waiting single writer will be assigned the write lock, or if there is a group of readers waiting longer than all waiting writers, that group will be assigned the read lock. 

A caller that tries to acquire a read lock (non-reentrantly) will enqueued if either the write lock is held, or there is a waiting writer. The caller will not acquire the read lock until after the oldest currently waiting writer has acquired and released the write lock. Of course, if a waiting writer abandons its wait, leaving one or more readers as the longest waiters in the queue with the write lock free, then those readers will be assigned the read lock.

A caller that tries to acquire a write lock (non-reentrantly) will block unless both the read lock and write lock are free (which implies there are no waiters).

## Graceful Shutdown
`Dispose` method is not thread-safe and may cause unpredictable behavior if called on the lock which was acquired previously. This is happening because the method doesn't wait for the lock to be released. Starting with version _2.6.0_ this type of lock implements [IAsyncDisposable](https://docs.microsoft.com/en-us/dotnet/api/system.iasyncdisposable) interface and provides a way for graceful shutdown. `DisposeAsync` behaves in the following way:
* If lock is not acquired then completes synchronously
* If lock is acquired then suspends the caller and wait when it will be released, then dispose the lock
