using System.ComponentModel;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Buffers;

/// <summary>
/// Represents common interface for growable buffer writers.
/// </summary>
/// <remarks>
/// This interface is intended to describe the shape of all buffer writer types
/// in .NEXT family of libraries. It is not recommended to have custom
/// implementation of this interface in your code.
/// </remarks>
/// <typeparam name="T">The type of the elements in the buffer.</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IGrowableBuffer<T> : IReadOnlySpanConsumer<T>, IDisposable, IResettable
{
    /// <summary>
    /// Represents default initial buffer size.
    /// </summary>
    private const int DefaultInitialBufferSize = 128;

    /// <summary>
    /// Gets the number of written elements.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    long WrittenCount { get; }

    /// <summary>
    /// Gets the maximum number of elements
    /// that can hold this buffer.
    /// </summary>
    /// <value>The maximum number of elements; or <see langword="null"/> if this buffer has no limits.</value>
    long? Capacity => null;

    /// <summary>
    /// Writes the memory block.
    /// </summary>
    /// <param name="input">The memory block to be written.</param>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    void Write(ReadOnlySpan<T> input);

    /// <inheritdoc />
    void IReadOnlySpanConsumer<T>.Invoke(ReadOnlySpan<T> input)
        => Write(input);

    /// <summary>
    /// Writes single element to this buffer.
    /// </summary>
    /// <param name="value">The value to be written.</param>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    void Write(T value) => Write(new ReadOnlySpan<T>(ref value));

    /// <summary>
    /// Passes the contents of this writer to the callback.
    /// </summary>
    /// <remarks>
    /// The callback may be called multiple times.
    /// </remarks>
    /// <param name="consumer">The callback used to accept the memory representing the contents of this builder.</param>
    /// <typeparam name="TConsumer">The type of the object that represents the consumer.</typeparam>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    void CopyTo<TConsumer>(TConsumer consumer)
        where TConsumer : IReadOnlySpanConsumer<T>;

    /// <summary>
    /// Passes the contents of this writer to the callback asynchronously.
    /// </summary>
    /// <remarks>
    /// The callback may be called multiple times.
    /// </remarks>
    /// <param name="consumer">The callback used to accept the memory representing the contents of this builder.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    ValueTask CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        where TConsumer : ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>;

    /// <summary>
    /// Copies the contents of this writer to the specified memory block.
    /// </summary>
    /// <param name="output">The memory block to be modified.</param>
    /// <returns>The actual number of copied elements.</returns>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    int CopyTo(Span<T> output);

    /// <summary>
    /// Clears the contents of the writer.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    void Clear();

    /// <inheritdoc />
    void IResettable.Reset() => Clear();

    /// <summary>
    /// Attempts to get written content as contiguous block of memory.
    /// </summary>
    /// <param name="block">The block representing written content.</param>
    /// <returns><see langword="true"/> if the written content can be represented as contiguous block of memory; otherwise, <see langword="false"/>.</returns>
    bool TryGetWrittenContent(out ReadOnlyMemory<T> block);

    internal static bool GetBufferSize(int sizeHint, int capacity, int writtenCount, out int newSize)
    {
        Debug.Assert(sizeHint >= 0);
        Debug.Assert(capacity >= 0);
        Debug.Assert(writtenCount >= 0);

        if (sizeHint is 0)
            sizeHint = 1;

        if (sizeHint > capacity - writtenCount)
        {
            var growBy = capacity is 0 ? DefaultInitialBufferSize : capacity;
            if ((sizeHint > growBy || (uint)(growBy += capacity) > (uint)Array.MaxLength) && (uint)(growBy = capacity + sizeHint) > (uint)Array.MaxLength)
                throw new InsufficientMemoryException();

            newSize = growBy;
            return true;
        }

        Unsafe.SkipInit(out newSize);
        return false;
    }
}