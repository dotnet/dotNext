using System;
using System.Threading;
using System.Runtime.InteropServices;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents lightweight reader-writer lock based on spin loop.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct ReaderWriterSpinLock
    {
        private const int WriteLockState = int.MinValue;
        private const int NoLockState = default;

        private int state;  //volatile

        public bool IsLockHeld => state.VolatileRead() != NoLockState;

        public bool IsWriteLockHeld => state.VolatileRead() == WriteLockState;

        public bool IsReadLockHeld => state.VolatileRead() > NoLockState;

        /// <summary>
        /// Gets the total number of unique threads that have entered the lock in read mode.
        /// </summary>
        public int CurrentReadCount => Math.Max(0, state.VolatileRead());

        public void EnterReadLock()
        {
            for (var spinner = new SpinWait(); ;)
            {
                var currentState = state.VolatileRead();
                if (currentState == WriteLockState)
                    spinner.SpinOnce();
                else if (state.CompareAndSet(currentState, checked(currentState + 1)))
                    break;
            }
        }

        public void ExitReadLock() => state.DecrementAndGet();

        private bool TryEnterReadLock(Timeout timeout, CancellationToken token)
        {
            SpinWait spinner;
            for (int currentState; !timeout.IsExpired; token.ThrowIfCancellationRequested())
                if ((currentState = state.VolatileRead()) == WriteLockState)
                    spinner.SpinOnce();
                else if (state.CompareAndSet(currentState, checked(currentState + 1)))
                    return true;
            return false;
        }

        public bool TryEnterReadLock(TimeSpan timeout, CancellationToken token = default)
            => TryEnterReadLock(new Timeout(timeout), token);

        public void EnterWriteLock()
        {
            for (var spinner = new SpinWait(); ;)
                if (state.VolatileRead() > NoLockState)
                    spinner.SpinOnce();
                else if (state.CompareAndSet(NoLockState, WriteLockState))
                    break;
        }

        private bool TryEnterWriteLock(Timeout timeout, CancellationToken token)
        {
            for (var spinner = new SpinWait(); !timeout.IsExpired; token.ThrowIfCancellationRequested())
                if (state.VolatileRead() > NoLockState)
                    spinner.SpinOnce();
                else if (state.CompareAndSet(NoLockState, WriteLockState))
                    return true;
            return false;
        }

        public bool TryEnterWriteLock(TimeSpan timeout, CancellationToken token = default)
            => TryEnterWriteLock(new Timeout(timeout), token);

        public void ExitWriteLock() => state.VolatileWrite(NoLockState);
    }
}
