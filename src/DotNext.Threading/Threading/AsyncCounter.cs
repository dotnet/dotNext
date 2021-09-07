using System.Runtime.CompilerServices;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
    using Tasks.Pooling;

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
        private readonly Func<DefaultWaitNode> pool;
        private long counter;

        /// <summary>
        /// Initializes a new asynchronous counter.
        /// </summary>
        /// <param name="initialValue">The initial value of the counter.</param>
        /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
        public AsyncCounter(long initialValue, int concurrencyLevel)
        {
            if (initialValue < 0L)
                throw new ArgumentOutOfRangeException(nameof(initialValue));

            if (concurrencyLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

            counter = initialValue;
            pool = new ConstrainedValueTaskPool<DefaultWaitNode>(concurrencyLevel, RemoveAndDrainWaitQueue).Get;
        }

        /// <summary>
        /// Initializes a new asynchronous counter.
        /// </summary>
        /// <param name="initialValue">The initial value of the counter.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialValue"/> is less than zero.</exception>
        public AsyncCounter(long initialValue = 0L)
        {
            if (initialValue < 0L)
                throw new ArgumentOutOfRangeException(nameof(initialValue));

            counter = initialValue;
            pool = new UnconstrainedValueTaskPool<DefaultWaitNode>(RemoveAndDrainWaitQueue).Get;
        }

        private static bool TryDecrement(ref long counter)
        {
            if (counter.VolatileRead() == 0L)
                return false;

            counter.DecrementAndGet();
            return true;
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
        public long Value => counter.VolatileRead();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        bool IAsyncEvent.Reset() => Interlocked.Exchange(ref counter, 0L) > 0L;

        /// <summary>
        /// Increments counter and resume suspended callers.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This object is disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Increment()
        {
            ThrowIfDisposed();
            counter = checked(counter + 1L);

            for (WaitNode? current = first as WaitNode, next; current is not null && counter.VolatileRead() > 0L; current = next)
            {
                next = current.Next as WaitNode;

                if (current.TrySetResult(true))
                {
                    RemoveNode(current);
                    counter.DecrementAndGet();
                }
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
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
            => WaitNoTimeoutAsync(ref counter, &TryDecrement, pool, out _, timeout, token);

        /// <summary>
        /// Suspends caller if <see cref="Value"/> is zero
        /// or just decrements it.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the waiting operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">This object is disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask WaitAsync(CancellationToken token = default)
            => WaitWithTimeoutAsync(ref counter, &TryDecrement, pool, out _, InfiniteTimeSpan, token);

        /// <summary>
        /// Attempts to decrement the counter synchronously.
        /// </summary>
        /// <returns><see langword="true"/> if the counter decremented successfully; <see langword="false"/> if this counter is already zero.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryDecrement()
        {
            ThrowIfDisposed();
            return TryDecrement(ref counter);
        }
    }
}
