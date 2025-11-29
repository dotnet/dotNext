namespace DotNext.Threading;

using static AsyncLockAcquisition;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class LockAcquisitionTests : Test
{
    [Fact]
    public static async Task AsyncReaderWriterLock()
    {
        var obj = new object();
        var holder1 = await AcquireReadLockAsync(obj);
        if (holder1) { }
        else Fail("Lock is not acquired");

        var holder2 = await AcquireReadLockAsync(obj, TimeSpan.Zero);
        if (holder2) { }
        else Fail("Lock is not acquired");

        await ThrowsAsync<TimeoutException>(AcquireWriteLockAsync(obj, TimeSpan.Zero).AsTask);
        holder1.Dispose();
        holder2.Dispose();

        holder1 = await AcquireWriteLockAsync(obj);
        if (holder1) { }
        else Fail("Lock is not acquired");
        holder1.Dispose();
    }

    [Fact]
    public static async Task AsyncExclusiveLock()
    {
        var obj = new object();
        var holder1 = await AcquireLockAsync(obj);
        if (holder1) { }
        else Fail("Lock is not acquired");

        await ThrowsAsync<TimeoutException>(AcquireLockAsync(obj, TimeSpan.Zero).AsTask);
        holder1.Dispose();
    }

    [Fact]
    public static async Task InvalidLock()
    {
        var obj = string.Intern("Interned string");
        Throws<InvalidOperationException>(() => Lock.AcquireReadLock(obj, DefaultTimeout));
        Throws<InvalidOperationException>(() => Lock.AcquireWriteLock(obj, DefaultTimeout));
        Throws<InvalidOperationException>(() => Lock.AcquireUpgradeableReadLock(obj, DefaultTimeout));

        await ThrowsAsync<InvalidOperationException>(async () => await AcquireLockAsync(obj, TimeSpan.Zero));
        await ThrowsAsync<InvalidOperationException>(async () => await AcquireReadLockAsync(obj, TimeSpan.Zero));
        await ThrowsAsync<InvalidOperationException>(async () => await AcquireWriteLockAsync(obj, TimeSpan.Zero));
        Throws<InvalidOperationException>(() => Lock.AcquireUpgradeableReadLock(obj, TimeSpan.Zero));
    }
}