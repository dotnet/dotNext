using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents a synchronization primitive that is signaled when its count becomes non zero.
    /// </summary>
    /// <remarks>
    /// This class behaves in opposite to <see cref="AsyncCountdownEvent"/>.
    /// Every call of <see cref="Increment"/> increments the counter.
    /// Every call of <see cref="WaitAsync(TimeSpan, CancellationToken)"/>
    /// decrements counter and release the caller if the current count is greater than zero.
    /// </remarks>
    public class AsyncCounter : QueuedSynchronizer, IAsyncEvent
    {
        private struct LockManager : ILockManager<WaitNode>
        {
            private long counter;

            internal LockManager(long value) => counter = value;

            internal long Count => counter.VolatileRead();

            internal long Increment() => counter.IncrementAndGet();

            internal long Decrement() => counter.DecrementAndGet();

            internal bool Reset() => Interlocked.Exchange(ref counter, 0L) > 0L;

            readonly WaitNode ILockManager<WaitNode>.CreateNode(WaitNode? tail)
                => tail is null ? new WaitNode() : new WaitNode(tail);

            bool ILockManager<WaitNode>.TryAcquire()
            {
                if (counter == 0)
                    return false;
                counter -= 1;
                return true;
            }
        }

        private LockManager manager;

        /// <summary>
        /// Initializes a new asynchronous counter.
        /// </summary>
        /// <param name="initialValue">The initial value of the counter.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialValue"/> is less than zero.</exception>
        public AsyncCounter(long initialValue = 0L)
        {
            if (initialValue < 0L)
                throw new ArgumentOutOfRangeException(nameof(initialValue));
            manager = new LockManager(initialValue);
        }

        /// <inheritdoc/>
        bool IAsyncEvent.IsSet => Value > 0;

        /// <summary>
        /// Gets the counter value.
        /// </summary>
        /// <remarks>
        /// The returned value indicates how many calls you can perform
        /// using <see cref="WaitAsync(TimeSpan, CancellationToken)"/> without
        /// blocking.
        /// </remarks>
        public long Value => manager.Count;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        bool IAsyncEvent.Reset() => manager.Reset();

        /// <summary>
        /// Increments counter and resume suspended callers.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This object is disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Increment()
        {
            ThrowIfDisposed();
            manager.Increment();
            for (WaitNode? current = head, next; current is not null && manager.Count > 0L; manager.Decrement(), current = next)
            {
                next = current.Next;
                RemoveNode(current);
                current.Complete();
            }
        }

        /// <inheritdoc/>
        bool IAsyncEvent.Signal()
        {
            Increment();
            return true;
        }

        /// <summary>
        /// Suspends caller if <see cref="Value"/> is zero
        /// or just decrements it.
        /// </summary>
        /// <param name="timeout">Time to wait for increment.</param>
        /// <param name="token">The token that can be used to cancel the waiting operation.</param>
        /// <returns><see langword="true"/> if counter is decremented successfully; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">This object is disposed.</exception>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token)
            => WaitAsync<WaitNode, LockManager>(ref manager, timeout, token);
    }
}
