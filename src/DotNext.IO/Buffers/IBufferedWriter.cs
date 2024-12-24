using System.Buffers;

namespace DotNext.Buffers;

/// <summary>
/// Represents buffered writer.
/// </summary>
public interface IBufferedWriter : IBufferedChannel, IBufferWriter<byte>
{
    /// <summary>
    /// Marks the specified number of bytes in the buffer as produced.
    /// </summary>
    /// <param name="count">The number of produced bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is larger than the length of <see cref="Buffer"/>.</exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    void Produce(int count);
    
    /// <summary>
    /// The remaining part of the internal buffer available for write.
    /// </summary>
    /// <remarks>
    /// The size of returned buffer may be less than or equal to <see cref="IBufferedChannel.MaxBufferSize"/>.
    /// </remarks>
    Memory<byte> Buffer { get; }

    /// <summary>
    /// Flushes buffered data to the underlying storage.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask WriteAsync(CancellationToken token);

    /// <summary>
    /// Writes the data to the underlying storage through the buffer.
    /// </summary>
    /// <param name="input">The input data to write.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
    {
        for (int bytesWritten; !input.IsEmpty; input = input.Slice(bytesWritten))
        {
            input.Span.CopyTo(Buffer.Span, out bytesWritten);
            Produce(bytesWritten);
            await WriteAsync(token).ConfigureAwait(false);
        }
    }
    
    /// <inheritdoc />
    void IBufferWriter<byte>.Advance(int count) => Produce(count);

    /// <inheritdoc />
    Memory<byte> IBufferWriter<byte>.GetMemory(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        var result = Buffer;
        return sizeHint <= result.Length ? result : throw new InsufficientMemoryException();
    }

    /// <inheritdoc />
    Span<byte> IBufferWriter<byte>.GetSpan(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        var result = Buffer.Span;
        return sizeHint <= result.Length ? result : throw new InsufficientMemoryException();
    }
}