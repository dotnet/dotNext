Async Reader/Writer Lock
====
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

Asynchronous reader/writer lock is not reentrant because there is no concept of lock owner thread. The lock can be requested by one thread and acquired by another due to nature of asynchronous operations.