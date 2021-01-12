using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class LockAcquisitionTests : Test
    {
        [Fact]
        public static async Task AsyncReaderWriterLock()
        {
            var obj = new object();
            var holder1 = await obj.AcquireReadLockAsync(TimeSpan.Zero);
            if (holder1) { }
            else throw new Exception();

            var holder2 = await obj.AcquireReadLockAsync(TimeSpan.Zero);
            if (holder2) { }
            else throw new Exception();

            await ThrowsAsync<TimeoutException>(() => obj.AcquireWriteLockAsync(TimeSpan.Zero));
            holder1.Dispose();
            holder2.Dispose();

            holder1 = await obj.AcquireWriteLockAsync(TimeSpan.Zero);
            if (holder1) { }
            else throw new Exception();
            holder1.Dispose();
        }

        [Fact]
        public static async Task AsyncExclusiveLock()
        {
            var obj = new object();
            var holder1 = await obj.AcquireLockAsync(TimeSpan.Zero);
            if (holder1) { }
            else throw new Exception();

            await ThrowsAsync<TimeoutException>(() => obj.AcquireLockAsync(TimeSpan.Zero));
            holder1.Dispose();
        }

        [Fact]
        public static void ReaderWriterLock()
        {
            var obj = new object();
            var holder1 = obj.AcquireReadLock(DefaultTimeout);
            if (holder1) { }
            else throw new Exception();

            var holder2 = obj.AcquireReadLock(DefaultTimeout);
            if (holder2) { }
            else throw new Exception();

            Throws<LockRecursionException>(() => obj.AcquireWriteLock(TimeSpan.Zero));
            holder1.Dispose();
            holder2.Dispose();

            holder1 = obj.AcquireWriteLock(TimeSpan.Zero);
            if (holder1) { }
            else throw new Exception();
            holder1.Dispose();
        }

        [Fact]
        public static async Task InvalidLock()
        {
            var obj = string.Intern("Interned string");
            Throws<InvalidOperationException>(() => obj.AcquireReadLock(DefaultTimeout));
            Throws<InvalidOperationException>(() => obj.AcquireWriteLock(DefaultTimeout));
            Throws<InvalidOperationException>(() => obj.AcquireUpgradeableReadLock(DefaultTimeout));

            await ThrowsAsync<InvalidOperationException>(() => obj.AcquireLockAsync(TimeSpan.Zero));
            await ThrowsAsync<InvalidOperationException>(() => obj.AcquireReadLockAsync(TimeSpan.Zero));
            await ThrowsAsync<InvalidOperationException>(() => obj.AcquireWriteLockAsync(TimeSpan.Zero));
            await ThrowsAsync<InvalidOperationException>(() => obj.AcquireUpgradeableReadLockAsync(TimeSpan.Zero));
        }
    }
}
