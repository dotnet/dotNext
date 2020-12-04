using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using Generic;
    using Tasks;

    /// <summary>
    /// Provides a framework for implementing asynchronous locks and related synchronization primitives that doesn't rely on first-in-first-out (FIFO) wait queues.
    /// </summary>
    /// <remarks>
    /// Derived synchronization primitives more efficient in terms of memory pressure in comparison with <see cref="QueuedSynchronizer">queued synchronization primitives</see>.
    /// It shares the same instance of <see cref="Task{TResult}"/> under contention for all waiters.
    /// </remarks>
    public abstract class Synchronizer : Disposable, ISynchronizer
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct PredicateCondition<T> : IConvertible<bool>
        {
            private readonly T arg;
            private readonly Predicate<T> condition;

            internal PredicateCondition(Predicate<T> condition, T arg)
            {
                this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
                this.arg = arg;
            }

            bool IConvertible<bool>.Convert() => condition(arg);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct FuncCondition<T1, T2> : IConvertible<bool>
        {
            private readonly T1 arg1;
            private readonly T2 arg2;
            private readonly Func<T1, T2, bool> condition;

            internal FuncCondition(Func<T1, T2, bool> condition, T1 arg1, T2 arg2)
            {
                this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
                this.arg1 = arg1;
                this.arg2 = arg2;
            }

            bool IConvertible<bool>.Convert() => condition(arg1, arg2);
        }

        private protected volatile ISynchronizer.WaitNode? node; // null means signaled state

        private protected Synchronizer()
        {
        }

        /// <inheritdoc/>
        bool ISynchronizer.HasWaiters => !(node is null);

        /// <summary>
        /// Determines whether this event in signaled state.
        /// </summary>
        public bool IsSet => node is null;

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
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token)
        {
            if (IsDisposed)
                return GetDisposedTask<bool>();

            if (node is null)
                return CompletedTask<bool, BooleanConst.True>.Task;

            return node.Task.WaitAsync(timeout, token);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private Task<bool> WaitAsync<TCondition>(ref TCondition condition, TimeSpan timeout, CancellationToken token)
            where TCondition : struct, IConvertible<bool>
        {
            if (IsDisposed)
                return GetDisposedTask<bool>();

            if (node is null || condition.Convert())
                return CompletedTask<bool, BooleanConst.True>.Task;

            return node.Task.WaitAsync(timeout, token);
        }

        /// <summary>
        /// Suspends the caller until this event is set.
        /// </summary>
        /// <remarks>
        /// If given predicate returns true then caller will not be suspended.
        /// </remarks>
        /// <typeparam name="T">The type of predicate parameter.</typeparam>
        /// <param name="condition">Additional condition that must be checked before suspension.</param>
        /// <param name="arg">The argument to be passed to the predicate.</param>
        /// <param name="timeout">The number of time to wait before this event is set.</param>
        /// <param name="token">The token that can be used to cancel waiting operation.</param>
        /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> WaitAsync<T>(Predicate<T> condition, T arg, TimeSpan timeout, CancellationToken token = default)
        {
            var cond = new PredicateCondition<T>(condition, arg);
            return WaitAsync(ref cond, timeout, token);
        }

        /// <summary>
        /// Suspends the caller until this event is set.
        /// </summary>
        /// <remarks>
        /// If given predicate returns true then caller will not be suspended.
        /// </remarks>
        /// <typeparam name="T">The type of predicate parameter.</typeparam>
        /// <param name="condition">Additional condition that must be checked before suspension.</param>
        /// <param name="arg">The argument to be passed to the predicate.</param>
        /// <param name="token">The token that can be used to cancel waiting operation.</param>
        /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task WaitAsync<T>(Predicate<T> condition, T arg, CancellationToken token = default)
            => WaitAsync(condition, arg, InfiniteTimeSpan, token);

        /// <summary>
        /// Suspends the caller until this event is set.
        /// </summary>
        /// <remarks>
        /// If given predicate returns true then caller will not be suspended.
        /// </remarks>
        /// <typeparam name="T1">The type of the first predicate parameter.</typeparam>
        /// <typeparam name="T2">The type of the second predicate parameter.</typeparam>
        /// <param name="condition">Additional condition that must be checked before suspension.</param>
        /// <param name="arg1">The first argument to be passed to the predicate.</param>
        /// <param name="arg2">The second argument to be passed to the predicate.</param>
        /// <param name="timeout">The number of time to wait before this event is set.</param>
        /// <param name="token">The token that can be used to cancel waiting operation.</param>
        /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> WaitAsync<T1, T2>(Func<T1, T2, bool> condition, T1 arg1, T2 arg2, TimeSpan timeout, CancellationToken token = default)
        {
            var cond = new FuncCondition<T1, T2>(condition, arg1, arg2);
            return WaitAsync(ref cond, timeout, token);
        }

        /// <summary>
        /// Suspends the caller until this event is set.
        /// </summary>
        /// <remarks>
        /// If given predicate returns true then caller will not be suspended.
        /// </remarks>
        /// <typeparam name="T1">The type of the first predicate parameter.</typeparam>
        /// <typeparam name="T2">The type of the second predicate parameter.</typeparam>
        /// <param name="condition">Additional condition that must be checked before suspension.</param>
        /// <param name="arg1">The first argument to be passed to the predicate.</param>
        /// <param name="arg2">The second argument to be passed to the predicate.</param>
        /// <param name="token">The token that can be used to cancel waiting operation.</param>
        /// <returns><see langword="true"/>, if this event was set; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task WaitAsync<T1, T2>(Func<T1, T2, bool> condition, T1 arg1, T2 arg2, CancellationToken token = default)
            => WaitAsync(condition, arg1, arg2, InfiniteTimeSpan, token);

        /// <summary>
        /// Releases all resources associated with exclusive lock.
        /// </summary>
        /// <remarks>
        /// This method is not thread-safe and may not be used concurrently with other members of this instance.
        /// </remarks>
        /// <param name="disposing">Indicates whether the <see cref="Dispose(bool)"/> has been called directly or from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Exchange(ref node, null)?.TrySetCanceled();
            }

            base.Dispose(disposing);
        }
    }
}