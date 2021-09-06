using System.Runtime.CompilerServices;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
    using Tasks.Pooling;

    /// <summary>
    /// Represents asynchronous version of <see cref="AutoResetEvent"/>.
    /// </summary>
    public class AsyncAutoResetEvent : QueuedSynchronizer, IAsyncResetEvent
    {
        private readonly Func<DefaultWaitNode> pool;
        private AtomicBoolean state;

        /// <summary>
        /// Initializes a new asynchronous reset event in the specified state.
        /// </summary>
        /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to non signaled.</param>
        /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
        public AsyncAutoResetEvent(bool initialState, int concurrencyLevel)
        {
            if (concurrencyLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

            state.Value = initialState;
            pool = new ConstrainedValueTaskPool<DefaultWaitNode>(concurrencyLevel).Get;
        }

        /// <summary>
        /// Initializes a new asynchronous reset event in the specified state.
        /// </summary>
        /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to non signaled.</param>
        public AsyncAutoResetEvent(bool initialState)
        {
            state.Value = initialState;
            pool = new UnconstrainedValueTaskPool<DefaultWaitNode>().Get;
        }

        private static bool TryReset(ref AtomicBoolean state) => state.TrueToFalse();

        /// <summary>
        /// Indicates whether this event is set.
        /// </summary>
        public bool IsSet => state.Value;

        /// <summary>
        /// Sets the state of this event to non signaled, causing consumers to wait asynchronously.
        /// </summary>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Reset()
        {
            ThrowIfDisposed();
            return TryReset(ref state);
        }

        private void SetCore()
        {
            Debug.Assert(Monitor.IsEntered(this));

            for (WaitNode? current = first as WaitNode, next; ; current = next)
            {
                if (current is null)
                {
                    state.Value = true;
                    break;
                }

                next = current.Next as WaitNode;
                RemoveNode(current);

                // skip dead node
                if (current.TrySetResult(true))
                    break;
            }
        }

        /// <summary>
        /// Sets the state of the event to signaled, allowing one or more awaiters to proceed.
        /// </summary>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Set()
        {
            ThrowIfDisposed();

            if (state.Value)
                return false;

            SetCore();
            return true;
        }

        /// <inheritdoc/>
        bool IAsyncEvent.Pulse() => Set();

        /// <inheritdoc/>
        EventResetMode IAsyncResetEvent.ResetMode => EventResetMode.AutoReset;

        /// <summary>
        /// Turns caller into idle state until the current event is set.
        /// </summary>
        /// <param name="timeout">The interval to wait for the signaled state.</param>
        /// <param name="token">The token that can be used to abort wait process.</param>
        /// <returns><see langword="true"/> if signaled state was set; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
            => WaitNoTimeoutAsync(ref state, &TryReset, pool, out _, timeout, token);

        /// <summary>
        /// Turns caller into idle state until the current event is set.
        /// </summary>
        /// <param name="token">The token that can be used to abort wait process.</param>
        /// <returns>The task representing asynchronous result.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask WaitAsync(CancellationToken token = default)
            => WaitWithTimeoutAsync(ref state, &TryReset, pool, out _, InfiniteTimeSpan, token);
    }
}