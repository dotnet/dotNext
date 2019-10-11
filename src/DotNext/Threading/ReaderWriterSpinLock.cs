using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Runtime.InteropServices;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents lightweight reader-writer lock based on spin loop.
    /// </summary>
    /// <remarks>
    /// This type should not be used to synchronize access to the I/O intensive resources.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public struct ReaderWriterSpinLock
    {
        private const int WriteLockState = int.MinValue;
        private const int NoLockState = default;

        private volatile int state;  //volatile

        /// <summary>
        /// Gets a value that indicates whether the current thread has entered the lock in write mode.
        /// </summary>
        public bool IsWriteLockHeld => state == WriteLockState;

        /// <summary>
        /// Gets a value that indicates whether the current thread has entered the lock in read mode.
        /// </summary>
        public bool IsReadLockHeld => state > NoLockState;

        /// <summary>
        /// Gets the total number of unique threads that have entered the lock in read mode.
        /// </summary>
        public int CurrentReadCount => Math.Max(0, state);

        /// <summary>
        /// Enters the lock in read mode.
        /// </summary>
        public void EnterReadLock()
        {
#pragma warning disable CS0420
            for (SpinWait spinner; ;)
            {
                var currentState = state;
                if (currentState == WriteLockState)
                    spinner.SpinOnce();
                else if (state.CompareAndSet(currentState, checked(currentState + 1)))
                    break;
            }
#pragma warning restore CS0420
        }

        /// <summary>
        /// Exits read mode.
        /// </summary>
        public void ExitReadLock() => Interlocked.Decrement(ref state);

        private bool TryEnterReadLock(Timeout timeout, CancellationToken token)
        {
#pragma warning disable CS0420
            SpinWait spinner;
            for (int currentState; !timeout.IsExpired; token.ThrowIfCancellationRequested())
                if ((currentState = state) == WriteLockState)
                    spinner.SpinOnce();
                else if (state.CompareAndSet(currentState, checked(currentState + 1)))
                    return true;
            return false;
#pragma warning restore CS0420
        }

        /// <summary>
        /// Tries to enter the lock in read mode.
        /// </summary>
        /// <param name="timeout">The interval to wait.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if the calling thread entered read mode, otherwise, <see langword="false"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public bool TryEnterReadLock(TimeSpan timeout, CancellationToken token = default)
            => TryEnterReadLock(new Timeout(timeout), token);

        /// <summary>
        /// Enters the lock in write mode.
        /// </summary>
        public void EnterWriteLock()
        {
            for (SpinWait spinner; Interlocked.CompareExchange(ref state, WriteLockState, NoLockState) != NoLockState; spinner.SpinOnce()) { }
        }

        private bool TryEnterWriteLock(Timeout timeout, CancellationToken token)
        {
            for (SpinWait spinner; Interlocked.CompareExchange(ref state, WriteLockState, NoLockState) != NoLockState; spinner.SpinOnce(), token.ThrowIfCancellationRequested())
                if(timeout.IsExpired)
                    return false;
            return true;
        }

        /// <summary>
        /// Tries to enter the lock in write mode.
        /// </summary>
        /// <param name="timeout">The interval to wait.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if the calling thread entered read mode, otherwise, <see langword="false"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public bool TryEnterWriteLock(TimeSpan timeout, CancellationToken token = default)
            => TryEnterWriteLock(new Timeout(timeout), token);

        /// <summary>
        /// Exits the write lock.
        /// </summary>
        public void ExitWriteLock() => state = NoLockState;
    }
}
