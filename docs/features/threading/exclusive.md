Async Exclusive Lock
====
[AsyncExclusiveLock](xref:DotNext.Threading.AsyncExclusiveLock) provides asynchronous acquisition operation compatible with **await** operator. The only one caller can acquire the lock at a time. Behavior of this lock is similar to the monitor lock. Under contention, locks favor granting access to the longest-waiting task.

```csharp
using DotNext.Threading;
using System.Threading;

var @lock = new AsyncExclusiveLock();

await @lock.AcquireAsync(CancellationToken.None);
try
{
    //synchronized access to the resource
}
finally
{
    @lock.Release();
}

//or with 'using' statement:
using(await @lock.AcquireLockAsync(CancellationToken.None))
{
    //synchronized access to the resource
}
```

This class supports methods to determine whether locks are held or contended. These methods are designed for monitoring system state, not for synchronization control. 

Exclusive lock should be destroyed if no longer needed by calling `Dispose` method which is not thread-safe.

## Graceful Shutdown
`Dispose` method is not thread-safe and may cause unpredictable behavior if called on the lock which was acquired previously. This is happening because the method doesn't wait for the lock to be released. Starting with version _2.6.0_ this type of lock implements [IAsyncDisposable](https://docs.microsoft.com/en-us/dotnet/api/system.iasyncdisposable) interface and provides a way for graceful shutdown. `DisposeAsync` behaves in the following way:
* If lock is not acquired then completes synchronously
* If lock is acquired then suspends the caller and wait when it will be released, then dispose the lock