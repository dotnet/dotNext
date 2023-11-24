namespace DotNext.Runtime.InteropServices;

/// <summary>
/// Provides access to the array allocated
/// in the unmanaged memory.
/// </summary>
/// <typeparam name="T">The type of the array elements.</typeparam>
public interface IUnmanagedArray<T> : IUnmanagedMemory, IEnumerable<T>, ICloneable, ISupplier<T[]>
    where T : unmanaged
{
    /// <summary>
    /// Gets the number of elements in the unmanaged memory.
    /// </summary>
    int Length { get; }

    /// <summary>
    /// Gets a pointer to the allocated unmanaged memory.
    /// </summary>
    new Pointer<T> Pointer { get; }

    /// <summary>
    /// Gets a span from the current instance.
    /// </summary>
    Span<T> Span { get; }

    /// <summary>
    /// Gets element of the unmanaged array.
    /// </summary>
    /// <param name="index">The index of the element to get.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    /// <value>The pointer to the array element.</value>
    ref T this[nint index]
    {
        get
        {
            if ((nuint)index >= (nuint)Length)
                throw new IndexOutOfRangeException();

            return ref Pointer[index];
        }
    }

    /// <summary>
    /// Copies elements from the unmanaged array into managed heap.
    /// </summary>
    /// <returns>The array allocated in managed heap containing copied elements from unmanaged memory.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    T[] ToArray() => Pointer.ToArray((uint)Length);

    /// <inheritdoc/>
    T[] ISupplier<T[]>.Invoke() => ToArray();
}