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
        private readonly ISupplier<DefaultWaitNode> pool;

        /// <summary>
        /// Initializes a new trigger.
        /// </summary>
        public AsyncTrigger()
        {
            pool = new UnconstrainedValueTaskPool<DefaultWaitNode>();
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

            pool = new ConstrainedValueTaskPool<DefaultWaitNode>(concurrencyLevel);
        }

        /// <inheritdoc/>
        bool IAsyncEvent.Reset() => false;

        /// <summary>
        /// Resumes all suspended callers.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void PulseAll()
        {
            ThrowIfDisposed();

            ResumeSuspendedCallers();
        }

        /// <summary>
        /// Resumes the first suspended caller in the wait queue.
        /// </summary>
        /// <returns><see langword="true"/> if at least one suspended caller has been resumed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Pulse()
        {
            ThrowIfDisposed();

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

        /// <inheritdoc/>
        bool IAsyncEvent.IsSet => first is null;

        internal static bool AlwaysFalse(ref ValueTuple timeout) => false;

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
        /// <seealso cref="PulseAll"/>
        /// <seealso cref="Pulse"/>
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
        /// <seealso cref="PulseAll"/>
        /// <seealso cref="Pulse"/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask WaitAsync(CancellationToken token = default)
        {
            var tuple = ValueTuple.Create();
            return WaitWithTimeoutAsync(ref tuple, &AlwaysFalse, pool, out _, InfiniteTimeSpan, token);
        }

        /// <summary>
        /// Resumes the first suspended caller in the queue and suspends the immediate caller.
        /// </summary>
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
        public ValueTask<bool> PulseAndWaitAsync(bool throwOnEmptyQueue, TimeSpan timeout, CancellationToken token = default)
        {
            ThrowIfDisposed();
            return !Pulse() && throwOnEmptyQueue
                ? ValueTask.FromException<bool>(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue))
                : WaitAsync(timeout, token);
        }

        /// <summary>
        /// Resumes the first suspended caller in the queue and suspends the immediate caller.
        /// </summary>
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
        public ValueTask PulseAndWaitAsync(bool throwOnEmptyQueue, CancellationToken token = default)
        {
            ThrowIfDisposed();
            Pulse();
            return WaitAsync(token);
        }

        /// <summary>
        /// Resumes all suspended callers in the queue and suspens the immediate caller.
        /// </summary>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ValueTask<bool> PulseAllAndWaitAsync(TimeSpan timeout, CancellationToken token = default)
        {
            ThrowIfDisposed();
            PulseAll();
            return WaitAsync(timeout, token);
        }

        /// <summary>
        /// Resumes all suspended callers in the queue and suspens the immediate caller.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ValueTask PulseAllAndWaitAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            PulseAll();
            return WaitAsync(token);
        }
    }

    /// <summary>
    /// Represents asynchronous trigger that allows to resume and suspend
    /// concurrent flows.
    /// </summary>
    /// <typeparam name="TState">The external state used for coordination.</typeparam>
    public class AsyncTrigger<TState> : QueuedSynchronizer
        where TState : class
    {
        private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IPooledManualResetCompletionSource<WaitNode>
        {
            private readonly Action<WaitNode> backToPool;
            internal volatile IConditionalAction<TState>? Trigger;

            private WaitNode(Action<WaitNode> backToPool) => this.backToPool = backToPool;

            protected override void AfterConsumed()
            {
                Trigger = null;
                base.AfterConsumed();
                backToPool(this);
            }

            public static WaitNode CreateSource(Action<WaitNode> backToPool) => new(backToPool);
        }

        private readonly ISupplier<WaitNode> pool;

        /// <summary>
        /// Initializes a new trigger.
        /// </summary>
        public AsyncTrigger()
        {
            pool = new UnconstrainedValueTaskPool<WaitNode>();
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

            pool = new ConstrainedValueTaskPool<WaitNode>(concurrencyLevel);
        }

        private static bool EnsureState(ref (TState, IConditionalAction<TState>) args)
        {
            if (args.Item2.Test(args.Item1))
            {
                args.Item2.Execute(args.Item1);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resumes the first suspended caller in the wait queue.
        /// </summary>
        /// <returns><see langword="true"/> if at least one suspended caller has been resumed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Pulse(TState state)
        {
            ThrowIfDisposed();

            for (WaitNode? current = first as WaitNode, next; current is not null; current = next)
            {
                next = current.Next as WaitNode;

                if (current.IsCompleted)
                {
                    RemoveNode(current);
                    continue;
                }

                var trigger = current.Trigger;

                if ((trigger?.Test(state) ?? true) && current.TrySetResult(true))
                {
                    RemoveNode(current);
                    trigger?.Execute(state);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resumes all suspended callers.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void PulseAll(TState state)
        {
            for (WaitNode? current = first as WaitNode, next; current is not null; current = next)
            {
                next = current.Next as WaitNode;

                if (current.IsCompleted)
                {
                    RemoveNode(current);
                    continue;
                }

                var trigger = current.Trigger;

                if ((trigger?.Test(state) ?? true) && current.TrySetResult(true))
                {
                    RemoveNode(current);
                    trigger?.Execute(state);
                }
            }
        }

        /// <summary>
        /// Ensures that the object has expected state.
        /// </summary>
        /// <remarks>
        /// This is synchronous version of <see cref="WaitAsync(TState, IConditionalAction{TState}, TimeSpan, CancellationToken)"/>
        /// with fail-fast behavior.
        /// </remarks>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="condition">The condition to be examined immediately.</param>
        /// <returns>The result of <paramref name="condition"/> invocation.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool EnsureState(TState state, IConditionalAction<TState> condition)
        {
            ThrowIfDisposed();

            var args = (state, condition);
            return EnsureState(ref args);
        }

        /// <summary>
        /// Suspends the caller and waits for the signal.
        /// </summary>
        /// <remarks>
        /// This method always suspends the caller.
        /// </remarks>
        /// <param name="state">The shared state used for coordination.</param>
        /// <param name="condition">The object that describes the action that should be invoked if specific condition met.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="PulseAll"/>
        /// <seealso cref="Pulse"/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask<bool> WaitAsync(TState state, IConditionalAction<TState> condition, TimeSpan timeout, CancellationToken token = default)
        {
            var args = (state, condition);
            var result = WaitNoTimeoutAsync(ref args, &EnsureState, pool, out var node, timeout, token);
            if (node is not null)
                node.Trigger = condition;

            return result;
        }

        /// <summary>
        /// Suspends the caller and waits for the signal.
        /// </summary>
        /// <remarks>
        /// This method always suspends the caller.
        /// </remarks>
        /// <param name="state">The shared state used for coordination.</param>
        /// <param name="condition">The object that describes the action that should be invoked if specific condition met.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="PulseAll"/>
        /// <seealso cref="Pulse"/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public unsafe ValueTask WaitAsync(TState state, IConditionalAction<TState> condition, CancellationToken token = default)
        {
            var args = (state, condition);
            var result = WaitWithTimeoutAsync(ref args, &EnsureState, pool, out var node, InfiniteTimeSpan, token);
            if (node is not null)
                node.Trigger = condition;

            return result;
        }

        /// <summary>
        /// Resumes the first suspended caller in the queue and suspends the immediate caller.
        /// </summary>
        /// <param name="state">The shared state used for coordination.</param>
        /// <param name="condition">The object that describes the action that should be invoked if specific condition met.</param>
        /// <param name="mutator">The action that is invoked to modify the shared state.</param>
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
        public unsafe ValueTask<bool> PulseAndWaitAsync(TState state, IConditionalAction<TState> condition, Action<TState> mutator, bool throwOnEmptyQueue, TimeSpan timeout, CancellationToken token = default)
        {
            ThrowIfDisposed();
            mutator(state);
            return !Pulse(state) && throwOnEmptyQueue
                ? ValueTask.FromException<bool>(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue))
                : WaitAsync(state, condition, timeout, token);
        }

        /// <summary>
        /// Resumes the first suspended caller in the queue and suspends the immediate caller.
        /// </summary>
        /// <param name="state">The shared state used for coordination.</param>
        /// <param name="condition">The object that describes the action that should be invoked if specific condition met.</param>
        /// <param name="mutator">The action that is invoked to modify the shared state.</param>
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
        public unsafe ValueTask PulseAndWaitAsync(TState state, IConditionalAction<TState> condition, Action<TState> mutator, bool throwOnEmptyQueue, CancellationToken token = default)
        {
            ThrowIfDisposed();
            mutator(state);
            return !Pulse(state) && throwOnEmptyQueue
                ? ValueTask.FromException(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue))
                : WaitAsync(state, condition, token);
        }

        /// <summary>
        /// Resumes the first suspended caller in the queue and suspends the immediate caller.
        /// </summary>
        /// <typeparam name="TArgs">The type of the arguments to be passed to the state mutator.</typeparam>
        /// <param name="state">The shared state used for coordination.</param>
        /// <param name="condition">The object that describes the action that should be invoked if specific condition met.</param>
        /// <param name="mutator">The action that is invoked to modify the shared state.</param>
        /// <param name="args">The arguments to be passed to the state mutator.</param>
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
        public unsafe ValueTask<bool> PulseAndWaitAsync<TArgs>(TState state, IConditionalAction<TState> condition, Action<TState, TArgs> mutator, TArgs args, bool throwOnEmptyQueue, TimeSpan timeout, CancellationToken token = default)
        {
            ThrowIfDisposed();
            mutator(state, args);
            return !Pulse(state) && throwOnEmptyQueue
                ? ValueTask.FromException<bool>(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue))
                : WaitAsync(state, condition, timeout, token);
        }

        /// <summary>
        /// Resumes the first suspended caller in the queue and suspends the immediate caller.
        /// </summary>
        /// <typeparam name="TArgs">The type of the arguments to be passed to the state mutator.</typeparam>
        /// <param name="state">The shared state used for coordination.</param>
        /// <param name="condition">The object that describes the action that should be invoked if specific condition met.</param>
        /// <param name="mutator">The action that is invoked to modify the shared state.</param>
        /// <param name="args">The arguments to be passed to the state mutator.</param>
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
        public unsafe ValueTask PulseAndWaitAsync<TArgs>(TState state, IConditionalAction<TState> condition, Action<TState, TArgs> mutator, TArgs args, bool throwOnEmptyQueue, CancellationToken token = default)
        {
            ThrowIfDisposed();
            mutator(state, args);
            return !Pulse(state) && throwOnEmptyQueue
                ? ValueTask.FromException(new InvalidOperationException(ExceptionMessages.EmptyWaitQueue))
                : WaitAsync(state, condition, token);
        }
    }
}