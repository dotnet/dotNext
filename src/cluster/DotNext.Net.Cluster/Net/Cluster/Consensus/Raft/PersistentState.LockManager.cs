using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Threading;

    public partial class PersistentState
    {
        private sealed class LockState
        {
            private readonly uint maxReadCount;
            private uint readerCount;
            private bool allowWrite;

            internal LockState(int concurrencyLevel)
            {
                maxReadCount = (uint)concurrencyLevel;
                allowWrite = true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryAcquireStrongReadLock()
            {
                if (readerCount < maxReadCount && allowWrite)
                {
                    allowWrite = false;
                    readerCount += 1U;
                    return true;
                }

                return false;
            }

            private void ReleaseStrongReadLock()
            {
                Debug.Assert(readerCount > 0U);
                Debug.Assert(!allowWrite);

                readerCount -= 1U;
                allowWrite = true;
            }

            internal static bool TryAcquireStrongReadLock(LockState state)
                => state.TryAcquireStrongReadLock();

            internal static void ReleaseStrongReadLock(LockState state)
                => state.ReleaseStrongReadLock();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryAcquireWeakReadLock()
            {
                if (readerCount < maxReadCount)
                {
                    readerCount += 1U;
                    return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseWeakReadLock()
            {
                Debug.Assert(readerCount > 0L);

                readerCount -= 1U;
            }

            internal static bool TryAcquireWeakReadLock(LockState state)
                => state.TryAcquireWeakReadLock();

            internal static void ReleaseWeakReadLock(LockState state)
                => state.ReleaseWeakReadLock();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryAcquireWriteLock()
            {
                if (allowWrite)
                {
                    allowWrite = false;
                    return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseWriteLock()
            {
                Debug.Assert(!allowWrite);

                allowWrite = true;
            }

            internal static bool TryAcquireWriteLock(LockState state)
                => state.TryAcquireWriteLock();

            internal static void ReleaseWriteLock(LockState state)
                => state.ReleaseWriteLock();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryAcquireCompactionLock()
            {
                if (readerCount == 0U)
                {
                    readerCount = uint.MaxValue;
                    return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseCompactionLock()
            {
                Debug.Assert(readerCount == uint.MaxValue);

                readerCount = 0U;
            }

            internal static bool TryAcquireCompactionLock(LockState state)
                => state.TryAcquireCompactionLock();

            internal static void ReleaseCompactionLock(LockState state)
                => state.ReleaseCompactionLock();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryAcquireExclusiveLock()
            {
                if (readerCount == 0L && allowWrite)
                {
                    readerCount = uint.MaxValue;
                    allowWrite = false;
                    return true;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ReleaseExclusiveLock()
            {
                Debug.Assert(readerCount == uint.MaxValue);
                Debug.Assert(!allowWrite);

                readerCount = 0U;
                allowWrite = true;
            }

            internal static bool TryAcquireExclusiveLock(LockState state)
                => state.TryAcquireExclusiveLock();

            internal static void ReleaseExclusiveLock(LockState state)
                => state.ReleaseExclusiveLock();
        }

        internal interface IWriteLock
        {
            long Version { get; }

            void Release(long version);
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
        internal sealed class LockManager : AsyncTrigger, IWriteLock
        {
            private readonly LockState state;
            private readonly Predicate<LockState>[] lockAcquisition = { LockState.TryAcquireWeakReadLock, LockState.TryAcquireStrongReadLock, LockState.TryAcquireWriteLock, LockState.TryAcquireCompactionLock, LockState.TryAcquireExclusiveLock };
            private readonly Action<LockState>[] lockRelease = { LockState.ReleaseWeakReadLock, LockState.ReleaseStrongReadLock, LockState.ReleaseWriteLock, LockState.ReleaseCompactionLock, LockState.ReleaseExclusiveLock };
            private long lockVersion; // volatile

            internal LockManager(IAsyncLockSettings configuration)
            {
                state = new(configuration.ConcurrencyLevel);

                lockVersion = long.MinValue;

                // setup metrics
                if (configuration.LockContentionCounter is not null)
                    LockContentionCounter = configuration.LockContentionCounter;
                if (configuration.LockDurationCounter is not null)
                    LockDurationCounter = configuration.LockDurationCounter;
            }

            internal Task AcquireAsync(LockType type, CancellationToken token = default)
            {
                Debug.Assert(type >= LockType.WeakReadLock && type <= LockType.ExclusiveLock);

                var acquisition = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(lockAcquisition), (int)type);
                return WaitAsync(state, acquisition, token);
            }

            internal Task<bool> AcquireAsync(LockType type, TimeSpan timeout, CancellationToken token = default)
            {
                Debug.Assert(type >= LockType.WeakReadLock && type <= LockType.ExclusiveLock);

                var acquisition = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(lockAcquisition), (int)type);
                return WaitAsync(state, acquisition, timeout, token);
            }

            internal void Release(LockType type)
            {
                Debug.Assert(type >= LockType.WeakReadLock && type <= LockType.ExclusiveLock);

                var release = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(lockRelease), (int)type);
                Signal(state, release, true);
            }

            long IWriteLock.Version => lockVersion.VolatileRead();

            void IWriteLock.Release(long version)
            {
                if (lockVersion.CompareAndSet(version, version + 1L))
                    Release(LockType.ExclusiveLock);
                else
                    Debug.Fail(ExceptionMessages.InvalidLockToken);
            }
        }
    }
}