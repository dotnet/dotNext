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
    [Obsolete("Use indexer overload with native-sized integer parameter")]
    ref T this[int index] => ref this[(nint)index];

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
    /// Copies elements from the memory location to the managed array.
    /// </summary>
    /// <param name="destination">The destination array.</param>
    /// <param name="offset">The position in the destination array from which copying begins.</param>
    /// <param name="count">The number of array elements to be copied.</param>
    /// <returns>The actual number of copied elements.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    [Obsolete("Use Span property instead")]
    long WriteTo(T[] destination, long offset, long count) => Pointer.WriteTo(destination, offset, count);

    /// <summary>
    /// Copies elements from the memory location to the managed array.
    /// </summary>
    /// <param name="destination">The destination array.</param>
    /// <returns>The actual number of copied elements.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    [Obsolete("Use Span property instead")]
    long WriteTo(T[] destination) => Pointer.WriteTo(destination, 0, Math.Min(destination.LongLength, Length));

    /// <summary>
    /// Copies elements from the unmanaged array into managed heap.
    /// </summary>
    /// <returns>The array allocated in managed heap containing copied elements from unmanaged memory.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    T[] ToArray() => Pointer.ToArray(Length);

    /// <inheritdoc/>
    T[] ISupplier<T[]>.Invoke() => ToArray();

    /// <summary>
    /// Copies elements from the specified array into
    /// the memory block identified by this object.
    /// </summary>
    /// <param name="source">The source array.</param>
    /// <param name="offset">The position in the source array from which copying begins.</param>
    /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
    /// <returns>Actual number of copied elements.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    [Obsolete("Use Span property instead")]
    long ReadFrom(T[] source, long offset, long count) => Pointer.ReadFrom(source, offset, Math.Min(Length, count));

    /// <summary>
    /// Copies elements from the memory location to the managed array.
    /// </summary>
    /// <param name="source">The source array.</param>
    /// <returns>The actual number of copied elements.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    [Obsolete("Use Span property instead")]
    long ReadFrom(T[] source)
    {
        source.AsSpan().CopyTo(Span, out var writtenCount);
        return writtenCount;
    }

    /// <summary>
    /// Copies elements from the current memory location to the specified memory location.
    /// </summary>
    /// <param name="destination">The target memory location.</param>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    [Obsolete("Use Span property instead")]
    void WriteTo(Pointer<T> destination) => Pointer.CopyTo(destination, Length);

    /// <summary>
    /// Copies bytes from the source memory to the memory identified by this object.
    /// </summary>
    /// <param name="source">The pointer to the source unmanaged memory.</param>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    [Obsolete("Use Span property instead")]
    void ReadFrom(Pointer<T> source) => source.CopyTo(Pointer, Length);

    /// <summary>
    /// Copies elements from the current memory location to the specified memory location.
    /// </summary>
    /// <param name="destination">The target memory location.</param>
    /// <returns>The actual number of copied elements.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    [Obsolete("Use Span property instead")]
    long WriteTo(IUnmanagedArray<T> destination)
    {
        var count = Math.Min(Length, destination.Length);
        Pointer.CopyTo(destination.Pointer, count);
        return count;
    }

    /// <summary>
    /// Computes bitwise equality between two blocks of memory.
    /// </summary>
    /// <param name="other">The block of memory to be compared.</param>
    /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    bool BitwiseEquals(Pointer<T> other) => Pointer.BitwiseEquals(other, Length);

    /// <summary>
    /// Computes bitwise equality between this array and the specified managed array.
    /// </summary>
    /// <param name="other">The array to be compared.</param>
    /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    [Obsolete("Use Span property instead")]
    bool BitwiseEquals(T[] other) => Span.SequenceEqual(other);

    /// <summary>
    /// Bitwise comparison of the memory blocks.
    /// </summary>
    /// <param name="other">The block of memory to be compared.</param>
    /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    int BitwiseCompare(Pointer<T> other) => Pointer.BitwiseCompare(other, Length);

    /// <summary>
    /// Bitwise comparison of the memory blocks.
    /// </summary>
    /// <param name="other">The array to be compared.</param>
    /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
    /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
    [Obsolete("Use Span property instead")]
    int BitwiseCompare(T[] other) => Span.BitwiseCompare(other);
}