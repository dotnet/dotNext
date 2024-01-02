using System.Buffers;
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
    private readonly unsafe delegate*<nuint, nuint, void* > allocator;
    private volatile Action? ownerDisposal;

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

        unsafe
        {
            allocator = &NativeMemory.Alloc;
        }
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
    public bool TrackAllocations
    {
        get => removeMemory is not null;
        init => removeMemory = value ? RemoveTracking : null;
    }

    /// <summary>
    /// Sets a value indicating that the allocated unmanaged memory must be filled with zeroes.
    /// </summary>
    public unsafe bool AllocateZeroedMemory
    {
        init => allocator = value ? &NativeMemory.AllocZeroed : &NativeMemory.Alloc;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void RemoveTracking(IUnmanagedMemory<T> owner)
        => ownerDisposal -= owner.Dispose;

    [MethodImpl(MethodImplOptions.Synchronized)]
    private void AddTracking(IUnmanagedMemory<T> owner)
        => ownerDisposal += owner.Dispose;

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

        PoolingUnmanagedMemoryOwner<T> result;

        unsafe
        {
            result = new PoolingUnmanagedMemoryOwner<T>(Math.Max(length, defaultBufferSize), allocator)
            {
                OnDisposed = removeMemory,
            };
        }

        if (removeMemory is not null)
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
            Interlocked.Exchange(ref ownerDisposal, null)?.Invoke();
        }
    }
}

file sealed class PoolingUnmanagedMemoryOwner<T> : UnmanagedMemoryOwner<T>, IUnmanagedMemory<T>
    where T : unmanaged
{
    required internal Action<IUnmanagedMemory<T>>? OnDisposed;

    internal unsafe PoolingUnmanagedMemoryOwner(int length, delegate*<nuint, nuint, void* > allocator)
        : base(length, allocator)
    {
    }

    /// <summary>
    /// Releases unmanaged memory that was allocated by this object.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release all resources; <see langword="false"/> to release unmanaged memory only.</param>
    protected override void Dispose(bool disposing)
    {
        try
        {
            OnDisposed?.Invoke(this);
            OnDisposed = null;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    void IUnmanagedMemory<T>.Reallocate(int length) => throw new NotSupportedException();

    bool IUnmanagedMemory<T>.SupportsReallocation => false;
}