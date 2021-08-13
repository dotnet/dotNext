using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents asynchronous version of <see cref="AutoResetEvent"/>.
    /// </summary>
    public class AsyncAutoResetEvent : QueuedSynchronizer, IAsyncResetEvent
    {
        private struct LockManager : ILockManager<WaitNode>
        {
            private AtomicBoolean state;

            public bool TryAcquire() => state.TrueToFalse();

            internal bool IsSignaled
            {
                readonly get => state.Value;
                set => state.Value = value;
            }

            readonly WaitNode ILockManager<WaitNode>.CreateNode() => new WaitNode();
        }

        private LockManager manager;

        /// <summary>
        /// Initializes a new asynchronous reset event in the specified state.
        /// </summary>
        /// <param name="initialState"><see langword="true"/> to set the initial state signaled; <see langword="false"/> to set the initial state to non signaled.</param>
        public AsyncAutoResetEvent(bool initialState) => manager = new LockManager { IsSignaled = initialState };

        /// <summary>
        /// Gets whether this event is set.
        /// </summary>
        public bool IsSet => manager.IsSignaled;

        /// <summary>
        /// Sets the state of this event to non signaled, causing consumers to wait asynchronously.
        /// </summary>
        /// <returns><see langword="true"/> if the operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Reset()
        {
            ThrowIfDisposed();
            return manager.TryAcquire();
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
            if (manager.IsSignaled)
                return false;

            if (first is null)
                return manager.IsSignaled = true;

            first.SetResult();
            RemoveNode(first);
            manager.IsSignaled = false;
            return true;
        }

        /// <inheritdoc/>
        bool IAsyncEvent.Signal() => Set();

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
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token)
            => WaitAsync<WaitNode, LockManager>(ref manager, timeout, false, token);
    }
}