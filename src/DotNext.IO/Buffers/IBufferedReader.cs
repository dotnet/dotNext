namespace DotNext.Buffers;

/// <summary>
/// Represents buffered reader.
/// </summary>
public interface IBufferedReader : IBufferedChannel
{
    /// <summary>
    /// Gets unconsumed part of the buffer.
    /// </summary>
    ReadOnlyMemory<byte> Buffer { get; }

    /// <summary>
    /// Advances read position.
    /// </summary>
    /// <param name="count">The number of consumed bytes.</param>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is larger than the length of <see cref="Buffer"/>.</exception>
    void Consume(int count);

    /// <summary>
    /// Fetches the data from the underlying storage to the internal buffer.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the data has been copied from the underlying storage to the internal buffer;
    /// <see langword="false"/> if no more data to read.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="InternalBufferOverflowException">Internal buffer has no free space.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask<bool> ReadAsync(CancellationToken token);

    /// <summary>
    /// Reads the block of the memory.
    /// </summary>
    /// <param name="destination">The output buffer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of bytes copied to <paramref name="destination"/>.</returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken token)
    {
        var result = 0;
        for (int bytesRead; result < destination.Length; result += bytesRead, destination = destination.Slice(bytesRead))
        {
            Buffer.Span.CopyTo(destination.Span, out bytesRead);
            Consume(bytesRead);
            if (!await ReadAsync(token).ConfigureAwait(false))
                break;
        }

        return result;
    }
}