Async Exclusive Lock
====
[AsyncExclusiveLock](../../api/DotNext.Threading.AsyncExclusiveLock.yml) provides asynchronous acquisition operation compatible with **await** operator. The only one caller can acquire the lock at a time. Behavior of this lock is similar to the monitor lock.

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

Asynchronous exclusive lock is not reentrant because there is no concept of lock owner thread. The lock can be requested in one thread and acquired in another due to nature of asynchronous operations.