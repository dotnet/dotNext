namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class LockAcquisitionTests : Test
{
    [Fact]
    public static async Task AsyncReaderWriterLock()
    {
        var obj = new object();
        var holder1 = await AsyncLock.AcquireReadLockAsync(obj, TestToken);
        if (holder1) { }
        else Fail("Lock is not acquired");

        var holder2 = await AsyncLock.AcquireReadLockAsync(obj, TimeSpan.Zero, TestToken);
        if (holder2) { }
        else Fail("Lock is not acquired");

        await ThrowsAsync<TimeoutException>(AsyncLock.AcquireWriteLockAsync(obj, TimeSpan.Zero, TestToken).AsTask);
        holder1.Dispose();
        holder2.Dispose();

        holder1 = await AsyncLock.AcquireWriteLockAsync(obj, TestToken);
        if (holder1) { }
        else Fail("Lock is not acquired");
        holder1.Dispose();
    }

    [Fact]
    public static async Task AsyncExclusiveLock()
    {
        var obj = new object();
        var holder1 = await AsyncLock.AcquireLockAsync(obj, TestToken);
        if (holder1) { }
        else Fail("Lock is not acquired");

        await ThrowsAsync<TimeoutException>(AsyncLock.AcquireLockAsync(obj, TimeSpan.Zero, TestToken).AsTask);
        holder1.Dispose();
    }

    [Fact]
    public static async Task InvalidLock()
    {
        var obj = string.Intern("Interned string");
        Throws<InvalidOperationException>(() => Lock.AcquireReadLock(obj, DefaultTimeout));
        Throws<InvalidOperationException>(() => Lock.AcquireWriteLock(obj, DefaultTimeout));
        Throws<InvalidOperationException>(() => Lock.AcquireUpgradeableReadLock(obj, DefaultTimeout));

        await ThrowsAsync<InvalidOperationException>(async () => await AsyncLock.AcquireLockAsync(obj, TimeSpan.Zero, TestToken));
        await ThrowsAsync<InvalidOperationException>(async () => await AsyncLock.AcquireReadLockAsync(obj, TimeSpan.Zero, TestToken));
        await ThrowsAsync<InvalidOperationException>(async () => await AsyncLock.AcquireWriteLockAsync(obj, TimeSpan.Zero, TestToken));
        Throws<InvalidOperationException>(() => Lock.AcquireUpgradeableReadLock(obj, TimeSpan.Zero));
    }
}