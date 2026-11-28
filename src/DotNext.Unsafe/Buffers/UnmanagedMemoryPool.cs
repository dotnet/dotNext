using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers;

using Runtime.InteropServices;

/// <summary>
/// Represents pool of unmanaged memory.
/// </summary>
/// <typeparam name="T">The type of the items in the memory pool.</typeparam>
public sealed class UnmanagedMemoryPool<T> : MemoryPool<T>
    where T : unmanaged
{
    private readonly Lock? syncRoot;
    private readonly int defaultBufferSize;
    private readonly bool zeroed;
    private PooledMemory? head; // the list of rented memory blocks, if the allocations are tracked

    /// <summary>
    /// Initializes a new pool of unmanaged memory.
    /// </summary>
    /// <param name="maxBufferSize">The maximum allowed number of elements that can be allocated by the pool.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBufferSize"/> is negative or zero.</exception>
    public UnmanagedMemoryPool(int maxBufferSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBufferSize);

        const int defaultBufferSize = 32;
        MaxBufferSize = maxBufferSize;
        this.defaultBufferSize = int.Min(defaultBufferSize, maxBufferSize);
    }

    /// <summary>
    /// Gets or sets the size of memory block allocated by default.
    /// </summary>
    public int DefaultBufferSize
    {
        get => defaultBufferSize;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            defaultBufferSize = int.Min(value, MaxBufferSize);
        }
    }

    /// <summary>
    /// Indicates that destruction of this pool releases the memory rented by this pool.
    /// </summary>
    /// <value><see langword="true"/> to release allocated unmanaged memory when <see cref="Dispose(bool)"/> is called; otherwise, <see langword="false"/>.</value>
    [MemberNotNullWhen(true, nameof(syncRoot))]
    public bool TrackAllocations
    {
        get => syncRoot is not null;
        init => syncRoot = value ? new() : null;
    }

    /// <summary>
    /// Sets a value indicating that the allocated unmanaged memory must be filled with zeroes.
    /// </summary>
    public bool AllocateZeroedMemory
    {
        init => zeroed = value;
    }

    private void AddTracking(PooledMemory owner)
    {
        Debug.Assert(TrackAllocations);

        lock (syncRoot)
        {
            if (head is not null)
            {
                owner.Append(head);
            }

            head = owner;
        }
    }

    private void RemoveTracking(PooledMemory owner)
    {
        Debug.Assert(TrackAllocations);

        lock (syncRoot)
        {
            RemoveTrackingCore(owner);
        }
    }

    private void RemoveTrackingCore(PooledMemory owner)
    {
        Debug.Assert(syncRoot is { IsHeldByCurrentThread: true });

        if (ReferenceEquals(head, owner))
            head = owner.Next;

        owner.Detach();
    }

    private PooledMemory? TryRemoveFirst()
    {
        Debug.Assert(TrackAllocations);

        PooledMemory? result;
        lock (syncRoot)
        {
            if ((result = head) is not null)
                RemoveTrackingCore(result);
        }

        return result;
    }

    /// <summary>
    /// Gets the maximum elements that can be allocated by this pool.
    /// </summary>
    public override int MaxBufferSize { get; }

    /// <summary>
    /// Returns unmanaged memory block capable of holding at least <paramref name="length"/> elements of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="length">The length of the continuous block of memory.</param>
    /// <returns>The allocated block of unmanaged memory.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is greater than <see cref="MaxBufferSize"/>.</exception>
    public override IMemoryOwner<T> Rent(int length = -1)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, MaxBufferSize);
        ArgumentOutOfRangeException.ThrowIfZero(length);

        if (length < 0)
            length = defaultBufferSize;

        return new PooledMemory(length, zeroed, TrackAllocations ? this : null);
    }

    /// <summary>
    /// Frees the unmanaged resources used by the memory pool and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && TrackAllocations)
        {
            // the owners can be disposed concurrently, so remove them one by one
            while (TryRemoveFirst() is { } owner)
                owner.As<IDisposable>().Dispose();
        }
    }

    private sealed unsafe class PooledMemory : UnmanagedMemoryOwner<T>, IUnmanagedMemory<T>
    {
        private UnmanagedMemoryPool<T>? pool;
        private bool disposed;
        private PooledMemory? previous;

        // protected by the lock of the pool
        public PooledMemory? Next;

        public PooledMemory(int length, bool zeroed, UnmanagedMemoryPool<T>? pool)
            : base(Allocate(length, zeroed), length)
        {
            this.pool = pool;
            pool?.AddTracking(this);
        }

        public void Append(PooledMemory other)
        {
            Next = other;
            other.previous = this;
        }
        
        public void Detach()
        {
            previous?.Next = Next;
            Next?.previous = previous;
            Next = previous = null;
        }

        private static T* Allocate(int length, bool zeroed) => zeroed
            ? INativeMemoryAllocator<T>.Allocate<ZeroedAllocator<T>>((uint)length)
            : INativeMemoryAllocator<T>.Allocate<DraftAllocator<T>>((uint)length);

        protected override void Dispose(bool disposing)
        {
            // the pool can release the memory concurrently with the owner
            if (Interlocked.Exchange(ref disposed, true))
                return;

            try
            {
                pool?.RemoveTracking(this);
            }
            finally
            {
                pool = null;
                base.Dispose(disposing);
            }
        }

        void IUnmanagedMemory<T>.Reallocate(int length) => throw new NotSupportedException();

        bool IUnmanagedMemory<T>.SupportsReallocation => false;
    }
}