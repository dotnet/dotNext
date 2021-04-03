using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;
using Timeout = System.Threading.Timeout;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Threading;

    public partial class PersistentState
    {
        private sealed class LockState
        {
            private const long WriteLock = -1L;
            private const long CompactionLock = -2L;
            private const long WriteAndCompactionLock = -3L;

            private readonly long maxReadCount;
            private long readerCount;

            internal LockState(long concurrencyLevel)
            {
                maxReadCount = concurrencyLevel;
                readerCount = 0L;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryAcquireReadLock()
            {
                if (readerCount.Between(0L, maxReadCount, BoundType.LeftClosed))
                {
                    readerCount += 1L;
                    return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseReadLock()
            {
                Debug.Assert(readerCount > 0L);
                readerCount -= 1L;
            }

            internal static bool TryAcquireReadLock(LockState state)
                => state.TryAcquireReadLock();

            internal static void ReleaseReadLock(LockState state)
                => state.ReleaseReadLock();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryAcquireWriteLock()
            {
                switch (readerCount)
                {
                    default:
                        return false;
                    case 0L:
                        readerCount = WriteLock;
                        break;
                    case CompactionLock:
                        readerCount = WriteAndCompactionLock;
                        break;
                }

                return true;
            }

            internal static bool TryAcquireWriteLock(LockState state)
                => state.TryAcquireWriteLock();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseWriteLock()
            {
                Debug.Assert(readerCount == WriteLock || readerCount == WriteAndCompactionLock);
                switch (readerCount)
                {
                    case WriteLock:
                        readerCount = 0L;
                        break;
                    case WriteAndCompactionLock:
                        readerCount = CompactionLock;
                        break;
                }
            }

            internal static void ReleaseWriteLock(LockState state)
                => state.ReleaseWriteLock();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryAcquireCompactionLock()
            {
                switch (readerCount)
                {
                    default:
                        return false;
                    case 0L:
                        readerCount = CompactionLock;
                        break;
                    case WriteLock:
                        readerCount = WriteAndCompactionLock;
                        break;
                }

                return true;
            }

            internal static bool TryAcquireCompactionLock(LockState state)
                => state.TryAcquireCompactionLock();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseCompactionLock()
            {
                Debug.Assert(readerCount == CompactionLock || readerCount == WriteAndCompactionLock);
                switch (readerCount)
                {
                    case CompactionLock:
                        readerCount = 0L;
                        break;
                    case WriteAndCompactionLock:
                        readerCount = WriteLock;
                        break;
                }
            }

            internal static void ReleaseCompactionLock(LockState state)
                => state.ReleaseCompactionLock();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryAcquireExclusiveLock()
            {
                if (readerCount == 0L)
                {
                    readerCount = WriteAndCompactionLock;
                    return true;
                }

                return false;
            }

            internal static bool TryAcquireExclusiveLock(LockState state)
                => state.TryAcquireExclusiveLock();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseExclusiveLock()
            {
                Debug.Assert(readerCount == WriteAndCompactionLock);
                readerCount = 0L;
            }

            internal static void ReleaseExclusiveLock(LockState state)
                => state.ReleaseExclusiveLock();
        }

        internal interface IWriteLock
        {
            Task AcquireAsync(CancellationToken token);

            void Release();

            long Version { get; }

            void Release(long version);
        }

        // This lock manager implements the following logic:
        // Multiple reads are allowed, but mutually exclusive with write or compaction lock
        // Write lock is mutually exclusive with read lock but can co-exist with compaction lock
        // Compaction lock is mutually exclusive with read lock but can co-exist with write lock
        private sealed class LockManager : AsyncTrigger, IWriteLock
        {
            private readonly LockState state;
            private readonly Predicate<LockState> acquireReadLock, acquireWriteLock, acquireCompactionLock, acquireExclusiveLock;
            private readonly Action<LockState> releaseReadLock, releaseWriteLock, releaseCompactionLock, releaseExclusiveLock;
            private long lockVersion; // volatile

            internal LockManager(long concurrencyLevel)
            {
                state = new LockState(concurrencyLevel);
                acquireReadLock = LockState.TryAcquireReadLock;
                releaseReadLock = LockState.ReleaseReadLock;
                acquireWriteLock = LockState.TryAcquireWriteLock;
                releaseWriteLock = LockState.ReleaseWriteLock;
                acquireCompactionLock = LockState.TryAcquireCompactionLock;
                releaseCompactionLock = LockState.ReleaseCompactionLock;
                acquireExclusiveLock = LockState.TryAcquireExclusiveLock;
                releaseExclusiveLock = LockState.ReleaseExclusiveLock;
                lockVersion = long.MinValue;
            }

            internal Task AcquireExclusiveLockAsync(CancellationToken token = default)
                => WaitAsync(state, acquireExclusiveLock, token);

            internal void ReleaseExclusiveLock()
                => Signal(state, releaseExclusiveLock, true);

            internal Task AcquireReadLockAsync(CancellationToken token)
                => WaitAsync(state, acquireReadLock, token);

            internal void ReleaseReadLock()
                => Signal(state, releaseReadLock, true);

            internal Task<bool> AcquireWriteLockAsync(TimeSpan timeout, CancellationToken token)
                => WaitAsync(state, acquireWriteLock, timeout, token);

            internal Task AcquireWriteLockAsync(CancellationToken token = default)
                => WaitAsync(state, acquireWriteLock, token);

            Task IWriteLock.AcquireAsync(CancellationToken token) => AcquireWriteLockAsync(token);

            internal void ReleaseWriteLock()
                => Signal(state, releaseWriteLock, true);

            void IWriteLock.Release() => ReleaseWriteLock();

            internal Task AcquireCompactionLockAsync(CancellationToken token)
                => WaitAsync(state, acquireCompactionLock, token);

            internal void ReleaseCompactionLock()
                => Signal(state, releaseCompactionLock, true);

            long IWriteLock.Version => lockVersion.VolatileRead();

            void IWriteLock.Release(long version)
            {
                if (lockVersion.CompareAndSet(version, version + 1L))
                    ReleaseWriteLock();
                else
                    Debug.Fail(ExceptionMessages.InvalidLockToken);
            }
        }
    }
}