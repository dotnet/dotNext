using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Collections.Generic;
using Runtime;
using Tasks;
using Tasks.Pooling;

/// <summary>
/// Represents a collection of asynchronous events.
/// </summary>
[DebuggerDisplay($"Count = {{{nameof(Count)}}}")]
public partial class AsyncEventHub : QueuedSynchronizer, IResettable
{
    private static readonly int MaxCount = Unsafe.SizeOf<UInt128>() * 8;

    private readonly EventGroup all;
    private ValueTaskPool<bool, WaitNode, Action<WaitNode>> pool;
    private UInt128 state;

    /// <summary>
    /// Initializes a new collection of asynchronous events.
    /// </summary>
    /// <param name="count">The number of asynchronous events.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than or equal to zero.</exception>
    public AsyncEventHub(int count)
    {
        ArgumentOutOfRangeException.ThrowIfZero(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)MaxCount, nameof(count));

        Count = count;
        pool = new(OnCompleted, count);
        all = new(Count == MaxCount ? UInt128.MaxValue : GetBitMask(count) - UInt128.One);
    }

    private void OnCompleted(WaitNode node)
    {
        lock (SyncRoot)
        {
            if (node.NeedsRemoval)
                RemoveNode(node);

            pool.Return(node);
        }
    }

    /// <summary>
    /// Gets the number of events.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Waits for the event represented by the specified index.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <param name="timeout">The time to wait for an event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event.</returns>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitOneAsync(int eventIndex, TimeSpan timeout, CancellationToken token = default)
    {
        if ((uint)eventIndex > (uint)Count)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(eventIndex)));

        var manager = new WaitAllManager(this, eventIndex);
        return AcquireSpecialAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));
    }

    /// <summary>
    /// Waits for the event represented by the specified index.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitOneAsync(int eventIndex, CancellationToken token = default)
    {
        if ((uint)eventIndex > (uint)Count)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(eventIndex)));

        var manager = new WaitAllManager(this, eventIndex);
        return AcquireSpecialAsync(ref pool, ref manager, new CancellationTokenOnly(token));
    }

    /// <summary>
    /// Turns all events to non-signaled state.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);
        
        lock (SyncRoot)
        {
            state = default;
        }
    }

    private LinkedValueTaskCompletionSource<bool>? DrainWaitQueue()
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();

        for (WaitNode? current = Unsafe.As<WaitNode>(WaitQueueHead), next; current is not null; current = next)
        {
            next = Unsafe.As<WaitNode>(current.Next);

            if (current.Matches(state) && RemoveAndSignal(current, out var resumable) && resumable)
                detachedQueue.Add(current);
        }

        return detachedQueue.First;
    }

    /// <summary>
    /// Turns the specified event into the signaled state and reset all other events.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <returns><see langword="true"/> if the event turned into signaled state; <see langword="false"/> if the event is already in signaled state.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public bool ResetAndPulse(int eventIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)eventIndex, (uint)Count, nameof(eventIndex));
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        var newState = GetBitMask(eventIndex);
        bool result;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            result = (state & newState) == UInt128.Zero;
            state = newState;
            suspendedCallers = DrainWaitQueue();
        }

        suspendedCallers?.Unwind();
        return result;
    }

    /// <summary>
    /// Turns an event into the signaled state.
    /// </summary>
    /// <param name="eventIndex">The index of the event.</param>
    /// <returns><see langword="true"/> if the event turned into signaled state; <see langword="false"/> if the event is already in signaled state.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="eventIndex"/> is invalid.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public bool Pulse(int eventIndex)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)eventIndex, (uint)Count, nameof(eventIndex));
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        var mask = GetBitMask(eventIndex);
        bool result;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            result = (state & mask) == UInt128.Zero;
            state |= mask;
            suspendedCallers = DrainWaitQueue();
        }

        suspendedCallers?.Unwind();
        return result;
    }

    /// <summary>
    /// Turns the specified events into signaled state and reset all other events.
    /// </summary>
    /// <param name="events">A group of events to be signaled.</param>
    /// <returns>A group of events set by the method.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="events"/> contains an event index that is larger than or equal to <see cref="Count"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public EventGroup ResetAndPulse(in EventGroup events)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(events.Mask, all.Mask, nameof(events));
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);
        
        EventGroup result;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            result = new(events.Mask & ~state);
            state = events.Mask;
            suspendedCallers = DrainWaitQueue();
        }

        suspendedCallers?.Unwind();
        return result;
    }

    /// <summary>
    /// Turns the specified events into signaled state.
    /// </summary>
    /// <param name="events">A group of events to be signaled.</param>
    /// <returns>A group of events set by the method.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="events"/> contains an event index that is larger than or equal to <see cref="Count"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public EventGroup Pulse(in EventGroup events)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(events.Mask, all.Mask, nameof(events));
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        EventGroup result;
        LinkedValueTaskCompletionSource<bool>? suspendedCallers;
        lock (SyncRoot)
        {
            result = new(events.Mask & ~state);
            state |= events.Mask;
            suspendedCallers = DrainWaitQueue();
        }

        suspendedCallers?.Unwind();
        return result;
    }

    /// <summary>
    /// Turns all events into the signaled state.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public EventGroup PulseAll()
        => Pulse(all);

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="events">A group of events to be awaited.</param>
    /// <param name="timeout">The time to wait for an event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="events"/> contains an event index that is larger than or equal to <see cref="Count"/>.
    /// </exception>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAnyAsync(in EventGroup events, TimeSpan timeout, CancellationToken token = default)
    {
        if (events.Mask > all.Mask)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));
        
        var manager = new WaitAnyManager(this, events.Mask);
        return AcquireSpecialAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));
    }

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="events">A group of events to be awaited.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="events"/> contains an event index that is larger than or equal to <see cref="Count"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAnyAsync(in EventGroup events, CancellationToken token = default)
    {
        if (events.Mask > all.Mask)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));
        
        var manager = new WaitAnyManager(this, events.Mask);
        return AcquireSpecialAsync(ref pool, ref manager, new CancellationTokenOnly(token));
    }
    
    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="events">A group of events to be awaited.</param>
    /// <param name="output">A collection of signaled events set by the method when returned successfully.</param>
    /// <param name="timeout">The time to wait for an event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="events"/> contains an event index that is larger than or equal to <see cref="Count"/>.
    /// </exception>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAnyAsync(in EventGroup events, ICollection<int> output, TimeSpan timeout, CancellationToken token = default)
    {
        if (events.Mask > all.Mask)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));
        
        var manager = new WaitAnyManager(this, events.Mask) { Events = output };
        return AcquireSpecialAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));
    }

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="events">A group of events to be awaited.</param>
    /// <param name="output">A collection of signaled events set by the method when returned successfully.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing the event.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="events"/> contains an event index that is larger than or equal to <see cref="Count"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAnyAsync(in EventGroup events, ICollection<int> output, CancellationToken token = default)
    {
        if (events.Mask > all.Mask)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));
        
        var manager = new WaitAnyManager(this, events.Mask) { Events = output };
        return AcquireSpecialAsync(ref pool, ref manager, new CancellationTokenOnly(token));
    }

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="timeout">The time to wait for an event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAnyAsync(TimeSpan timeout, CancellationToken token = default)
        => WaitAnyAsync(all, timeout, token);

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAnyAsync(CancellationToken token = default)
        => WaitAnyAsync(all, token);
    
    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="output">A collection of signaled events set by the method when returned successfully.</param>
    /// <param name="timeout">The time to wait for an event.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAnyAsync(ICollection<int> output, TimeSpan timeout, CancellationToken token = default)
        => WaitAnyAsync(all, output, timeout, token);

    /// <summary>
    /// Waits for any of the specified events.
    /// </summary>
    /// <param name="output">A collection of signaled events set by the method when returned successfully.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the first signaled event.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAnyAsync(ICollection<int> output, CancellationToken token = default)
        => WaitAnyAsync(all, output, token);

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="events">A group of events to be awaited.</param>
    /// <param name="timeout">The time to wait for the events.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all the specified events.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="events"/> contains an event index that is larger than or equal to <see cref="Count"/>.
    /// </exception>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAllAsync(in EventGroup events, TimeSpan timeout, CancellationToken token = default)
    {
        if (events.Mask > all.Mask)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));
        
        var manager = new WaitAllManager(this, events.Mask);
        return AcquireSpecialAsync(ref pool, ref manager, new TimeoutAndCancellationToken(timeout, token));
    }

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="events">A group of events to be awaited.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all the specified events.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="events"/> contains an event index that is larger than or equal to <see cref="Count"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAllAsync(in EventGroup events, CancellationToken token = default)
    {
        if (events.Mask > all.Mask)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));
        
        var manager = new WaitAllManager(this, events.Mask);
        return AcquireSpecialAsync(ref pool, ref manager, new CancellationTokenOnly(token));
    }

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="timeout">The time to wait for the events.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all the specified events.</returns>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAllAsync(TimeSpan timeout, CancellationToken token = default)
        => WaitAllAsync(all, timeout, token);

    /// <summary>
    /// Waits for all events.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the completion of all the specified events.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public ValueTask WaitAllAsync(CancellationToken token = default)
        => WaitAllAsync(all, token);
    
    private static UInt128 GetBitMask(int index) => UInt128.One << index;
    
    private static void FillIndices(UInt128 events, ICollection<int> indices)
    {
        for (var enumerator = new EventGroup.Enumerator(events); enumerator.MoveNext();)
        {
            indices.Add(enumerator.Current);
        }
    }

    /// <summary>
    /// Represents a group of events.
    /// </summary>
    /// <remarks>
    /// It's better to cache a set of necessary event groups rather than create them on the fly
    /// due to performance reasons.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct EventGroup : IReadOnlyCollection<int>
    {
        internal readonly UInt128 Mask;

        internal EventGroup(UInt128 mask) => Mask = mask;

        /// <summary>
        /// Initializes a new group of events.
        /// </summary>
        /// <param name="indices">Indices of the events.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="indices"/> has at least one negative index.</exception>
        public EventGroup(ReadOnlySpan<int> indices)
        {
            foreach (var index in indices)
            {
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(indices));

                Mask |= GetBitMask(index);
            }
        }

        /// <summary>
        /// Gets a number of events in this group.
        /// </summary>
        public int Count => int.CreateTruncating(UInt128.PopCount(Mask));

        /// <summary>
        /// Checks whether the specified event is in this group.
        /// </summary>
        /// <param name="index">The index of the event.</param>
        /// <returns><see langword="true"/> if the event with index <paramref name="index"/> is in this group; otherwise, <see langword="false"/>.</returns>
        public bool Contains(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            
            return (Mask & GetBitMask(index)) != UInt128.Zero;
        }

        /// <summary>
        /// Gets an enumerator over indices in this group.
        /// </summary>
        /// <returns>An enumerator over indices.</returns>
        public Enumerator GetEnumerator() => new(Mask);

        /// <inheritdoc cref="GetEnumerator()"/>
        IEnumerator<int> IEnumerable<int>.GetEnumerator()
            => GetEnumerator().ToClassicEnumerator<Enumerator, int>();
        
        /// <inheritdoc cref="GetEnumerator()"/>
        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator().ToClassicEnumerator<Enumerator, int>();

        /// <summary>
        /// Represents an enumerator over indices.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator : IEnumerator<Enumerator, int>
        {
            private UInt128 state;

            internal Enumerator(in UInt128 state) => this.state = state;

            /// <summary>
            /// Gets the current index.
            /// </summary>
            public int Current
            {
                readonly get;
                private set;
            }

            /// <inheritdoc cref="IEnumerator.MoveNext()"/>
            public bool MoveNext()
            {
                if (state == UInt128.Zero)
                    return false;

                var index = Current = int.CreateTruncating(UInt128.TrailingZeroCount(state));
                state ^= GetBitMask(index);
                return true;
            }
        }
    }

    // TODO: Move to ref struct in .NET 10
    [StructLayout(LayoutKind.Auto)]
    private readonly struct WaitAllManager : ILockManager, IConsumer<WaitNode>
    {
        private readonly ReadOnlyValueReference<UInt128> state;
        private readonly UInt128 mask;

        internal WaitAllManager(AsyncEventHub stateHolder, int eventIndex)
        {
            Debug.Assert(stateHolder is not null);

            mask = GetBitMask(eventIndex);
            state = new(stateHolder, in stateHolder.state);
        }

        internal WaitAllManager(AsyncEventHub stateHolder, in UInt128 mask)
        {
            Debug.Assert(stateHolder is not null);

            this.mask = mask;
            state = new(stateHolder, in stateHolder.state);
        }

        void IConsumer<WaitNode>.Invoke(WaitNode node) => node.WaitAll(mask);

        bool ILockManager.IsLockAllowed => (state.Value & mask) == mask;

        void ILockManager.AcquireLock(bool synchronously)
        {
            // no need to reset events
        }

        static bool ILockManager.RequiresEmptyQueue => false;
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly struct WaitAnyManager : ILockManager, IConsumer<WaitNode>
    {
        private readonly ReadOnlyValueReference<UInt128> state;
        private readonly UInt128 mask;

        internal WaitAnyManager(AsyncEventHub stateHolder, in UInt128 mask)
        {
            Debug.Assert(stateHolder is not null);

            this.mask = mask;
            state = new(stateHolder, in stateHolder.state);
        }

        [DisallowNull]
        internal ICollection<int>? Events
        {
            get;
            init;
        }

        void IConsumer<WaitNode>.Invoke(WaitNode node) => node.WaitAny(mask, Events);

        bool ILockManager.IsLockAllowed => (state.Value & mask) != UInt128.Zero;

        void ILockManager.AcquireLock(bool synchronously)
        {
            if (Events is { } collection)
                FillIndices(state.Value & mask, collection);
        }

        static bool ILockManager.RequiresEmptyQueue => false;
    }

    private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IPooledManualResetCompletionSource<Action<WaitNode>>
    {
        private Action<WaitNode>? callback;
        private UInt128 mask;
        private bool waitAll;
        private ICollection<int>? events;

        internal void WaitAll(in UInt128 mask)
        {
            waitAll = true;
            this.mask = mask;
        }

        internal void WaitAny(in UInt128 mask, ICollection<int>? events)
        {
            waitAll = false;
            this.mask = mask;
            this.events = events;
        }

        protected override void AfterConsumed() => callback?.Invoke(this);

        protected override void CleanUp()
        {
            events = null;
            base.CleanUp();
        }

        internal bool Matches(UInt128 state)
        {
            var result = state & mask;

            if (waitAll)
            {
                if (result == mask)
                    return true;
            }
            else if (result != UInt128.Zero)
            {
                if (events is not null)
                    FillIndices(result, events);

                return true;
            }

            return false;
        }

        Action<WaitNode>? IPooledManualResetCompletionSource<Action<WaitNode>>.OnConsumed
        {
            get => callback;
            set => callback = value;
        }
    }
}