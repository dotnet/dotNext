Synchronization Enhancements
===
.NET class library provides different ways to organize synchronized access to the resource shared between multiple threads. These synchronization mechanisms are have differences in their API and require to pay more attention to how to acquire and release resources. For example, [lock statement](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement) can acquire and release resource in more safely way than [Semaphore](https://docs.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) or [ReaderWriterLock](https://docs.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim). Last two synchronization mechanisms force the developer to use **try-finally** statement manually.

DotNext provides unified representation of the resource lock using the following mechanisms:
* Monitor
* Reader lock
* Upgradable reader lock
* Write lock
* Semaphore

This unification is implemented in the form of single type [Lock](../../api/DotNext.Threading.Lock.yml) and set of extension methods. Lock type implements [Dispose](https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose) pattern so lock lifetime management can be organized with [using keyword](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-statement) in C#.

```csharp
using DotNext.Threading;
using System.Threading;

private readonly ReaderWriterLockSlim rwlock;
private readonly object syncRoot; //monitor object

using(rwlock.ReadLock())
{
    //code protected by read lock
}

using(rwlock.WriteLock())
{
    //code protected by write lock
}

using(syncRoot.Lock(TimeSpan.FromSeconds(10)))
{
    //code protected by monitor lock
}
```

# Built-in reader/writer synchronization
Exclusive lock such as monitor lock may not be applicable due to performance reasons to some data types. For example, exclusive lock for dictionary or list is redundant because there are two consumers of these objects: writers and readers.

> [!NOTE]
> Built-in support for reader/writer locks is available since version 0.2 of DotNext library.

DotNext library provides several extension methods for more granular control over synchronization for any reference type:
* `ReadLock` acquires reader lock
* `WriteLock` acquires exclusive lock
* `UpgradableReadLock` acquires read lock which can be upgraded to write lock

These methods allow to turn any thread-unsafe object into thread-safe object with precise control of multithreading access.

```csharp
using System.Text;
using DotNext.Threading;

var builder = new StringBuilder();

//reader
using(builder.ReadLock())
{
    Console.WriteLine(builder.ToString());
}

//writer
using(builder.WriteLock())
{
    builder.Append("Hello, world!");
}
```