using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Sequence = Collections.Generic.Sequence;

public partial class SparseBufferWriter<T> : IEnumerable<ReadOnlyMemory<T>>
{
    /// <summary>
    /// Represents enumerator over memory segments.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<ReadOnlyMemory<T>>
    {
        private MemoryChunk? current;
        private bool initialized;

        internal Enumerator(MemoryChunk? head)
        {
            current = head;
            initialized = false;
        }

        /// <inheritdoc />
        public readonly ReadOnlyMemory<T> Current
            => current is null ? ReadOnlyMemory<T>.Empty : current.WrittenMemory;

        /// <inheritdoc />
        readonly object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool MoveNext()
        {
            if (initialized)
                current = current?.Next;
            else
                initialized = true;

            return current is not null;
        }

        /// <inheritdoc />
        readonly void IEnumerator.Reset() => throw new NotSupportedException();

        /// <inheritdoc />
        void IDisposable.Dispose() => this = default;
    }

    /// <summary>
    /// Gets the current write position within the buffer.
    /// </summary>
    /// <remarks>
    /// Position within the buffer can be used later to retrieve the portion of data placed to the buffer.
    /// However, the call of <see cref="Clear"/> method invalidates any position object. The value of this
    /// property remains unchanged between invocations of read-only operations.
    /// </remarks>
    /// <seealso cref="Read(SequencePosition, long)"/>
    public SequencePosition End => last?.EndOfChunk ?? default;

    /// <summary>
    /// Gets the position of the first chunk of data within the buffer.
    /// </summary>
    public SequencePosition Start => new(first, 0);

    /// <summary>
    /// Returns position at an offset from the start of this buffer.
    /// </summary>
    /// <param name="offset">The offset from the start of this buffer.</param>
    /// <returns>The position within this buffer.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is less than zero or greater than <see cref="WrittenCount"/>.</exception>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public SequencePosition GetPosition(long offset)
    {
        ThrowIfDisposed();

        if (offset < 0L || offset > length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        SequencePosition result;

        if (offset is 0L)
        {
            result = Start;
        }
        else if (offset == length)
        {
            result = End;
        }
        else
        {
            result = default(SequencePosition);
            for (var current = first; current is not null && offset > 0L; current = current.Next)
            {
                var chunkLength = current.WrittenMemory.Length;
                if (offset <= current.WrittenMemory.Length)
                {
                    result = new(current, unchecked((int)offset));
                    break;
                }

                offset -= chunkLength;
            }
        }

        return result;
    }

    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
    private static ReadOnlyMemory<T> Read(ref SequencePosition position, ref long count)
    {
        if (position.GetObject() is not MemoryChunk currentChunk || count is 0L)
            return ReadOnlyMemory<T>.Empty;

        var offset = position.GetInteger();
        switch (currentChunk.WrittenMemory.Slice(offset))
        {
            case { IsEmpty: true } when currentChunk.Next is null: // end of buffer reached
                return ReadOnlyMemory<T>.Empty;
            case { IsEmpty: true }: // skip empty chunk
                position = currentChunk.StartOfNextChunk;
                return Read(ref position, ref count);
            case var block when block.Length < count:
                // next chunk may be null, in this case adjust position correctly to the end of this buffer
                position = currentChunk.Next is null
                    ? currentChunk.EndOfChunk
                    : currentChunk.StartOfNextChunk;
                count -= block.Length;
                return block;
            case var block:
                block = block.Slice(0, unchecked((int)count));
                count = 0L;
                position = new(currentChunk, offset + block.Length);
                return block;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NormalizePosition(scoped ref SequencePosition position)
    {
        if (position.GetObject() is not MemoryChunk)
            position = Start;
    }

    /// <summary>
    /// Reads the data from this buffer.
    /// </summary>
    /// <param name="start">The start position within this buffer.</param>
    /// <param name="count">The number of elements to read.</param>
    /// <returns>A collection of memory chunks containing the requested number of elements.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
    /// <exception cref="InvalidOperationException">The end of the buffer has reached but <paramref name="count"/> is larger than the number of available elements.</exception>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public IEnumerable<ReadOnlyMemory<T>> Read(SequencePosition start, long count)
    {
        ThrowIfDisposed();

        if (count < 0L)
            throw new ArgumentOutOfRangeException(nameof(count));

        NormalizePosition(ref start);
        return ReadCore(start, count);

        static IEnumerable<ReadOnlyMemory<T>> ReadCore(SequencePosition start, long count)
        {
            while (Read(ref start, ref count) is { IsEmpty: false } block)
                yield return block;

            if (count > 0L)
                throw new InvalidOperationException(ExceptionMessages.EndOfBuffer(count));
        }
    }

    /// <summary>
    /// Copies the elements from this buffer to the destination location, starting at the specified position,
    /// and advances the position.
    /// </summary>
    /// <param name="output">The destination block of memory.</param>
    /// <param name="position">The position within this buffer.</param>
    /// <returns>The number of copied elements.</returns>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public int CopyTo(scoped Span<T> output, scoped ref SequencePosition position)
    {
        ThrowIfDisposed();

        NormalizePosition(ref position);
        var result = 0;

        for (long count = output.Length; Read(ref position, ref count) is { IsEmpty: false } block; result += block.Length, output = output.Slice(block.Length))
        {
            block.Span.CopyTo(output);
        }

        return result;
    }

    /// <summary>
    /// Passes the elements from this buffer to the specified consumer, starting at the specified position.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="consumer">The consumer to be called multiple times to process the chunk of data.</param>
    /// <param name="start">The start position within this buffer.</param>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public void CopyTo<TConsumer>(TConsumer consumer, SequencePosition start)
        where TConsumer : notnull, IReadOnlySpanConsumer<T>
    {
        ThrowIfDisposed();

        NormalizePosition(ref start);
        for (long count = length; Read(ref start, ref count) is { IsEmpty: false } block;)
        {
            consumer.Invoke(block.Span);
        }
    }

    /// <summary>
    /// Passes the elements from this buffer to the specified consumer, starting at the specified position,
    /// and advances the position.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="consumer">The consumer to be called multiple times to process the chunk of data.</param>
    /// <param name="position">The position within this buffer.</param>
    /// <param name="count">The number of elements to read.</param>
    /// <returns>The actual number of copied elements.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public long CopyTo<TConsumer>(TConsumer consumer, scoped ref SequencePosition position, long count)
        where TConsumer : notnull, IReadOnlySpanConsumer<T>
    {
        ThrowIfDisposed();

        if (count < 0L)
            throw new ArgumentOutOfRangeException(nameof(count));

        NormalizePosition(ref position);

        long result;
        for (result = 0L; Read(ref position, ref count) is { IsEmpty: false } block; result += block.Length)
        {
            consumer.Invoke(block.Span);
        }

        return result;
    }

    /// <summary>
    /// Gets enumerator over memory segments.
    /// </summary>
    /// <returns>The enumerator over memory segments.</returns>
    /// <exception cref="ObjectDisposedException">The buffer has been disposed.</exception>
    public Enumerator GetEnumerator()
    {
        ThrowIfDisposed();
        return new Enumerator(first);
    }

    /// <inheritdoc />
    IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator()
        => first is null ? Sequence.GetEmptyEnumerator<ReadOnlyMemory<T>>() : GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}