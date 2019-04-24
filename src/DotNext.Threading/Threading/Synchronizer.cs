using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    using Tasks;
    using Generic;

    /// <summary>
    /// Provides a framework for implementing asynchronous locks and related synchronizers that doesn't rely on first-in-first-out (FIFO) wait queues.
    /// </summary>
    /// <remarks>
    /// Derived synchronizers more efficient in terms of memory pressure in comparison with <see cref="QueuedSynchronizer">queued synchronizers</see>.
    /// It shares the same instance of <see cref="Task{TResult}"/> under contention for all waiters.
    /// </remarks>
    public abstract class Synchronizer: Disposable, ISynchronizer
    {
        internal class WaitNode: TaskCompletionSource<bool>
        {
            internal WaitNode() : base(TaskCreationOptions.RunContinuationsAsynchronously) { }

            internal void Complete() => SetResult(true);
        }

        private protected volatile WaitNode node;//null means signaled state

        private protected Synchronizer()
        {
        }

        bool ISynchronizer.HasWaiters => !(node is null);

        /// <summary>
        /// Determines whether this event in signaled state.
        /// </summary>
        public bool IsSet => node is null;

        private static async Task<bool> Wait(WaitNode node, TimeSpan timeout, CancellationToken token)
        {
            using (var tokenSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource())
            {
                if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, Task.Delay(timeout, tokenSource.Token)).ConfigureAwait(false)))
                {
                    tokenSource.Cancel();   //ensure that Delay task is cancelled
                    return true;
                }
            }
            token.ThrowIfCancellationRequested();
            return false;
        }

        private static async Task<bool> Wait(WaitNode node, CancellationToken token)
        {
            using (var tracker = new CancelableTaskCompletionSource<bool>(ref token))
                if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, tracker.Task).ConfigureAwait(false)))
                    return true;
                else
                {
                    token.ThrowIfCancellationRequested();
                    return false;
                }
        }

        /// <summary>
        /// Suspends the caller until this event is set.
        /// </summary>
        /// <param name="timeout">The number of time to wait before this event is set.</param>
        /// <param name="token">The token that can be used to cancel waiting operation.</param>
        /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> Wait(TimeSpan timeout, CancellationToken token)
        {
            ThrowIfDisposed();
            if (timeout < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            else if (token.IsCancellationRequested)
                return Task.FromCanceled<bool>(token);
            else if (node is null)
                return CompletedTask<bool, BooleanConst.True>.Task;
            else if (timeout == TimeSpan.Zero)   //if timeout is zero fail fast
                return CompletedTask<bool, BooleanConst.False>.Task;
            else if (timeout == TimeSpan.MaxValue)
                return token.CanBeCanceled ? Wait(node, token) : node.Task;
            else
                return Wait(node, timeout, token);
        }

        

        /// <summary>
        /// Releases all resources associated with exclusive lock.
        /// </summary>
        /// <remarks>
        /// This method is not thread-safe and may not be used concurrently with other members of this instance.
        /// </remarks>
        /// <param name="disposing">Indicates whether the <see cref="Dispose(bool)"/> has been called directly or from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                node?.TrySetCanceled();
                node = null;
            }
        }
    }
}