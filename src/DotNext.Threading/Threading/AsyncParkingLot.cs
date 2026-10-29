using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DotNext.Threading.Tasks;

namespace DotNext.Threading;

/// <summary>
/// Represents a set of wait queues shared by many lightweight asynchronous locks.
/// </summary>
/// <remarks>
/// Each lock is represented by a field of type <see cref="State"/> owned by the caller,
/// so the lock itself doesn't require any allocation. The wait queue is allocated only for the contended locks.
/// The lock is identified by its position and optional user data. The positions mapped to the same slot share
/// the wait queue, but they don't block each other.
/// <para>
/// The ownership is handed off to the first waiter on release, so the waiters are resumed in FIFO order.
/// The lock is not reentrant and doesn't track the owner.
/// </para>
/// </remarks>
public partial class AsyncParkingLot : Disposable
{
    private readonly WaitQueue[] slots;
    private readonly int mask;

    /// <summary>
    /// Initializes a new parking lot.
    /// </summary>
    /// <param name="capacity">The number of slots. It is rounded up to the nearest power of two.</param>
    /// <param name="poolSizePerSlot">
    /// The maximum number of wait nodes retained by the pool of each slot;
    /// or <see langword="null"/> to retain all of them.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than or equal to zero;
    /// or <paramref name="poolSizePerSlot"/> is less than or equal to zero.
    /// </exception>
    public AsyncParkingLot(int capacity, long? poolSizePerSlot = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (poolSizePerSlot.HasValue)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(poolSizePerSlot.GetValueOrDefault(), nameof(poolSizePerSlot));
        
        var length = int.CreateChecked(BitOperations.RoundUpToPowerOf2((ulong)(uint)capacity));
        slots = new WaitQueue[length];
        mask = length - 1;

        foreach (ref var queue in slots.AsSpan())
        {
            queue = new(poolSizePerSlot);
        }
    }

    /// <summary>
    /// Gets the number of slots.
    /// </summary>
    public int Capacity => slots.Length;

