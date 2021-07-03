using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
    using static Runtime.Intrinsics;

    /// <summary>
    /// Represents asynchronous trigger which allows to resume suspended
    /// callers based on registered conditions.
    /// </summary>
    public class AsyncTrigger : QueuedSynchronizer, IAsyncEvent
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct LockManager : ILockManager<WaitNode>
        {
            bool ILockManager<WaitNode>.TryAcquire() => false;

            WaitNode ILockManager<WaitNode>.CreateNode(WaitNode? tail)
                => tail is null ? new WaitNode() : new WaitNode(tail);
        }

        private abstract class ConditionalNode : WaitNode, ISupplier<object, bool>
        {
            private protected ConditionalNode()
            {
            }

            private protected ConditionalNode(WaitNode parent)
                : base(parent)
            {
            }

            public abstract bool Invoke(object state);
        }

        private sealed class WaitNode<TState> : ConditionalNode
            where TState : class
        {
            private readonly Predicate<TState> predicate;

            internal WaitNode(Predicate<TState> condition) => predicate = condition;

            internal WaitNode(Predicate<TState> condition, WaitNode tail)
                : base(tail) => predicate = condition;

            public override bool Invoke(object state)
                => state is TState typedState && predicate(typedState);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct ConditionalLockManager<TState> : ILockManager<WaitNode<TState>>
            where TState : class
        {
            private readonly Predicate<TState> condition;
            private readonly TState state;

            internal ConditionalLockManager(TState state, Predicate<TState> condition)
            {
                this.state = state;
                this.condition = condition;
            }

            bool ILockManager<WaitNode<TState>>.TryAcquire() => condition(state);

            WaitNode<TState> ILockManager<WaitNode<TState>>.CreateNode(WaitNode? tail)
                => tail is null ? new WaitNode<TState>(condition) : new WaitNode<TState>(condition, tail);
        }

        /// <inheritdoc/>
        bool IAsyncEvent.Reset() => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResumeAndRemove(WaitNode node)
        {
            Debug.Assert(Monitor.IsEntered(this));

            node.SetResult();
            RemoveNode(node);
        }

        private void ResumePendingCallers()
        {
            Debug.Assert(Monitor.IsEntered(this));

            // triggers only stateless nodes
            for (WaitNode? current = first, next; current is not null; current = next)
            {
                next = current.Next;
                if (IsExactTypeOf<WaitNode>(current))
                    ResumeAndRemove(current);
            }
        }

        private void ResumePendingCallers<TState>(TState state, bool fairness)
            where TState : class
        {
            Debug.Assert(Monitor.IsEntered(this));

            for (WaitNode? current = first, next; current is not null; current = next)
            {
                next = current.Next;
                if (current is ConditionalNode conditional && !conditional.Invoke(state))
                {
                    if (fairness)
                        break;
                    else
                        continue;
                }

                ResumeAndRemove(current);
            }
        }

        /// <summary>
        /// Signals to all suspended callers that do not rely on state.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal()
        {
            ThrowIfDisposed();
            ResumePendingCallers();
        }

        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <typeparam name="TState">The type of the state maintained externally.</typeparam>
        /// <param name="state">The new state of the trigger.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        public void Signal<TState>(TState state) // TODO: Remove in future versions and add fairness=false as default parameter value
            where TState : class
            => Signal(state, false);

        /// <summary>
        /// Signals to single or all suspended callers about the new state.
        /// </summary>
        /// <typeparam name="TState">The type of the state maintained externally.</typeparam>
        /// <param name="state">The new state of the trigger.</param>
        /// <param name="fairness"><see langword="true"/> to resume suspended callers in order as they were added to the wait queue; <see langword="false"/> to resume all suspended callers regardless of the ordering.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal<TState>(TState state, bool fairness)
            where TState : class
        {
            ThrowIfDisposed();
            ResumePendingCallers(state, fairness);
        }

        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <typeparam name="TState">The type of the state maintained externally.</typeparam>
        /// <typeparam name="TArgs">The type of the arguments of state mutator.</typeparam>
        /// <param name="state">The state to be modified.</param>
        /// <param name="mutator">State mutation.</param>
        /// <param name="args">The arguments to be passed to the mutator.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        public void Signal<TState, TArgs>(TState state, Action<TState, TArgs> mutator, TArgs args) // TODO: Remove in future versions and add fairness=false as default parameter value
            where TState : class
            => Signal(state, mutator, args, false);

        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <typeparam name="TState">The type of the state maintained externally.</typeparam>
        /// <typeparam name="TArgs">The type of the arguments of state mutator.</typeparam>
        /// <param name="state">The state to be modified.</param>
        /// <param name="mutator">State mutation.</param>
        /// <param name="args">The arguments to be passed to the mutator.</param>
        /// <param name="fairness"><see langword="true"/> to resume suspended callers in order as they were added to the wait queue; <see langword="false"/> to resume all suspended callers regardless of the ordering.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal<TState, TArgs>(TState state, Action<TState, TArgs> mutator, TArgs args, bool fairness)
            where TState : class
        {
            ThrowIfDisposed();
            mutator(state, args);
            ResumePendingCallers(state, fairness);
        }

        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <typeparam name="TState">The type of the state maintained externally.</typeparam>
        /// <param name="state">The state to be modified.</param>
        /// <param name="mutator">State mutation.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        public void Signal<TState>(TState state, Action<TState> mutator) // TODO: Remove in future versions and add fairness=false as default parameter value
            where TState : class
            => Signal(state, mutator, false);

        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <typeparam name="TState">The type of the state maintained externally.</typeparam>
        /// <param name="state">The state to be modified.</param>
        /// <param name="mutator">State mutation.</param>
        /// <param name="fairness"><see langword="true"/> to resume suspended callers in order as they were added to the wait queue; <see langword="false"/> to resume all suspended callers regardless of the ordering.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal<TState>(TState state, Action<TState> mutator, bool fairness)
            where TState : class
        {
            ThrowIfDisposed();
            mutator.Invoke(state);
            ResumePendingCallers(state, fairness);
        }

        /// <inheritdoc/>
        bool IAsyncEvent.IsSet => first is null;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        bool IAsyncEvent.Signal()
        {
            ThrowIfDisposed();
            var queueNotEmpty = first is not null;
            ResumePendingCallers();
            return queueNotEmpty;
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
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
        {
            var manager = new LockManager();
            return WaitAsync<WaitNode, LockManager>(ref manager, timeout, token);
        }

        /// <summary>
        /// Ensures that the object has expected state.
        /// </summary>
        /// <remarks>
        /// This is synchronous version of <see cref="WaitAsync{TState}(TState, Predicate{TState}, TimeSpan, CancellationToken)"/>
        /// with fail-fast behavior.
        /// </remarks>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="condition">The condition to be examined immediately.</param>
        /// <returns>The result of <paramref name="condition"/> invocation.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool EnsureState<TState>(TState state, Predicate<TState> condition)
            where TState : class
        {
            ThrowIfDisposed();
            return condition(state);
        }

        /// <summary>
        /// Suspends the caller and waits for the event that meets to the specified condition.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> WaitAsync<TState>(TState state, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default)
            where TState : class
        {
            var manager = new ConditionalLockManager<TState>(state, condition);
            return WaitAsync<WaitNode<TState>, ConditionalLockManager<TState>>(ref manager, timeout, token);
        }

        /// <summary>
        /// Suspends the caller and waits for the event that meets to the specified condition.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task WaitAsync<TState>(TState state, Predicate<TState> condition, CancellationToken token = default)
           where TState : class
           => WaitAsync(state, condition, InfiniteTimeSpan, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the signal.
        /// </summary>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync(TimeSpan timeout, CancellationToken token = default)
        {
            ThrowIfDisposed();
            ResumePendingCallers();
            return WaitAsync(timeout, token);
        }

        /// <summary>
        /// Signals to all suspended callers and waits for the signal.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync(CancellationToken token = default)
            => SignalAndWaitAsync(InfiniteTimeSpan, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> SignalAndWaitAsync<TState>(TState state, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default) // TODO: Remove in future versions and add fairness=false as default parameter value
            where TState : class
            => SignalAndWaitAsync(state, condition, timeout, false, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="fairness"><see langword="true"/> to resume suspended callers in order as they were added to the wait queue; <see langword="false"/> to resume all suspended callers regardless of the ordering.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync<TState>(TState state, Predicate<TState> condition, TimeSpan timeout, bool fairness, CancellationToken token = default)
            where TState : class
        {
            ThrowIfDisposed();
            ResumePendingCallers(state, fairness);
            var manager = new ConditionalLockManager<TState>(state, condition);
            return WaitAsync<WaitNode<TState>, ConditionalLockManager<TState>>(ref manager, timeout, token);
        }

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync<TState>(TState state, Predicate<TState> condition, CancellationToken token = default) // TODO: Remove in future versions and add fairness=false as default parameter value
            where TState : class
            => SignalAndWaitAsync(state, condition, false, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="fairness"><see langword="true"/> to resume suspended callers in order as they were added to the wait queue; <see langword="false"/> to resume all suspended callers regardless of the ordering.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync<TState>(TState state, Predicate<TState> condition, bool fairness, CancellationToken token = default)
            where TState : class
            => SignalAndWaitAsync(state, condition, InfiniteTimeSpan, fairness, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <typeparam name="TArgs">The type of the arguments of mutation action.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="mutator">State mutation action.</param>
        /// <param name="args">The arguments to be passed to the action.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> SignalAndWaitAsync<TState, TArgs>(TState state, Action<TState, TArgs> mutator, TArgs args, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default) // TODO: Remove in future versions and add fairness=false as default parameter value
            where TState : class
            => SignalAndWaitAsync(state, mutator, args, condition, timeout, false, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <typeparam name="TArgs">The type of the arguments of mutation action.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="mutator">State mutation action.</param>
        /// <param name="args">The arguments to be passed to the action.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="fairness"><see langword="true"/> to resume suspended callers in order as they were added to the wait queue; <see langword="false"/> to resume all suspended callers regardless of the ordering.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync<TState, TArgs>(TState state, Action<TState, TArgs> mutator, TArgs args, Predicate<TState> condition, TimeSpan timeout, bool fairness, CancellationToken token = default)
            where TState : class
        {
            ThrowIfDisposed();
            mutator(state, args);
            ResumePendingCallers(state, fairness);
            var manager = new ConditionalLockManager<TState>(state, condition);
            return WaitAsync<WaitNode<TState>, ConditionalLockManager<TState>>(ref manager, timeout, token);
        }

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <typeparam name="TArgs">The type of the arguments of mutation action.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="mutator">State mutation action.</param>
        /// <param name="args">The arguments to be passed to the action.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync<TState, TArgs>(TState state, Action<TState, TArgs> mutator, TArgs args, Predicate<TState> condition, CancellationToken token = default) // TODO: Remove in future versions and add fairness=false as default parameter value
           where TState : class
           => SignalAndWaitAsync(state, mutator, args, condition, false, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <typeparam name="TArgs">The type of the arguments of mutation action.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="mutator">State mutation action.</param>
        /// <param name="args">The arguments to be passed to the action.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="fairness"><see langword="true"/> to resume suspended callers in order as they were added to the wait queue; <see langword="false"/> to resume all suspended callers regardless of the ordering.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync<TState, TArgs>(TState state, Action<TState, TArgs> mutator, TArgs args, Predicate<TState> condition, bool fairness, CancellationToken token = default)
           where TState : class
           => SignalAndWaitAsync(state, mutator, args, condition, InfiniteTimeSpan, fairness, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="mutator">State mutation action.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> SignalAndWaitAsync<TState>(TState state, Action<TState> mutator, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default) // TODO: Remove in future versions and add fairness=false as default parameter value
            where TState : class
            => SignalAndWaitAsync(state, mutator, condition, timeout, false, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="mutator">State mutation action.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="fairness"><see langword="true"/> to resume suspended callers in order as they were added to the wait queue; <see langword="false"/> to resume all suspended callers regardless of the ordering.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync<TState>(TState state, Action<TState> mutator, Predicate<TState> condition, TimeSpan timeout, bool fairness, CancellationToken token = default)
            where TState : class
        {
            ThrowIfDisposed();
            mutator(state);
            ResumePendingCallers(state, fairness);
            var manager = new ConditionalLockManager<TState>(state, condition);
            return WaitAsync<WaitNode<TState>, ConditionalLockManager<TState>>(ref manager, timeout, token);
        }

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="mutator">State mutation action.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync<TState>(TState state, Action<TState> mutator, Predicate<TState> condition, CancellationToken token = default) // TODO: Remove in future versions and add fairness=false as default parameter value
            where TState : class
            => SignalAndWaitAsync(state, mutator, condition, false, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <typeparam name="TState">The type of the state to be inspected.</typeparam>
        /// <param name="state">The state to be inspected by predicate.</param>
        /// <param name="mutator">State mutation action.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="fairness"><see langword="true"/> to resume suspended callers in order as they were added to the wait queue; <see langword="false"/> to resume all suspended callers regardless of the ordering.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync<TState>(TState state, Action<TState> mutator, Predicate<TState> condition, bool fairness, CancellationToken token = default)
            where TState : class
            => SignalAndWaitAsync(state, mutator, condition, InfiniteTimeSpan, fairness, token);
    }
}