using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Collections.Generic;
using Tasks;

/// <summary>
/// Represents a collection of asynchronous events.
/// </summary>
[DebuggerDisplay($"Count = {{{nameof(Count)}}}")]
public partial class AsyncEventHub : QueuedSynchronizer, IResettable
{
    private static readonly int MaxCount = Unsafe.SizeOf<UInt128>() * 8;

    private readonly EventGroup all;
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
        all = new(Count == MaxCount ? UInt128.MaxValue : GetBitMask(count) - UInt128.One);
    }
    
    private static UInt128 GetBitMask(int index) => UInt128.One << index;

    private protected sealed override void DrainWaitQueue(ref WaitQueueVisitor waitQueueVisitor)
    {
        for (; !waitQueueVisitor.IsEndOfQueue<WaitNode, WaitNode>(out var node); waitQueueVisitor.Advance())
        {
            if (node.Matches(state))
                waitQueueVisitor.SignalCurrent();
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

        var builder = CreateTaskBuilder(timeout, token);
        return WaitAllAsync<ValueTask, TimeoutAndCancellationToken>(ref builder, GetBitMask(eventIndex));
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

        var builder = CreateTaskBuilder(token);
        return WaitAllAsync<ValueTask, CancellationTokenOnly>(ref builder, GetBitMask(eventIndex));
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

        var builder = CreateTaskBuilder(timeout, token);
        return WaitAnyAsync<ValueTask, TimeoutAndCancellationToken>(ref builder, events.Mask);
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

        var builder = CreateTaskBuilder(token);
        return WaitAnyAsync<ValueTask, CancellationTokenOnly>(ref builder, events.Mask);
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

        var builder = CreateTaskBuilder(timeout, token);
        return WaitAnyAsync<ValueTask, TimeoutAndCancellationToken>(ref builder, events.Mask, output);
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

        var builder = CreateTaskBuilder(token);
        return WaitAnyAsync<ValueTask, CancellationTokenOnly>(ref builder, events.Mask, output);
    }
    
    private T WaitAnyAsync<T, TBuilder>(ref TBuilder builder, UInt128 mask, ICollection<int>? output = null)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>
    {
        var events = state & mask;
        switch (builder.IsCompleted)
        {
            case true:
                goto default;
            case false when Acquire<T, TBuilder, WaitNode>(ref builder, events != UInt128.Zero) is { } node:
                node.WaitAny(mask, output);
                goto default;
            case false when output is not null:
                FillIndices(events, output);
                goto default;
            default:
                builder.Dispose();
                break;
        }

        return builder.Invoke();
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

        var builder = CreateTaskBuilder(timeout, token);
        return WaitAllAsync<ValueTask, TimeoutAndCancellationToken>(ref builder, events.Mask);
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

        var builder = CreateTaskBuilder(token);
        return WaitAllAsync<ValueTask, CancellationTokenOnly>(ref builder, events.Mask);
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

    private T WaitAllAsync<T, TBuilder>(ref TBuilder builder, UInt128 mask)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>
    {
        var events = state & mask;
        switch (builder.IsCompleted)
        {
            case true:
                goto default;
            case false when Acquire<T, TBuilder, WaitNode>(ref builder, events == mask) is { } node:
                node.WaitAll(mask);
                goto default;
            default:
                builder.Dispose();
                break;
        }

        return builder.Invoke();
    }

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

    private new sealed class WaitNode : QueuedSynchronizer.WaitNode, INodeMapper<WaitNode, WaitNode>
    {
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

        static WaitNode INodeMapper<WaitNode, WaitNode>.GetValue(WaitNode node)
            => node;
    }
}