using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Threading;

public partial class PersistentState
{
    internal sealed class LockState
    {
        private readonly uint maxReadCount;
        private uint readerCount;
        private bool allowWrite;

        internal LockState(int concurrencyLevel)
        {
            maxReadCount = (uint)concurrencyLevel;
            allowWrite = true;
        }

        internal bool IsStrongReadLockAllowed => readerCount < maxReadCount && allowWrite;

        internal void AcquireStrongReadLock()
        {
            Debug.Assert(IsStrongReadLockAllowed);

            allowWrite = false;
            readerCount += 1U;
        }

        internal void ReleaseStrongReadLock()
        {
            Debug.Assert(readerCount > 0U);
            Debug.Assert(!allowWrite);

            readerCount -= 1U;
            allowWrite = true;
        }

        internal bool IsWeakReadLockAllowed => readerCount < maxReadCount;

        internal void AcquireWeakReadLock()
        {
            Debug.Assert(IsWeakReadLockAllowed);

            readerCount += 1U;
        }

        internal void ReleaseWeakReadLock()
        {
            Debug.Assert(readerCount > 0L);

            readerCount -= 1U;
        }

        internal bool IsWriteLockAllowed => allowWrite;

        internal void AcquireWriteLock()
        {
            Debug.Assert(IsWriteLockAllowed);

            allowWrite = false;
        }

        internal void ReleaseWriteLock()
        {
            Debug.Assert(!allowWrite);

            allowWrite = true;
        }

        internal bool IsCompactionLockAllowed => readerCount == 0U;

        internal void AcquireCompactionLock()
        {
            Debug.Assert(IsCompactionLockAllowed);

            readerCount = uint.MaxValue;
        }

        internal void ReleaseCompactionLock()
        {
            Debug.Assert(readerCount == uint.MaxValue);

            readerCount = 0U;
        }

        internal bool IsExclusiveLockAllowed => readerCount == 0L && allowWrite;

        internal void AcquireExclusiveLock()
        {
            Debug.Assert(IsExclusiveLockAllowed);

            readerCount = uint.MaxValue;
            allowWrite = false;
        }

        internal void ReleaseExclusiveLock()
        {
            Debug.Assert(readerCount == uint.MaxValue);
            Debug.Assert(!allowWrite);

            readerCount = 0U;
            allowWrite = true;
        }
    }

    internal enum LockType : int
    {
        WeakReadLock = 0,
        StrongReadLock = 1,
        WriteLock = 2,
        CompactionLock = 3,
        ExclusiveLock = 4,
    }

    // This lock manager implements the following logic:
    // Weak read lock   - allow reads, allow writes (to the end of the log), disallow compaction
    // Strong read lock - allow reads, disallow writes, disallow compaction
    // Write lock       - allow reads, disallow writes, allow compaction
    // Compaction lock  - disallow reads, allow writes (to the end of the log), disallow compaction
    // Exclusive lock   - disallow everything
    // Write lock + Compaction lock = exclusive lock
    internal sealed class LockManager : AsyncTrigger<LockState>
    {
        private sealed class WeakReadLockTransition : ITransition
        {
            bool ITransition.Test(LockState state) => state.IsWeakReadLockAllowed;

            void ITransition.Transit(LockState state) => state.AcquireWeakReadLock();

            internal static void Release(LockState state) => state.ReleaseWeakReadLock();
        }

        private sealed class StrongReadLockTransition : ITransition
        {
            bool ITransition.Test(LockState state) => state.IsStrongReadLockAllowed;

            void ITransition.Transit(LockState state) => state.AcquireStrongReadLock();

            internal static void Release(LockState state) => state.ReleaseStrongReadLock();
        }

        private sealed class WriteLockTransition : ITransition
        {
            bool ITransition.Test(LockState state) => state.IsWriteLockAllowed;

            void ITransition.Transit(LockState state) => state.AcquireWriteLock();

            internal static void Release(LockState state) => state.ReleaseWriteLock();
        }

        private sealed class CompactionLockTransition : ITransition
        {
            bool ITransition.Test(LockState state) => state.IsCompactionLockAllowed;

            void ITransition.Transit(LockState state) => state.AcquireCompactionLock();

            internal static void Release(LockState state) => state.ReleaseCompactionLock();
        }

        private sealed class ExclusiveLockTransition : ITransition
        {
            bool ITransition.Test(LockState state) => state.IsExclusiveLockAllowed;

            void ITransition.Transit(LockState state) => state.AcquireExclusiveLock();

            internal static void Release(LockState state) => state.ReleaseExclusiveLock();
        }

        private readonly ITransition[] acquisitions = { new WeakReadLockTransition(), new StrongReadLockTransition(), new WriteLockTransition(), new CompactionLockTransition(), new ExclusiveLockTransition() };
        private readonly Action<LockState>[] exits = { WeakReadLockTransition.Release, StrongReadLockTransition.Release, WriteLockTransition.Release, CompactionLockTransition.Release, ExclusiveLockTransition.Release };

        internal LockManager(int concurrencyLevel)
            : base(new(concurrencyLevel), concurrencyLevel + 2) // + write lock + compaction lock
        {
        }

        internal ValueTask AcquireAsync(LockType type, CancellationToken token = default)
        {
            Debug.Assert(type is >= LockType.WeakReadLock and <= LockType.ExclusiveLock);

            var acquisition = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(acquisitions), (int)type);
            return WaitAsync(acquisition, token);
        }

        internal ValueTask<bool> AcquireAsync(LockType type, TimeSpan timeout, CancellationToken token = default)
        {
            Debug.Assert(type is >= LockType.WeakReadLock and <= LockType.ExclusiveLock);

            var acquisition = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(acquisitions), (int)type);
            return WaitAsync(acquisition, timeout, token);
        }

        internal void Release(LockType type)
        {
            Debug.Assert(type is >= LockType.WeakReadLock and <= LockType.ExclusiveLock);

            var exit = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(exits), (int)type);
            Signal(exit);
        }
    }
}