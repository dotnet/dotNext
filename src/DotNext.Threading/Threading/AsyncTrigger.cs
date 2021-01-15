using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using static Runtime.Intrinsics;
    using CallerMustBeSynchronizedAttribute = Runtime.CompilerServices.CallerMustBeSynchronizedAttribute;

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

        [CallerMustBeSynchronized]
        private void ResumePendingCallers()
        {
            // triggers only stateless nodes
            for (WaitNode? current = head, next; current is not null; current = next)
            {
                next = current.Next;
                if (IsExactTypeOf<WaitNode>(current))
                {
                    current.Complete();
                    RemoveNode(current);
                }
            }
        }

        [CallerMustBeSynchronized]
        private void ResumePendingCallers<TState>(TState state)
            where TState : class
        {
            for (WaitNode? current = head, next; current is not null; current = next)
            {
                next = current.Next;
                if (current is not ConditionalNode conditional || conditional.Invoke(state))
                {
                    current.Complete();
                    RemoveNode(current);
                }
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
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal<TState>(TState state)
            where TState : class
        {
            ThrowIfDisposed();
            ResumePendingCallers(state);
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
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal<TState, TArgs>(TState state, Action<TState, TArgs> mutator, TArgs args)
            where TState : class
        {
            ThrowIfDisposed();
            mutator(state, args);
            ResumePendingCallers(state);
        }

        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <typeparam name="TState">The type of the state maintained externally.</typeparam>
        /// <param name="state">The state to be modified.</param>
        /// <param name="mutator">State mutation.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal<TState>(TState state, Action<TState> mutator)
            where TState : class
        {
            ThrowIfDisposed();
            mutator.Invoke(state);
            ResumePendingCallers(state);
        }

        /// <inheritdoc/>
        bool IAsyncEvent.IsSet => head is null;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.Synchronized)]
        bool IAsyncEvent.Signal()
        {
            ThrowIfDisposed();
            var queueNotEmpty = head is not null;
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
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync<TState>(TState state, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default)
            where TState : class
        {
            ThrowIfDisposed();
            ResumePendingCallers(state);
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
        public Task SignalAndWaitAsync<TState>(TState state, Predicate<TState> condition, CancellationToken token = default)
            where TState : class
            => SignalAndWaitAsync(state, condition, InfiniteTimeSpan, token);

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
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync<TState, TArgs>(TState state, Action<TState, TArgs> mutator, TArgs args, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default)
            where TState : class
        {
            ThrowIfDisposed();
            mutator(state, args);
            ResumePendingCallers(state);
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
        public Task SignalAndWaitAsync<TState, TArgs>(TState state, Action<TState, TArgs> mutator, TArgs args, Predicate<TState> condition, CancellationToken token = default)
           where TState : class
           => SignalAndWaitAsync(state, mutator, args, condition, InfiniteTimeSpan, token);

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
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync<TState>(TState state, Action<TState> mutator, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default)
            where TState : class
        {
            ThrowIfDisposed();
            mutator(state);
            ResumePendingCallers(state);
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
        public Task SignalAndWaitAsync<TState>(TState state, Action<TState> mutator, Predicate<TState> condition, CancellationToken token = default)
            where TState : class
            => SignalAndWaitAsync(state, mutator, condition, InfiniteTimeSpan, token);
    }
}