    private ref WaitQueue this[int index]
        => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(slots), index & mask);

    /// <summary>
    /// Acquires the lock asynchronously, with a time-out.
    /// </summary>
    /// <remarks>
    /// The returned task must be awaited. Otherwise, the wait node of the timed out or canceled caller
    /// is never removed from the wait queue.
    /// </remarks>
    /// <param name="position">The position of the lock.</param>
    /// <param name="state">The state of the lock.</param>
    /// <param name="userData">
    /// The object that distinguishes the locks with the same position. It is compared by reference,
    /// so <see cref="Unpark"/> must be called with the same object.
    /// </param>
    /// <param name="timeout">The interval to wait for the lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the lock is acquired; <see langword="false"/> if timed out.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative; or <paramref name="timeout"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">This object is disposed.</exception>
    public ValueTask<bool> ParkAsync(int position, ref State state, object? userData, TimeSpan timeout, CancellationToken token = default)
        => position >= 0
            ? ParkAsync(position, ref state, userData, new Timeout(timeout), token)
            : ValueTask.FromException<bool>(new ArgumentOutOfRangeException(nameof(position)));
    
    private ValueTask<bool> ParkAsync(int position, ref State state, object? userData, Timeout timeout, CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return ValueTask.FromCanceled<bool>(token);

        // try to acquire the lock first, so zero timeout doesn't fail on the free lock
        while (!TryPark(ref state))
        {
            if (!timeout.TryGetRemainingTime(out var remainingTime))
                return ValueTask.FromResult(false);

            if (TryParkSlow(position, ref state, userData, out var disposed) is { } node)
                return node.CreateTask(remainingTime, token);

            if (disposed)
                return ValueTask.FromException<bool>(CreateException());

            if (token.IsCancellationRequested)
                return ValueTask.FromCanceled<bool>(token);
        }

        return ValueTask.FromResult(true);
    }

    /// <summary>
    /// Acquires the lock asynchronously.
    /// </summary>
    /// <remarks>
    /// The returned task must be awaited. Otherwise, the wait node of the canceled caller
    /// is never removed from the wait queue.
    /// </remarks>
    /// <param name="position">The position of the lock.</param>
    /// <param name="state">The state of the lock.</param>
    /// <param name="userData">
    /// The object that distinguishes the locks with the same position. It is compared by reference,
    /// so <see cref="Unpark"/> must be called with the same object.
    /// </param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing lock acquisition.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">This object is disposed.</exception>
    public ValueTask ParkAsync(int position, ref State state, object? userData = null, CancellationToken token = default)
    {
        if (position < 0)
            return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(position)));

        if (token.IsCancellationRequested)
            return ValueTask.FromCanceled(token);

        while (!TryPark(ref state))
        {
            if (TryParkSlow(position, ref state, userData, out var disposed) is { } node)
                return node.Invoke<ValueTask>(token);

            if (disposed)
                return ValueTask.FromException(CreateException());

            if (token.IsCancellationRequested)
                return ValueTask.FromCanceled(token);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Attempts to acquire the lock synchronously, without waiting.
    /// </summary>
    /// <param name="state">The state of the lock.</param>
    /// <returns><see langword="true"/> if the lock is acquired; <see langword="false"/> if the lock is already acquired.</returns>
    public static bool TryPark(ref State state)
        => Interlocked.CompareExchange(ref state, State.Locked, State.Free) is State.Free;

    private WaitNode? TryParkSlow(int position, ref State state, object? userData, out bool isDisposed)
    {
        WaitNode? node;
        ref var queue = ref this[position];
        lock (queue.Lock)
        {
            var stateCopy = state;

            // lock was freed, go back to the fast path
            if (!(isDisposed = IsDisposingOrDisposed)
                && stateCopy is not State.Free
                && Interlocked.CompareExchange(ref state, State.LockedWithWaiters, stateCopy) == stateCopy)
            {
                node = queue.RentFromPool();
                node.Initialize(this, position, userData);
                queue.Add(node, in measurementTags);
            }
            else
            {
                node = null;
            }
        }

        return node;
    }

    /// <summary>
    /// Releases the lock.
    /// </summary>
    /// <remarks>
    /// If the lock has waiters, the ownership is handed off to the first of them.
    /// The method must be called by the lock owner only, it's not checked.
    /// </remarks>
    /// <param name="position">The position of the lock.</param>
    /// <param name="state">The state of the lock.</param>
    /// <param name="userData">The object passed to <see cref="ParkAsync(int, ref State, object?, CancellationToken)"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">The lock is not acquired.</exception>
    public void Unpark(int position, ref State state, object? userData = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        switch (Interlocked.CompareExchange(ref state, State.Free, State.Locked))
        {
            case State.Free:
                throw new InvalidOperationException();
            case State.Locked:
                break;
            default:
                UnparkSlow(position, ref state, userData);
                break;
        }
    }

    private void UnparkSlow(int position, ref State state, object? userData)
    {
        ref var queue = ref this[position];
        WaitNode? node;
        bool resumable;
        lock (queue.Lock)
        {
            // the lock is handed off to the signaled waiter, so it remains Locked
            node = queue.TrySignalFirst(position, userData, in measurementTags, out var hasMore, out resumable);
            var newState = node is null ? State.Free : hasMore ? State.LockedWithWaiters : State.Locked;
            Volatile.Write(ref Unsafe.As<State, byte>(ref state), (byte)newState);
        }

        if (resumable)
            node?.NotifyConsumer();
    }

    private void ReturnNode(WaitNode node)
    {
        ref var queue = ref this[node.Position];

        // completed by the timeout or cancellation, the node is still in the queue and only the consumer can remove it
        if (node.NeedsRemoval)
        {
            lock (queue.Lock)
            {
                queue.Remove(node, in measurementTags);
            }
        }

        // outside the lock: Reset is a barrier against a late timeout or cancellation callback
        node.Reset();

        if (!IsDisposingOrDisposed)
        {
            lock (queue.Lock)
            {
                queue.ReturnToPool(node);
            }
        }
    }

    private void NotifyDisposed()
    {
        var detachedQueue = new LinkedValueTaskCompletionSource<bool>.LinkedList();
        var e = CreateException();
        ExceptionDispatchInfo.SetCurrentStackTrace(e);
        foreach (ref var slot in slots.AsSpan())
        {
            lock (slot.Lock)
            {
                slot.NotifyDisposed(e, ref detachedQueue, in measurementTags);
            }
        }
        
        detachedQueue.First?.Unwind();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            NotifyDisposed();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Represents the state of the lock.
    /// </summary>
    public enum State : byte
    {
        /// <summary>
        /// The lock is not acquired.
        /// </summary>
        Free = 0,

        /// <summary>
        /// The lock is acquired and has no waiters.
        /// </summary>
        Locked = 1,

        /// <summary>
        /// The lock is acquired and may have waiters.
        /// </summary>
        LockedWithWaiters = 2,
    }
}