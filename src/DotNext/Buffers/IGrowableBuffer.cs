using System;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents common interface for growable buffer writers.
    /// </summary>
    /// <remarks>
    /// This interface is intended to describe the shape of all buffer writer types
    /// in .NEXT family of libraries. It is not recommended to have custom
    /// implementation of this interface in your code.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
    public interface IGrowableBuffer<T> : IReadOnlySpanConsumer<T>, IDisposable
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
        void Write(T value) => Write(MemoryMarshal.CreateReadOnlySpan(ref value, 1));

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
            where TConsumer : notnull, IReadOnlySpanConsumer<T>;

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

        internal static int? GetBufferSize(int sizeHint, int capacity, int writtenCount)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            if (sizeHint == 0)
                sizeHint = 1;

            if (sizeHint > capacity - writtenCount)
            {
                var growBy = Math.Max(capacity, sizeHint);
                if (capacity == 0)
                    growBy = Math.Max(growBy, DefaultInitialBufferSize);
                var newSize = capacity + growBy;
                if ((uint)newSize > int.MaxValue)
                {
                    newSize = capacity + sizeHint;
                    if ((uint)newSize > int.MaxValue)
                        throw new InsufficientMemoryException();
                }

                return newSize;
            }

            return null;
        }
    }
}