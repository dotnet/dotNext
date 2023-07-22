namespace DotNext.Threading;

public sealed class LockAcquisitionTests : Test
{
    [Fact]
    public static async Task AsyncReaderWriterLock()
    {
        var obj = new object();
        var holder1 = await obj.AcquireReadLockAsync(TimeSpan.Zero);
        if (holder1) { }
        else Fail("Lock is not acquired");

        var holder2 = await obj.AcquireReadLockAsync(TimeSpan.Zero);
        if (holder2) { }
        else Fail("Lock is not acquired");

        await ThrowsAsync<TimeoutException>(obj.AcquireWriteLockAsync(TimeSpan.Zero).AsTask);
        holder1.Dispose();
        holder2.Dispose();

        holder1 = await obj.AcquireWriteLockAsync(TimeSpan.Zero);
        if (holder1) { }
        else Fail("Lock is not acquired");
        holder1.Dispose();
    }

    [Fact]
    public static async Task AsyncExclusiveLock()
    {
        var obj = new object();
        var holder1 = await obj.AcquireLockAsync(TimeSpan.Zero);
        if (holder1) { }
        else Fail("Lock is not acquired");

        await ThrowsAsync<TimeoutException>(obj.AcquireLockAsync(TimeSpan.Zero).AsTask);
        holder1.Dispose();
    }

    [Fact]
    public static void ReaderWriterLock()
    {
        var obj = new object();
        var holder1 = obj.AcquireReadLock(DefaultTimeout);
        if (holder1) { }
        else Fail("Lock is not acquired");

        var holder2 = obj.AcquireReadLock(DefaultTimeout);
        if (holder2) { }
        else Fail("Lock is not acquired");

        Throws<LockRecursionException>(() => obj.AcquireWriteLock(TimeSpan.Zero));
        holder1.Dispose();
        holder2.Dispose();

        holder1 = obj.AcquireWriteLock(TimeSpan.Zero);
        if (holder1) { }
        else Fail("Lock is not acquired");
        holder1.Dispose();
    }

    [Fact]
    public static async Task InvalidLock()
    {
        var obj = string.Intern("Interned string");
        Throws<InvalidOperationException>(() => obj.AcquireReadLock(DefaultTimeout));
        Throws<InvalidOperationException>(() => obj.AcquireWriteLock(DefaultTimeout));
        Throws<InvalidOperationException>(() => obj.AcquireUpgradeableReadLock(DefaultTimeout));

        await ThrowsAsync<InvalidOperationException>(async () => await obj.AcquireLockAsync(TimeSpan.Zero));
        await ThrowsAsync<InvalidOperationException>(async () => await obj.AcquireReadLockAsync(TimeSpan.Zero));
        await ThrowsAsync<InvalidOperationException>(async () => await obj.AcquireWriteLockAsync(TimeSpan.Zero));
        Throws<InvalidOperationException>(() => obj.AcquireUpgradeableReadLock(TimeSpan.Zero));
    }
}