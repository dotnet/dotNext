using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Threading;

partial class WriteAheadLog
{
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private readonly LockManager lockManager;

#if DEBUG
    internal
#else 
    private
#endif
    enum LockType
    {
        /// <summary>
        /// Allows reading of the log entries.
        /// </summary>
        /// <remarks>
        /// Cannot be acquired concurrently with <see cref="Overwrite"/>, <see cref="ReadBarrier"/>.
        /// </remarks>
        Read = 0,

        /// <summary>
        /// Allows the infrastructure to remove the entries applied to the snapshot.
        /// </summary>
        /// <remarks>
        /// Cannot be acquired concurrently with <see cref="Read"/>, <see cref="ReadBarrier"/>, <see cref="Overwrite"/>.
        /// </remarks>
        ReadBarrier,

        /// <summary>
        /// Allows appending of a new entries to the end of the log.
        /// </summary>
        /// <remarks>
        /// Cannot be acquired concurrently with <see cref="Append"/>, <see cref="Overwrite"/>.
        /// </remarks>
        Append,
        
        /// <summary>
        /// Allows committing of the log entries.
        /// </summary>
        /// <remarks>
        /// Cannot be acquired concurrently with <see cref="Commit"/> and <see cref="Overwrite"/>.
        /// </remarks>
        Commit,

        /// <summary>
        /// Allows overwriting of the existing uncommitted entries. 
        /// </summary>
        /// <remarks>
        /// Cannot be acquired concurrently with <see cref="Append"/>, <see cref="Overwrite"/>, <see cref="Read"/>,
        /// <see cref="Commit"/>, <see cref="ReadBarrier"/>.
        /// </remarks>
        Overwrite,
    }

#if DEBUG
    internal
#else 
    private
#endif
    sealed class LockManager(int concurrencyLevel) : QueuedSynchronizer<LockType>(concurrencyLevel)
    {
        private ulong readersCount;
        private bool appendLockState, overwriteLockState, commitLockState;

        protected override bool CanAcquire(LockType type) => type switch
        {
            LockType.Read => !overwriteLockState,
            LockType.ReadBarrier => !overwriteLockState && readersCount is 0U,
            LockType.Append => !appendLockState,
            LockType.Commit => !overwriteLockState && !commitLockState,
            LockType.Overwrite => appendLockState && !overwriteLockState && !commitLockState && readersCount is 0UL,
            _ => false
        };

        protected override void AcquireCore(LockType type)
        {
            switch (type)
            {
                case LockType.Read:
                    readersCount++;
                    break;
                case LockType.ReadBarrier:
                    readersCount = 1L;
                    break;
                case LockType.Append:
                    appendLockState = true;
                    break;
                case LockType.Commit:
                    commitLockState = true;
                    break;
                case LockType.Overwrite:
                    overwriteLockState = true;
                    break;
                default:
                    Debug.Fail($"Unexpected lock type {type}");
                    break;
            }
        }

        protected override void ReleaseCore(LockType type)
        {
            switch (type)
            {
                case LockType.Read or LockType.ReadBarrier:
                    readersCount--;
                    break;
                case LockType.Append or LockType.Overwrite:
                    appendLockState = overwriteLockState = false;
                    break;
                case LockType.Commit:
                    commitLockState = false;
                    break;
                default:
                    Debug.Fail($"Unexpected lock type {type}");
                    break;
            }
        }

        public ValueTask AcquireReadLockAsync(CancellationToken token = default)
            => AcquireAsync(LockType.Read, token);

        public void ReleaseReadLock() => Release(LockType.Read);
        
        public ValueTask AcquireReadBarrierAsync(CancellationToken token = default)
            => AcquireAsync(LockType.ReadBarrier, token);

        public ValueTask AcquireAppendLockAsync(CancellationToken token = default)
            => AcquireAsync(LockType.Append, token);

        public void ReleaseAppendLock() => Release(LockType.Append);

        public ValueTask AcquireCommitLockAsync(CancellationToken token = default)
            => AcquireAsync(LockType.Commit, token);

        public bool TryAcquireCommitLock()
            => TryAcquire(LockType.Commit);
        
        public void ReleaseCommitLock() => Release(LockType.Commit);

        public ValueTask UpgradeToOverwriteLockAsync(CancellationToken token = default)
            => AcquireAsync(LockType.Overwrite, token);
    }
}