using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Threading;

public partial class PersistentState
{
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
    internal sealed class LockManager : QueuedSynchronizer<LockType>
    {
        private readonly uint maxReadCount;
        private uint readerCount;
        private bool allowWrite;

        internal LockManager(int concurrencyLevel)
            : base(concurrencyLevel + 2) // + write lock + compaction lock
        {
            maxReadCount = (uint)concurrencyLevel;
            allowWrite = true;
        }

        protected override bool CanAcquire(LockType type) => type switch
        {
            LockType.WeakReadLock => readerCount < maxReadCount,
            LockType.StrongReadLock => readerCount < maxReadCount && allowWrite,
            LockType.WriteLock => allowWrite,
            LockType.CompactionLock => readerCount is 0U,
            LockType.ExclusiveLock => readerCount is 0U && allowWrite,
            _ => false,
        };

        protected override void AcquireCore(LockType type)
        {
            Debug.Assert(CanAcquire(type));

            switch (type)
            {
                case LockType.WeakReadLock:
                    readerCount += 1U;
                    break;
                case LockType.StrongReadLock:
                    allowWrite = false;
                    readerCount += 1U;
                    break;
                case LockType.WriteLock:
                    allowWrite = false;
                    break;
                case LockType.CompactionLock:
                    readerCount = uint.MaxValue;
                    break;
                case LockType.ExclusiveLock:
                    readerCount = uint.MaxValue;
                    allowWrite = false;
                    break;
                default:
                    Debug.Fail($"Unexpected lock type {type}");
                    break;
            }
        }

        internal new ValueTask AcquireAsync(LockType type, CancellationToken token = default)
        {
            Debug.Assert(type is >= LockType.WeakReadLock and <= LockType.ExclusiveLock);

            return base.AcquireAsync(type, token);
        }

        internal new void Release(LockType type)
        {
            Debug.Assert(type is >= LockType.WeakReadLock and <= LockType.ExclusiveLock);

            base.Release(type);
        }

        protected override void ReleaseCore(LockType type)
        {
            switch (type)
            {
                case LockType.WeakReadLock:
                    Debug.Assert(readerCount > 0L);

                    readerCount -= 1U;
                    break;
                case LockType.StrongReadLock:
                    Debug.Assert(readerCount > 0U);
                    Debug.Assert(!allowWrite);

                    readerCount -= 1U;
                    allowWrite = true;
                    break;
                case LockType.WriteLock:
                    Debug.Assert(!allowWrite);

                    allowWrite = true;
                    break;
                case LockType.CompactionLock:
                    Debug.Assert(readerCount is uint.MaxValue);

                    readerCount = 0U;
                    break;
                case LockType.ExclusiveLock:
                    Debug.Assert(readerCount is uint.MaxValue);
                    Debug.Assert(!allowWrite);

                    readerCount = 0U;
                    allowWrite = true;
                    break;
                default:
                    Debug.Fail($"Unexpected lock type {type}");
                    break;
            }
        }

        internal new bool TryAcquire(LockType type)
        {
            Debug.Assert(type is >= LockType.WeakReadLock and <= LockType.ExclusiveLock);

            return base.TryAcquire(type);
        }
    }
}