using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace DotNext.Runtime.InteropServices;

/// <summary>
/// Represents common interface for the wrapper of the unmanaged memory.
/// </summary>
[CLSCompliant(false)]
[NativeMarshalling(typeof(UnmanagedMemoryMarshaller))]
public interface IUnmanagedMemory : IDisposable, ISupplier<Stream>
{
    /// <summary>
    /// Gets size of referenced unmanaged memory, in bytes.
    /// </summary>
    nuint Size { get; }

    /// <summary>
    /// Sets all bits of allocated memory to zero.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    void Clear() => Pointer.Clear(Size);

    /// <summary>
    /// Gets a pointer to the allocated unmanaged memory.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    Pointer<byte> Pointer { get; }

    /// <summary>
    /// Gets a span of bytes from the current instance.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    Span<byte> Bytes { get; }

    /// <summary>
    /// Represents unmanaged memory as stream.
    /// </summary>
    /// <returns>The stream of unmanaged memory.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    Stream AsStream() => Pointer.AsStream(long.CreateChecked(Size));

    /// <inheritdoc/>
    Stream ISupplier<Stream>.Invoke() => AsStream();
}

/// <summary>
/// Represents unmanaged memory owner.
/// </summary>
/// <typeparam name="T">The type of elements in the unmanaged memory.</typeparam>
[CLSCompliant(false)]
[NativeMarshalling(typeof(UnmanagedMemoryMarshaller<>))]
public interface IUnmanagedMemory<T> : IUnmanagedMemory, IMemoryOwner<T>, ISupplier<Memory<T>>
    where T : unmanaged
{
    /// <inheritdoc/>
    Memory<T> ISupplier<Memory<T>>.Invoke() => Memory;

    /// <inheritdoc/>
    unsafe nuint IUnmanagedMemory.Size => (nuint)Length * (nuint)sizeof(T);

    /// <summary>
    /// Gets the number of elements in the unmanaged memory.
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Gets a pointer to the allocated unmanaged memory.
    /// </summary>
    new Pointer<T> Pointer { get; }

    /// <inheritdoc/>
    Pointer<byte> IUnmanagedMemory.Pointer => new(Pointer);

    /// <summary>
    /// Gets a span from the current instance.
    /// </summary>
    Span<T> Span { get; }

    /// <inheritdoc/>
    Span<byte> IUnmanagedMemory.Bytes => MemoryMarshal.AsBytes(Span);

    /// <summary>
    /// Gets element of the unmanaged array.
    /// </summary>
    /// <param name="index">The index of the element to get.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    /// <value>The pointer to the array element.</value>
    ref T this[int index] => ref Span[index];

    /// <summary>
    /// Resizes a block of memory represented by this instance.
    /// </summary>
    /// <remarks>
    /// This method is dangerous because it invalidates all buffers returned by <see cref="System.Buffers.IMemoryOwner{T}.Memory"/> property.
    /// </remarks>
    /// <param name="length">The new number of elements in the unmanaged array.</param>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than or equal to zero.</exception>
    /// <exception cref="NotSupportedException">Reallocation is not supported.</exception>
    /// <seealso cref="SupportsReallocation"/>
    void Reallocate(int length) => throw new NotSupportedException();

    /// <summary>
    /// Gets a value indicating that the referenced memory can be reallocated.
    /// </summary>
    bool SupportsReallocation => false;
}