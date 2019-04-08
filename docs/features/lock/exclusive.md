Async Exclusive Lock
====
[AsyncExclusiveLock](../../api/DotNext.Threading.AsyncExclusiveLock.yml) provides asynchronous acquisition operation compatible with **await** operator. The only one caller can acquire the lock at a time. Behavior of this lock is similar to the monitor lock. Under contention, locks favor granting access to the longest-waiting task.

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

This class supports methods to determine whether locks are held or contended. These methods are designed for monitoring system state, not for synchronization control. 

Exclusive lock should be destroyed if no longer needed by calling `Dispose` method which is not thread-safe.