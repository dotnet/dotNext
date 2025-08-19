using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Runtime.InteropServices;

/// <summary>
/// Represents pool of unmanaged memory.
/// </summary>
/// <typeparam name="T">The type of the items in the memory pool.</typeparam>
public sealed class UnmanagedMemoryPool<T> : MemoryPool<T>
    where T : unmanaged
{
    private readonly Action<IUnmanagedMemory<T>>? removeMemory;
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
        this.defaultBufferSize = Math.Min(defaultBufferSize, maxBufferSize);
        allocator = Rent<DraftAllocator<T>>;
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

            defaultBufferSize = Math.Min(value, MaxBufferSize);
        }
    }

    /// <summary>
    /// Indicates that destruction of this pool releases the memory rented by this pool.
    /// </summary>
    /// <value><see langword="true"/> to release allocated unmanaged memory when <see cref="Dispose(bool)"/> is called; otherwise, <see langword="false"/>.</value>
    [MemberNotNullWhen(true, nameof(removeMemory))]
    public bool TrackAllocations
    {
        get => removeMemory is not null;
        init => removeMemory = value ? RemoveTracking : null;
    }

    /// <summary>
    /// Sets a value indicating that the allocated unmanaged memory must be filled with zeroes.
    /// </summary>
    public bool AllocateZeroedMemory
    {
        init => allocator = value ? Rent<ZeroedAllocator<T>> : Rent<DraftAllocator<T>>;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void RemoveTracking(IUnmanagedMemory<T> owner)
        => ownerDisposal -= owner.Dispose;

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void AddTracking(IUnmanagedMemory<T> owner)
        => ownerDisposal += owner.Dispose;

    [MethodImpl(MethodImplOptions.Synchronized)]
    private Action? ClearTracker()
    {
        var result = ownerDisposal;
        ownerDisposal = null;
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

    private UnmanagedMemoryOwner<T, TAllocator> Rent<TAllocator>(int length)
        where TAllocator : struct, INativeMemoryAllocator<T>
        => new PoolingUnmanagedMemoryOwner<T, TAllocator>(length, removeMemory);

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

file sealed class PoolingUnmanagedMemoryOwner<T, TAllocator> : UnmanagedMemoryOwner<T, TAllocator>, IUnmanagedMemory<T>
    where T : unmanaged
    where TAllocator : struct, INativeMemoryAllocator<T>
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