using System.Diagnostics;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Threading;

partial class WriteAheadLog
{
    private readonly LockManager lockManager;

    private enum LockType
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

    private sealed class LockManager(int concurrencyLevel) : QueuedSynchronizer<LockType>(concurrencyLevel)
    {
        private const byte AppendLockState = 1;
        private const byte OverwriteLockState = 2;

        private bool commitState;
        private ulong readersCount;
        private byte writeLockState;

        protected override bool CanAcquire(LockType type) => type switch
        {
            LockType.Read => writeLockState <= AppendLockState,
            LockType.ReadBarrier => writeLockState <= AppendLockState && readersCount is 0U,
            LockType.Append => writeLockState is 0,
            LockType.Commit => !commitState && writeLockState <= AppendLockState,
            LockType.Overwrite => writeLockState <= AppendLockState && readersCount is 0UL && !commitState,
            _ => false
        };

        protected override void AcquireCore(LockType type)
        {
            switch (type)
            {
                case LockType.Read:
                    Debug.Assert(writeLockState <= AppendLockState);
                    readersCount++;
                    break;
                case LockType.ReadBarrier:
                    Debug.Assert(readersCount is 0UL && writeLockState <= AppendLockState);
                    readersCount = 1L;
                    break;
                case LockType.Append:
                    Debug.Assert(writeLockState is 0);
                    writeLockState = AppendLockState;
                    break;
                case LockType.Commit:
                    Debug.Assert(!commitState && writeLockState <= AppendLockState);
                    commitState = true;
                    break;
                case LockType.Overwrite:
                    Debug.Assert(writeLockState <= AppendLockState && readersCount is 0UL && !commitState);
                    writeLockState = OverwriteLockState;
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
                    writeLockState = 0;
                    break;
                case LockType.Commit:
                    commitState = false;
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