using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Collections.Generic;

/// <summary>
/// Represents a collection of asynchronous events.
/// </summary>
[DebuggerDisplay($"Count = {{{nameof(Count)}}}")]
public partial class AsyncEventHub : QueuedSynchronizer, IResettable
{
    private static readonly int MaxInlinedSize = Unsafe.SizeOf<UInt128>() * 8;

    private readonly EventGroup all;
    private State state;

    /// <summary>
    /// Initializes a new collection of asynchronous events.
    /// </summary>
    /// <param name="count">The number of asynchronous events.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than or equal to zero.</exception>
    public AsyncEventHub(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        Count = count;
        var allState = new State(count, defaultValue: true);
        all = new(allState);
        state = new(count);
    }

    private new void DrainWaitQueue(ref WaitQueueScope queue)
    {
        for (; !queue.IsEndOfQueue<WaitNode, WaitNode>(out var node); queue.Advance())
        {
            if (node.Matches(in state))
                queue.SignalCurrent();
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
        if ((uint)eventIndex >= (uint)Count)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(eventIndex)));

        var mask = new State(Count) { [eventIndex] = true };
        var builder = BeginAcquisition(timeout, token);
        return WaitAllAsync<ValueTask, TimeoutAndCancellationToken>(ref builder, mask);
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
        if ((uint)eventIndex >= (uint)Count)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(eventIndex)));

        var mask = new State(Count) { [eventIndex] = true };
        var builder = BeginAcquisition(token);
        return WaitAllAsync<ValueTask, CancellationTokenOnly>(ref builder, mask);
    }

    /// <summary>
    /// Turns all events to non-signaled state.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The object is disposed.</exception>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        TryAcquire(new ResetTransition(ref state), out _).Dispose();
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
        
        bool result;
        var queue = CaptureWaitQueue();
        try
        {
            result = state.Pop(eventIndex);
            DrainWaitQueue(ref queue);
        }
        finally
        {
            queue.Dispose();
        }

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
        
        bool result;

        var queue = CaptureWaitQueue();
        try
        {
            result = state[eventIndex] is false;
            state[eventIndex] = true;
            DrainWaitQueue(ref queue);
        }
        finally
        {
            queue.Dispose();
        }

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
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)events.Mask.Capacity, (uint)Count, nameof(events));
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        var result = events.Mask.Clone();
        var queue = CaptureWaitQueue();
        try
        {
            result.AndNot(in state);
            events.Mask.CopyTo(ref state);
            DrainWaitQueue(ref queue);
        }
        finally
        {
            queue.Dispose();
        }

        return new(result);
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
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)events.Mask.Capacity, (uint)Count, nameof(events));
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        var result = events.Mask.Clone();
        var queue = CaptureWaitQueue();
        try
        {
            result.AndNot(in state);
            state |= events.Mask;
            DrainWaitQueue(ref queue);
        }
        finally
        {
            queue.Dispose();
        }

        return new(result);
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
        if ((uint)events.Mask.Capacity > (uint)Count)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));

        var builder = BeginAcquisition(timeout, token);
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
        if ((uint)events.Mask.Capacity > (uint)Count)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));

        var builder = BeginAcquisition(token);
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
        if ((uint)events.Mask.Capacity > (uint)Count)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));

        var builder = BeginAcquisition(timeout, token);
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
        if ((uint)events.Mask.Capacity > (uint)Count)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));

        var builder = BeginAcquisition(token);
        return WaitAnyAsync<ValueTask, CancellationTokenOnly>(ref builder, events.Mask, output);
    }
    
    private T WaitAnyAsync<T, TBuilder>(ref TBuilder builder, in State mask, ICollection<int>? output = null)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
    {
        var events = state.Clone();
        events &= mask;
        switch (builder.IsCompleted)
        {
            case true:
                goto default;
            case false when Acquire<T, TBuilder, WaitNode>(ref builder, !events.IsZeroed) is { } node:
                node.WaitAny(mask, output);
                goto default;
            case false when output is not null:
                output.AddAll(in events);
                goto default;
            default:
                return builder.Build();
        }
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
        if ((uint)events.Mask.Capacity > (uint)Count)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));

        var builder = BeginAcquisition(timeout, token);
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
        if ((uint)events.Mask.Capacity > (uint)Count)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(events)));

        var builder = BeginAcquisition(token);
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

    private T WaitAllAsync<T, TBuilder>(ref TBuilder builder, in State mask)
        where T : struct, IEquatable<T>
        where TBuilder : struct, ITaskBuilder<T>, allows ref struct
    {
        switch (builder.IsCompleted)
        {
            case true:
                goto default;
            case false when Acquire<T, TBuilder, WaitNode>(ref builder, state.CheckMask(in mask)) is { } node:
                node.WaitAll(in mask);
                goto default;
            default:
                return builder.Build();
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
        internal readonly State Mask;

        internal EventGroup(in State mask) => Mask = mask;

        /// <summary>
        /// Initializes a new group of events.
        /// </summary>
        /// <param name="indices">Indices of the events.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="indices"/> has at least one negative index.</exception>
        public EventGroup(ReadOnlySpan<int> indices)
        {
            var mask = new State();
            foreach (var index in indices)
            {
                mask.Add(index);
            }

            Mask = mask;
        }

        /// <summary>
        /// Gets a number of events in this group.
        /// </summary>
        public int Count => Mask.PopCount;

        /// <summary>
        /// Checks whether the specified event is in this group.
        /// </summary>
        /// <param name="index">The index of the event.</param>
        /// <returns><see langword="true"/> if the event with index <paramref name="index"/> is in this group; otherwise, <see langword="false"/>.</returns>
        public bool Contains(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);

            return Mask[index];
        }

        /// <summary>
        /// Gets an enumerator over indices in this group.
        /// </summary>
        /// <returns>An enumerator over indices.</returns>
        public Enumerator GetEnumerator() => new(Mask);
        
        /// <inheritdoc />
        IEnumerator<int> IEnumerable<int>.GetEnumerator() => IEnumerator<int>.Create(GetEnumerator());

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => IEnumerator<int>.Create(GetEnumerator());

        /// <summary>
        /// Represents an enumerator over indices.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator : IEnumerator<Enumerator, int>
        {
            private State.Enumerator enumerator;

            internal Enumerator(in State state, bool clone = true)
                => enumerator = clone ? state.Clone().GetEnumerator() : state.GetEnumerator();

            /// <summary>
            /// Gets the current index.
            /// </summary>
            public int Current => enumerator.Current;

            /// <inheritdoc cref="IEnumerator.MoveNext()"/>
            public bool MoveNext() => enumerator.MoveNext();
        }
    }

    private new sealed class WaitNode : QueuedSynchronizer.WaitNode, IWaitNodeFeature<WaitNode>
    {
        private State mask;
        private bool waitAll;
        private ICollection<int>? indices;

        internal void WaitAll(in State expectedMask)
        {
            waitAll = true;
            mask = expectedMask;
        }

        internal void WaitAny(in State expectedMask, ICollection<int>? output)
        {
            waitAll = false;
            mask = expectedMask;
            indices = output;
        }

        protected override void CleanUp()
        {
            indices = null;
            base.CleanUp();
        }

        internal bool Matches(in State state)
        {
            if (waitAll)
                return state.CheckMask(in mask);

            var result = state.Clone();
            result &= mask;

            if (result.IsZeroed)
                return false;

            indices?.AddAll(in result);
            return true;
        }

        WaitNode IWaitNodeFeature<WaitNode>.Feature => this;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct ResetTransition(ref State state) : ILockManager
    {
        private readonly ref State state = ref state;

        bool ILockManager.IsLockAllowed => true;

        void ILockManager.AcquireLock() => state.Reset();

        static bool ILockManager.RequiresEmptyQueue => false;
    }
}

file static class CollectionExtensions
{
    public static void AddAll(this ICollection<int> indices, in AsyncEventHub.State state)
    {
        foreach (var index in state)
        {
            indices.Add(index);
        }
    }
}