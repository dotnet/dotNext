using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
    using Runtime.CompilerServices;

    /// <summary>
    /// Represents asynchronous trigger which allows to resume suspended
    /// callers based on registered conditions.
    /// </summary>
    /// <typeparam name="TState">The type of trigger state.</typeparam>
    public class AsyncTrigger<TState> : QueuedSynchronizer, IAsyncEvent
    {
        private sealed class ConditionalNode : WaitNode
        {
            internal readonly Predicate<TState> Condition;

            internal ConditionalNode(Predicate<TState> condition) => Condition = condition;

            internal ConditionalNode(Predicate<TState> condition, WaitNode tail)
                : base(tail) => Condition = condition;
        }

        [StructLayout(LayoutKind.Auto)]
        private struct State : ILockManager<WaitNode>
        {
            internal TState Value;

            internal State(TState initial) => Value = initial;

            bool ILockManager<WaitNode>.TryAcquire() => false;

            WaitNode ILockManager<WaitNode>.CreateNode(WaitNode? tail)
                => tail is null ? new WaitNode() : new WaitNode(tail);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct ConditionalLockManager : ILockManager<ConditionalNode>
        {
            private readonly Box<State> state;
            private readonly Predicate<TState> condition;

            internal ConditionalLockManager(Box<State> state, Predicate<TState> condition)
            {
                this.state = state;
                this.condition = condition;
            }

            bool ILockManager<ConditionalNode>.TryAcquire() => condition(state.Value.Value);

            ConditionalNode ILockManager<ConditionalNode>.CreateNode(WaitNode? tail)
                => tail is null ? new ConditionalNode(condition) : new ConditionalNode(condition, tail);
        }

        private readonly Box<State> state;

        /// <summary>
        /// Initializes a new trigger.
        /// </summary>
        /// <param name="initial">The initial state of the trigger.</param>
        public AsyncTrigger(TState initial)
            => state = new Box<State>(new State(initial));
        
        bool IAsyncEvent.Reset() => false;

        private void ResumePendingCallers(TState state)
        {
            for(WaitNode? current = head, next; !(current is null); current = next)
            {
                next = current.Next;
                if(!(current is ConditionalNode conditional) || conditional.Condition(state))
                {
                    current.Complete();
                    RemoveNode(current);
                }
            }
        }

        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <param name="newState">The new state of the trigger.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal(TState newState)
        {
            ThrowIfDisposed();
            state.Value.Value = newState;
            ResumePendingCallers(newState);
        }

        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <param name="transition">State transition function.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal(in ValueFunc<TState, TState> transition)
        {
            ThrowIfDisposed();
            ref var currentState = ref state.Value.Value;
            currentState = transition.Invoke(currentState);
            ResumePendingCallers(currentState);
        }
        
        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <typeparam name="TArgs">The type of the arguments of transition function.</typeparam>
        /// <param name="transition">State transition function.</param>
        /// <param name="args">The arguments to be passed to the function.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal<TArgs>(in ValueRefAction<TState, TArgs> transition, TArgs args)
        {
            ThrowIfDisposed();
            ref var currentState = ref state.Value.Value;
            transition.Invoke(ref currentState, args);
            ResumePendingCallers(currentState);
        }

        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <typeparam name="TArgs">The type of the arguments of state mutator.</typeparam>
        /// <param name="mutator">State mutation.</param>
        /// <param name="args">The arguments to be passed to the mutator.</param>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal<TArgs>(in ValueAction<TState, TArgs> mutator, TArgs args)
        {
            ThrowIfDisposed();
            var currentState = state.Value.Value;
            mutator.Invoke(currentState, args);
            ResumePendingCallers(currentState);
        }

        /// <summary>
        /// Signals to all suspended callers without changing state.
        /// </summary>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal()
        {
            ThrowIfDisposed();
            ResumePendingCallers(state.Value.Value);
        }

        bool IAsyncEvent.Signal()
        {
            Signal();
            return true;
        }

        bool IAsyncEvent.IsSet => false;
        
        /// <summary>
        /// Gets the current state of this trigger.
        /// </summary>
        /// <value>The current state.</value>
        public TState CurrentState
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get => state.Value.Value;
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
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
            => WaitAsync<WaitNode, State>(ref state.Value, timeout, token);

        /// <summary>
        /// Suspends the caller and waits for the event that meets to the specified condition.
        /// </summary>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<bool> WaitAsync(Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default)
        {
            var manager = new ConditionalLockManager(state, condition);
            return WaitAsync<ConditionalNode, ConditionalLockManager>(ref manager, timeout, token);
        }

        /// <summary>
        /// Suspends the caller and waits for the event that meets to the specified condition.
        /// </summary>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task WaitAsync(Predicate<TState> condition, CancellationToken token = default)
            => WaitAsync(condition, InfiniteTimeSpan, token);
        
        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <param name="newState">The new state of the trigger.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync(TState newState, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default)
        {
            state.Value.Value = newState;
            ResumePendingCallers(newState);
            var manager = new ConditionalLockManager(state, condition);
            return WaitAsync<ConditionalNode, ConditionalLockManager>(ref manager, timeout, token);
        }

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <param name="newState">The new state of the trigger.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync(TState newState, Predicate<TState> condition, CancellationToken token = default)
            => SignalAndWaitAsync(newState, condition, InfiniteTimeSpan, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <param name="transition">State transition function.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync(in ValueFunc<TState, TState> transition, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default)
        {
            ref var currentState = ref state.Value.Value;
            currentState = transition.Invoke(currentState);
            ResumePendingCallers(currentState);
            var manager = new ConditionalLockManager(state, condition);
            return WaitAsync<ConditionalNode, ConditionalLockManager>(ref manager, timeout, token);
        }

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <param name="transition">State transition function.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync(in ValueFunc<TState, TState> transition, Predicate<TState> condition, CancellationToken token = default)
            => SignalAndWaitAsync(in transition, condition, InfiniteTimeSpan, token);
        
        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <param name="transition">State transition function.</param>
        /// <param name="args">The arguments to be passed to the function.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TArgs">The type of the arguments of transition function.</typeparam>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync<TArgs>(in ValueRefAction<TState, TArgs> transition, TArgs args, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default)
        {
            ref var currentState = ref state.Value.Value;
            transition.Invoke(ref currentState, args);
            ResumePendingCallers(currentState);
            var manager = new ConditionalLockManager(state, condition);
            return WaitAsync<ConditionalNode, ConditionalLockManager>(ref manager, timeout, token);
        }

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <param name="transition">State transition function.</param>
        /// <param name="args">The arguments to be passed to the function.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TArgs">The type of the arguments of transition function.</typeparam>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync<TArgs>(in ValueRefAction<TState, TArgs> transition, TArgs args, Predicate<TState> condition, CancellationToken token = default)
            => SignalAndWaitAsync(in transition, args, condition, InfiniteTimeSpan, token);

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <param name="transition">State transition action.</param>
        /// <param name="args">The arguments to be passed to the action.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TArgs">The type of the arguments of transition action.</typeparam>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWaitAsync<TArgs>(in ValueAction<TState, TArgs> transition, TArgs args, Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default)
        {
            var currentState = state.Value.Value;
            transition.Invoke(currentState, args);
            ResumePendingCallers(currentState);
            var manager = new ConditionalLockManager(state, condition);
            return WaitAsync<ConditionalNode, ConditionalLockManager>(ref manager, timeout, token);
        }

        /// <summary>
        /// Signals to all suspended callers and waits for the event that meets to the specified condition
        /// atomically.
        /// </summary>
        /// <param name="transition">State transition action.</param>
        /// <param name="args">The arguments to be passed to the action.</param>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TArgs">The type of the arguments of transition action.</typeparam>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SignalAndWaitAsync<TArgs>(in ValueAction<TState, TArgs> transition, TArgs args, Predicate<TState> condition, CancellationToken token = default)
            => SignalAndWaitAsync(in transition, args, condition, InfiniteTimeSpan, token);
    }
}