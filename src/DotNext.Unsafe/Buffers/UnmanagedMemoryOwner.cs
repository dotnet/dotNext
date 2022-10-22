using System.Collections;

namespace DotNext.Buffers;

using Runtime.InteropServices;

/// <summary>
/// Represents unmanaged memory access that allows
/// to obtain <see cref="Memory{T}"/> pointing to the
/// unmanaged memory.
/// </summary>
/// <typeparam name="T">The type of elements to store in memory.</typeparam>
internal sealed class UnmanagedMemoryOwner<T> : UnmanagedMemory<T>, IUnmanagedMemoryOwner<T>
    where T : unmanaged
{
    private readonly bool fromPool;
    internal Action<IUnmanagedMemoryOwner<T>>? OnDisposed;

    internal UnmanagedMemoryOwner(int length, bool zeroMem, bool fromPool)
        : base(length, zeroMem) => this.fromPool = fromPool;

    unsafe object ICloneable.Clone()
    {
        var copy = new UnmanagedMemoryOwner<T>(Length, zeroMem: false, fromPool);
        Buffer.MemoryCopy(Address.ToPointer(), copy.Address.ToPointer(), Size, Size);
        return copy;
    }

    Pointer<byte> IUnmanagedMemory.Pointer => new(Address);

    /// <summary>
    /// Gets a span of bytes from the current instance.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
    public unsafe Span<byte> Bytes => new(Address.ToPointer(), checked((int)Size));

    /// <summary>
    /// Gets a pointer to the allocated unmanaged memory.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
    public Pointer<T> Pointer => new(Address);

    Span<T> IUnmanagedArray<T>.Span => GetSpan();

    /// <summary>
    /// Represents unmanaged memory as stream.
    /// </summary>
    /// <returns>The stream of unmanaged memory.</returns>
    public unsafe Stream AsStream() => Pointer.AsStream(Size);

    /// <summary>
    /// Gets enumerator over all elements located in the unmanaged memory.
    /// </summary>
    /// <returns>The enumerator over all elements in the unmanaged memory.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
    public Pointer<T>.Enumerator GetEnumerator() => Pointer.GetEnumerator(Length);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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

    void IUnmanagedMemoryOwner<T>.Reallocate(int length)
    {
        if (fromPool)
            throw new NotSupportedException();
        Reallocate(length);
    }

    bool IUnmanagedMemoryOwner<T>.SupportsReallocation => fromPool is false;
}