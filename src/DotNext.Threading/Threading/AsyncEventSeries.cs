using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    /// <summary>
    /// Allows to synchronize with over series of the events.
    /// </summary>
    /// <typeparam name="T">The type of the value describing instant event in the timeline.</typeparam>
    public class AsyncEventSeries<T> : QueuedSynchronizer
        where T : notnull
    {
        private sealed class Expectation : WaitNode
        {
            internal readonly T Value;

            internal Expectation(T expected) => this.Value = expected;

            internal Expectation(WaitNode tail, T expected) : base(tail) => this.Value = expected;
        }

        [StructLayout(LayoutKind.Auto)]
        private struct State : ILockManager<WaitNode>
        {
            private readonly IComparer<T> comparer;
            internal T Value;

            internal State(T start, IComparer<T> comparer)
            {
                this.comparer = comparer;
                Value = start;
            }

            internal readonly bool IsInPast(T value)
                => comparer.Compare(Value, value) >= 0;
            
            internal readonly bool IsInFuture(T value)
                => comparer.Compare(Value, value) < 0;

            readonly bool ILockManager<WaitNode>.TryAcquire() => false;

            readonly WaitNode ILockManager<WaitNode>.CreateNode(WaitNode? tail)
                => tail is null ? new WaitNode() : new WaitNode(tail);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct LockManager : ILockManager<Expectation>
        {
            private readonly object state;
            private readonly T expected;

            internal LockManager(object state, T expected)
            {
                this.state = state;
                this.expected = expected;
            }

            bool ILockManager<Expectation>.TryAcquire() => Unsafe.Unbox<State>(state).IsInPast(expected);

            Expectation ILockManager<Expectation>.CreateNode(WaitNode? tail)
                => tail is null ? new Expectation(expected) : new Expectation(tail, expected);
        }

        private readonly object state;

        /// <summary>
        /// Initializes a new series of events.
        /// </summary>
        /// <param name="start">The initial event in the timeline.</param>
        /// <param name="comparer">The comparer used to compare events in the timeline.</param>
        public AsyncEventSeries(T start, IComparer<T> comparer)
            => state = new State(start, comparer);

        /// <summary>
        /// Initializes a new series of events.
        /// </summary>
        /// <param name="start">The initial event in the timeline.</param>
        /// <param name="comparer">The comparer used to compare events in the timeline.</param>
        public AsyncEventSeries(T start, Comparison<T> comparer)
            : this(start, Comparer<T>.Create(comparer))
        {
        }

        /// <summary>
        /// Gets the value representing the current event.
        /// </summary>
        /// <value>The value representing the current event.</value>
        public T Instant
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get => Unsafe.Unbox<State>(state).Value;
        }

        private void ReleasePendingNodes(ref State state)
        {
            for (WaitNode? current = head, next; !(current is null); current = next)
            {
                next = current.Next;
                if(current is Expectation pointNode && state.IsInFuture(pointNode.Value))
                    continue;
                RemoveNode(current);
                current.Complete();
            }
        }

        /// <summary>
        /// Advances to the next event in the timeline.
        /// </summary>
        /// <typeparam name="TArgs">The type of the arguments to be passed to the updater.</typeparam>
        /// <param name="updater">The monotonically increasing function that advances the event.</param>
        /// <param name="args">The arguments to be passed to the updater.</param>
        /// <returns>A value represents the next event in the timeline.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public T Advance<TArgs>(in ValueRefAction<T, TArgs> updater, TArgs args)
        {
            ThrowIfDisposed();
            ref var state = ref Unsafe.Unbox<State>(this.state);
            updater.Invoke(ref state.Value, args);
            //release all wait nodes
            ReleasePendingNodes(ref state);
            return state.Value;
        }

        /// <summary>
        /// Advances to the next event in the timeline.
        /// </summary>
        /// <param name="updater">The monotonically increasing function that advances the event.</param>
        /// <returns>A value represents the next event in the timeline; or <see cref="Optional{T}.Empty"/> if <paramref name="updater"/> is not monotonically increasing function.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Optional<T> TryAdvance(in ValueFunc<T, T> updater)
        {
            ThrowIfDisposed();
            ref var state = ref Unsafe.Unbox<State>(this.state);
            var point = updater.Invoke(state.Value);
            if(state.IsInPast(point))
                return Optional<T>.Empty;
            state.Value = point;
            //release all wait nodes
            ReleasePendingNodes(ref state);
            return point;
        }

        /// <summary>
        /// Advances to the next event in the timeline.
        /// </summary>
        /// <param name="value">The value representing the next event in the timeline.</param>
        /// <returns><see langword="true"/> if the timeline is advanced successfully; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryAdvance(T value)
        {
            ThrowIfDisposed();
            ref var state = ref Unsafe.Unbox<State>(this.state);
            if(state.IsInPast(value))
                return false;
            state.Value = value;
            ReleasePendingNodes(ref state);
            return true;
        }

        /// <summary>
        /// Waits for the next event in the timeline.
        /// </summary>
        /// <remarks>
        /// Use this method with care because it always suspens the caller.
        /// </remarks>
        /// <param name="timeout">The time to wait for the next event in the timeline.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event occurred; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token)
            => WaitAsync(ref Unsafe.Unbox<State>(state), timeout, token);
        
        /// <summary>
        /// Waits for the specific event in the timeline.
        /// </summary>
        /// <param name="value">The expected event.</param>
        /// <param name="timeout">The time to wait for the next event in the timeline.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if event occurred; <see langword="false"/> if timeout occurred.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> WaitAsync(T value, TimeSpan timeout, CancellationToken token)
        {
            var manager = new LockManager(state, value);
            return WaitAsync(ref manager, timeout, token);
        }
    }
}