using System;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents simple memory writer backed by <see cref="Span{T}"/>
    /// </summary>
    public ref struct SpanWriter<T>
    {
        private readonly Span<T> span;
        private int position;

        /// <summary>
        /// Initializes a new memory writer.
        /// </summary>
        /// <param name="span">The span used to write elements.</param>
        public SpanWriter(Span<T> span)
        {
            this.span = span;
            position = 0;
        }

        /// <summary>
        /// Gets the available space in the underlying span.
        /// </summary>
        public readonly int FreeCapacity => span.Length - position;

        /// <summary>
        /// Gets the number of occupied elements in the underlying span.
        /// </summary>
        public readonly int WrittenCount => position;

        /// <summary>
        /// Gets the span over written elements.
        /// </summary>
        /// <value>The segment of underlying span containing written elements.</value>
        public readonly Span<T> WrittenSpan => span.Slice(0, position);

        /// <summary>
        /// Gets the underlying span.
        /// </summary>
        public readonly Span<T> Span => span;

        /// <summary>
        /// Copies the elements to the underlying span.
        /// </summary>
        /// <param name="input"></param>
        /// <returns>
        /// <see langword="true"/> if all elements are copied successfully;
        /// <see langword="false"/> if remaining space in the underlying span is not enough to place all elements from <paramref name="input"/>.
        /// </returns>
        public bool TryWrite(ReadOnlySpan<T> input)
        {
            var newLength = checked(position + input.Length);
            if (newLength > span.Length)
                return false;

            input.CopyTo(span.Slice(position));
            position = newLength;
            return true;
        }

        /// <summary>
        /// Copies the elements to the underlying span.
        /// </summary>
        /// <param name="input">The span of elements to be copied.</param>
        /// <exception cref="InvalidOperationException">Remaining space in the underlying span is not enough to place all elements from <paramref name="input"/>.</exception>
        public void Write(ReadOnlySpan<T> input)
        {
            if (!TryWrite(input))
                throw new InvalidOperationException(ExceptionMessages.NotEnoughMemory);
        }

        /// <summary>
        /// Puts single element into the underlying span.
        /// </summary>
        /// <param name="item">The item to place.</param>
        /// <returns>
        /// <see langword="true"/> if item has beem placed successfully;
        /// <see langword="false"/> if remaining space in the underlying span is not enough to place the item.
        /// </returns>
        public bool TryWrite(T item)
        {
            var newLength = checked(position + 1);
            if (newLength > span.Length)
                return false;

            span[position] = item;
            position = newLength;
            return true;
        }

        /// <summary>
        /// Puts single element into the underlying span.
        /// </summary>
        /// <param name="item">The item to place.</param>
        /// <exception cref="InvalidOperationException">Remaining space in the underlying span is not enough to place the item.</exception>
        public void Write(T item)
        {
            if (!TryWrite(item))
                throw new InvalidOperationException(ExceptionMessages.NotEnoughMemory);
        }

        /// <summary>
        /// Obtains the portion of underlying span and marks it as written.
        /// </summary>
        /// <param name="count">The size of the segment.</param>
        /// <param name="segment">The portion of the underlying span.</param>
        /// <returns>
        /// <see langword="true"/> if segment is obtained successfully;
        /// <see langword="false"/> if remaining space in the underlying span is not enough to place <paramref name="count"/> elements.
        /// </returns>
        public bool TrySlide(int count, out Span<T> segment)
        {
            var newLength = checked(position + count);
            if (newLength <= span.Length)
            {
                segment = span.Slice(position, count);
                position = newLength;
                return true;
            }

            segment = default;
            return false;
        }

        /// <summary>
        /// Obtains the portion of underlying span and marks it as written.
        /// </summary>
        /// <param name="count">The size of the segment.</param>
        /// <returns>The portion of the underlying span.</returns>
        /// <exception cref="InvalidOperationException">Remaining space in the underlying span is not enough to place <paramref name="count"/> elements.</exception>
        public Span<T> Slide(int count)
            => TrySlide(count, out var result) ? result : throw new InvalidOperationException(ExceptionMessages.NotEnoughMemory);
    }
}