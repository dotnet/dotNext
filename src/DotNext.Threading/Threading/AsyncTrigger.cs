using System.Runtime.CompilerServices;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
    using Tasks.Pooling;

    /// <summary>
    /// Represents asynchronous trigger that allows to resume and suspend
    /// concurrent flows.
    /// </summary>
    public class AsyncTrigger : QueuedSynchronizer, IAsyncEvent
    {
        private readonly ValueTaskPool<DefaultWaitNode> pool;

        /// <summary>
        /// Initializes a new trigger.
        /// </summary>
        public AsyncTrigger()
        {
            pool = new(RemoveAndDrainWaitQueue);
        }

        /// <summary>
        /// Initializes a new trigger.
        /// </summary>
        /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
        public AsyncTrigger(int concurrencyLevel)
        {
            if (concurrencyLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

            pool = new(concurrencyLevel, RemoveAndDrainWaitQueue);
        }

        /// <inheritdoc/>
        bool IAsyncEvent.Reset() => false;

        private bool SignalCore()
        {
            Debug.Assert(Monitor.IsEntered(this));

            for (WaitNode? current = first as WaitNode, next; current is not null; current = next)
            {
                next = current.Next as WaitNode;

                if (current.IsCompleted)
                {
                    RemoveNode(current);
                    continue;
                }

                if (current.TrySetResult(true))
                {
                    RemoveNode(current);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resumes the first suspended caller in the wait queue.
        /// </summary>
        /// <param name="resumeAll">
        /// <see langword="true"/> to resume the first suspended caller in the queue;
        /// <see langword="false"/> to resume all suspended callers in the queue.
        /// </param>
        /// <returns><see langword="true"/> if at least one suspended caller has been resumed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Signal(bool resumeAll = false)
        {
            ThrowIfDisposed();

            return resumeAll ? ResumeSuspendedCallers() > 0L : SignalCore();
        }

        /// <inheritdoc/>
        bool IAsyncEvent.IsSet => first is null;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        bool IAsyncEvent.Signal() => SignalCore();

        private static void AlwaysFalse(ref ValueTuple timeout, ref bool flag)
        {
        }

        /// <summary>
        /// Suspends the caller and waits for the signal.
        /// </summary>
        /// <remarks>
        /// This method always suspends the caller.
        /// </remarks>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="Signal"/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
        {
            var tuple = ValueTuple.Create();
            return WaitNoTimeoutAsync(ref tuple, &AlwaysFalse, pool, out _, timeout, token);
        }

        /// <summary>
        /// Suspends the caller and waits for the signal.
        /// </summary>
        /// <remarks>
        /// This method always suspends the caller.
        /// </remarks>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="Signal"/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask WaitAsync(CancellationToken token = default)
        {
            var tuple = ValueTuple.Create();
            return WaitWithTimeoutAsync(ref tuple, &AlwaysFalse, pool, out _, InfiniteTimeSpan, token);
        }

        /// <summary>
        /// Resumes the first suspended caller in the queue and suspends the immediate caller.
        /// </summary>
        /// <param name="resumeAll">
        /// <see langword="true"/> to resume the first suspended caller in the queue;
        /// <see langword="false"/> to resume all suspended callers in the queue.
        /// </param>
        /// <param name="throwOnEmptyQueue">
        /// <see langword="true"/> to throw <see cref="InvalidOperationException"/> if there is no suspended callers to resume;
        /// <see langword="false"/> to suspend the caller.
        /// </param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="throwOnEmptyQueue"/> is <see langword="true"/> and no suspended callers in the queue.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ValueTask<bool> SignalAndWaitAsync(bool resumeAll, bool throwOnEmptyQueue, TimeSpan timeout, CancellationToken token = default)
        {
            ThrowIfDisposed();
            return !Signal(resumeAll) && throwOnEmptyQueue
                ? ValueTask.FromException<bool>(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue))
                : WaitAsync(timeout, token);
        }

        /// <summary>
        /// Resumes the first suspended caller in the queue and suspends the immediate caller.
        /// </summary>
        /// <param name="resumeAll">
        /// <see langword="true"/> to resume the first suspended caller in the queue;
        /// <see langword="false"/> to resume all suspended callers in the queue.
        /// </param>
        /// <param name="throwOnEmptyQueue">
        /// <see langword="true"/> to throw <see cref="InvalidOperationException"/> if there is no suspended callers to resume;
        /// <see langword="false"/> to suspend the caller.
        /// </param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="throwOnEmptyQueue"/> is <see langword="true"/> and no suspended callers in the queue.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ValueTask SignalAndWaitAsync(bool resumeAll, bool throwOnEmptyQueue, CancellationToken token = default)
        {
            ThrowIfDisposed();
            return !Signal(resumeAll) && throwOnEmptyQueue
                ? ValueTask.FromException(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue))
                : WaitAsync(token);
        }
    }

    /// <summary>
    /// Represents asynchronous trigger that allows to resume and suspend
    /// concurrent flows.
    /// </summary>
    /// <typeparam name="TState">The type of the state used for coordination.</typeparam>
    public class AsyncTrigger<TState> : QueuedSynchronizer
        where TState : class
    {
        /// <summary>
        /// Represents state transition.
        /// </summary>
        public interface ITransition
        {
            /// <summary>
            /// Tests whether the state can be changed.
            /// </summary>
            /// <param name="state">The state to check.</param>
            /// <returns><see langword="true"/> if transition is allowed; otherwise, <see langword="false"/>.</returns>
            bool Test(TState state);

            /// <summary>
            /// Do transition.
            /// </summary>
            /// <param name="state">The state to modify.</param>
            void Transit(TState state);
        }

        private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IPooledManualResetCompletionSource<WaitNode>
        {
            private readonly Action<WaitNode> backToPool;
            internal volatile ITransition? Transition;

            private WaitNode(Action<WaitNode> backToPool) => this.backToPool = backToPool;

            protected override void AfterConsumed()
            {
                Transition = null;
                backToPool(this);
            }

            static WaitNode IPooledManualResetCompletionSource<WaitNode>.CreateSource(Action<WaitNode> backToPool) => new(backToPool);
        }

        private readonly ValueTaskPool<WaitNode> pool;

        /// <summary>
        /// Initializes a new trigger.
        /// </summary>
        /// <param name="state">The coordination state.</param>
        public AsyncTrigger(TState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            pool = new(RemoveAndDrainWaitQueue);
        }

        /// <summary>
        /// Initializes a new trigger.
        /// </summary>
        /// <param name="state">The coordination state.</param>
        /// <param name="concurrencyLevel">The expected number of concurrent flows.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than or equal to zero.</exception>
        public AsyncTrigger(TState state, int concurrencyLevel)
        {
            if (concurrencyLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));

            State = state ?? throw new ArgumentNullException(nameof(state));
            pool = new(concurrencyLevel, RemoveAndDrainWaitQueue);
        }

        /// <summary>
        /// Gets state of this trigger.
        /// </summary>
        public TState State { get; }

        private static void TransitionControl(ref (TState, ITransition) args, ref bool flag)
        {
            if (flag)
            {
                args.Item2.Transit(args.Item1);
            }
            else
            {
                flag = args.Item2.Test(args.Item1);
            }
        }

        private protected sealed override void DrainWaitQueue()
        {
            Debug.Assert(Monitor.IsEntered(this));

            for (WaitNode? current = first as WaitNode, next; current is not null; current = next)
            {
                next = current.Next as WaitNode;

                var transition = current.Transition;

                if (current.IsCompleted || transition is null)
                {
                    RemoveNode(current);
                    continue;
                }

                if (!transition.Test(State))
                    break;

                if (current.TrySetResult(true))
                {
                    RemoveNode(current);
                    transition.Transit(State);
                }
            }
        }

        /// <summary>
        /// Performs unconditional transition.
        /// </summary>
        /// <param name="transition">The transition action.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal(Action<TState> transition)
        {
            ThrowIfDisposed();
            transition(State);
            DrainWaitQueue();

            if (IsDisposeRequested && IsReadyToDispose)
                Dispose(true);
        }

        /// <summary>
        /// Performs unconditional transition.
        /// </summary>
        /// <typeparam name="T">The type of the argument to be passed to the transition.</typeparam>
        /// <param name="transition">The transition action.</param>
        /// <param name="arg">The argument to be passed to the transition.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        public void Signal<T>(Action<TState, T> transition, T arg)
        {
            ThrowIfDisposed();
            transition(State, arg);
            DrainWaitQueue();

            if (IsDisposeRequested && IsReadyToDispose)
                Dispose(true);
        }

        /// <summary>
        /// Performs conditional transition synchronously.
        /// </summary>
        /// <param name="transition">The condition to be examined immediately.</param>
        /// <returns>The result of <see cref="ITransition.Test(TState)"/> invocation.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe bool TrySignal(ITransition transition)
        {
            ThrowIfDisposed();

            var args = (State, transition);
            return TryAcquire(ref args, &TransitionControl);
        }

        /// <summary>
        /// Performs conditional transition asynchronously.
        /// </summary>
        /// <param name="transition">The conditional transition.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="Signal"/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask<bool> WaitAsync(ITransition transition, TimeSpan timeout, CancellationToken token = default)
        {
            var args = (State, transition);
            var result = WaitNoTimeoutAsync(ref args, &TransitionControl, pool, out var node, timeout, token);
            if (node is not null)
                node.Transition = transition;

            return result;
        }

        /// <summary>
        /// Suspends the caller and waits for the signal.
        /// </summary>
        /// <param name="transition">The conditional transition.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="Signal"/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask WaitAsync(ITransition transition, CancellationToken token = default)
        {
            var args = (State, transition);
            var result = WaitWithTimeoutAsync(ref args, &TransitionControl, pool, out var node, InfiniteTimeSpan, token);
            if (node is not null)
                node.Transition = transition;

            return result;
        }

        private protected sealed override bool IsReadyToDispose => first is null;
    }
}