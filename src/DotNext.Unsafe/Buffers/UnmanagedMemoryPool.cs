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
    private readonly Func<int, IUnmanagedMemory<T>> allocator;
    private Action? ownerDisposal;

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
        allocator = Action<IUnmanagedMemory<T>>.NoOp.Rent<T, DraftAllocator<T>>;
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
        init
        {
            Action<IUnmanagedMemory<T>> removeMemory;
            if (value)
            {
                syncRoot = new();
                removeMemory = RemoveTracking;
            }
            else
            {
                syncRoot = null;
                removeMemory = Action<IUnmanagedMemory<T>>.NoOp;
            }

            allocator = allocator.Method.CreateDelegate<Func<int, IUnmanagedMemory<T>>>(removeMemory);
        }
    }

    /// <summary>
    /// Sets a value indicating that the allocated unmanaged memory must be filled with zeroes.
    /// </summary>
    public bool AllocateZeroedMemory
    {
        init
        {
            var removeMemory = allocator.Target as Action<IUnmanagedMemory<T>>;
            Debug.Assert(removeMemory is not null);
            
            allocator = value ? removeMemory.Rent<T, ZeroedAllocator<T>> : removeMemory.Rent<T, DraftAllocator<T>>;
        }
    }

    private void RemoveTracking(IUnmanagedMemory<T> owner)
    {
        Debug.Assert(TrackAllocations);

        lock (syncRoot)
        {
            ownerDisposal -= owner.Dispose;
        }
    }

    private void AddTracking(IUnmanagedMemory<T> owner)
    {
        Debug.Assert(TrackAllocations);

        lock (syncRoot)
        {
            ownerDisposal += owner.Dispose;
        }
    }

    private Action? ClearTracker()
    {
        Debug.Assert(TrackAllocations);

        Action? result;
        lock (syncRoot)
        {
            result = ownerDisposal;
            ownerDisposal = null;
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

        var result = allocator(length);

        if (TrackAllocations)
            AddTracking(result);

        return result;
    }

    /// <summary>
    /// Frees the unmanaged resources used by the memory pool and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (TrackAllocations && ClearTracker() is { } callback)
                callback();
        }
    }
}

file static class AllocatorHelpers
{
    public static UnmanagedMemoryOwner<T, TAllocator> Rent<T, TAllocator>(this Action<IUnmanagedMemory<T>> removeMemory, int length)
        where T : unmanaged
        where TAllocator : struct, INativeMemoryAllocator<T>, allows ref struct
        => new PoolingUnmanagedMemoryOwner<T, TAllocator>(length, removeMemory);
}

file sealed class PoolingUnmanagedMemoryOwner<T, TAllocator> : UnmanagedMemoryOwner<T, TAllocator>, IUnmanagedMemory<T>
    where T : unmanaged
    where TAllocator : struct, INativeMemoryAllocator<T>, allows ref struct
{
    private Action<IUnmanagedMemory<T>>? onDisposedCallback;

    internal PoolingUnmanagedMemoryOwner(int length, Action<IUnmanagedMemory<T>>? onDisposed)
        : base(length)
        => onDisposedCallback = onDisposed;

    /// <summary>
    /// Releases unmanaged memory that was allocated by this object.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release all resources; <see langword="false"/> to release unmanaged memory only.</param>
    protected override void Dispose(bool disposing)
    {
        try
        {
            if (onDisposedCallback is { } callback)
            {
                callback(this);
                onDisposedCallback = null;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    void IUnmanagedMemory<T>.Reallocate(int length) => throw new NotSupportedException();

    bool IUnmanagedMemory<T>.SupportsReallocation => false;
}