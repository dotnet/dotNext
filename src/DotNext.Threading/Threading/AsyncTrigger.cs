using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Threading
{
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
            private readonly object state;
            private readonly Predicate<TState> condition;

            internal ConditionalLockManager(object state, Predicate<TState> condition)
            {
                this.state = state;
                this.condition = condition;
            }

            bool ILockManager<ConditionalNode>.TryAcquire() => condition(Unsafe.Unbox<State>(state).Value);

            ConditionalNode ILockManager<ConditionalNode>.CreateNode(WaitNode? tail)
                => tail is null ? new ConditionalNode(condition) : new ConditionalNode(condition, tail);
        }

        private readonly object state;

        /// <summary>
        /// Initializes a new trigger.
        /// </summary>
        /// <param name="initial">The initial state of the trigger.</param>
        public AsyncTrigger(TState initial)
            => state = new State(initial);
        
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
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal(TState newState)
        {
            Unsafe.Unbox<State>(state).Value = newState;
            ResumePendingCallers(newState);
        }

        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <param name="transition">State transition function.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal(in ValueFunc<TState, TState> transition)
        {
            ref var stateHolder = ref Unsafe.Unbox<State>(state);
            stateHolder.Value = transition.Invoke(stateHolder.Value);
            ResumePendingCallers(stateHolder.Value);
        }
        
        /// <summary>
        /// Signals to all suspended callers about the new state.
        /// </summary>
        /// <typeparam name="TArgs">The type of the arguments of transition function.</typeparam>
        /// <param name="transition">State transition function.</param>
        /// <param name="args">The arguments to be passed to the function.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal<TArgs>(in ValueRefAction<TState, TArgs> transition, TArgs args)
        {
            ref var stateHolder = ref Unsafe.Unbox<State>(state);
            transition.Invoke(ref stateHolder.Value, args);
            ResumePendingCallers(stateHolder.Value);
        }

        /// <summary>
        /// Signals to all suspended callers without changing state.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal() => ResumePendingCallers(Unsafe.Unbox<State>(state).Value);

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
            get => Unsafe.Unbox<State>(state).Value;
        }

        /// <summary>
        /// Suspends the caller and waits for the signal.
        /// </summary>
        /// <remarks>
        /// This method always suspends the caller.
        /// </remarks>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
            => WaitAsync(ref Unsafe.Unbox<State>(state), timeout, token);

        /// <summary>
        /// Suspends the caller and waits for the event that meets to the specified condition.
        /// </summary>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="timeout">The time to wait for the signal.</param>
        /// <param name="token">The token</param>
        /// <returns><see langword="true"/> if event is triggered in timely manner; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> WaitAsync(Predicate<TState> condition, TimeSpan timeout, CancellationToken token = default)
        {
            var manager = new ConditionalLockManager(state, condition);
            return WaitAsync(ref manager, timeout, token);
        }

        /// <summary>
        /// Suspends the caller and waits for the event that meets to the specified condition.
        /// </summary>
        /// <param name="condition">The condition to wait for.</param>
        /// <param name="token">The token</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <exception cref="ObjectDisposedException">This trigger has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task WaitAsync(Predicate<TState> condition, CancellationToken token = default)
            => WaitAsync(condition, InfiniteTimeSpan, token);
    }
}