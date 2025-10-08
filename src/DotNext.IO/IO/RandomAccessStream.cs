namespace DotNext.IO;

/// <summary>
/// Represents a stream over the storage that supports random access.
/// </summary>
public abstract partial class RandomAccessStream : ModernStream
{
    private long position;
    
    /// <inheritdoc/>
    public sealed override long Position
    {
        get => position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            position = value;
        }
    }
    
    private void Advance(int count) => position += count;

    /// <summary>
    /// Writes the bytes at the specified offset.
    /// </summary>
    /// <param name="buffer">The buffer to write.</param>
    /// <param name="offset">The offset within the underlying data storage.</param>
    protected abstract void Write(ReadOnlySpan<byte> buffer, long offset);
    
    /// <summary>
    /// Writes the bytes at the specified offset.
    /// </summary>
    /// <param name="buffer">The buffer to write.</param>
    /// <param name="offset">The offset within the underlying data storage.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous operation.</returns>
    protected abstract ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, long offset, CancellationToken token);

    /// <inheritdoc/>
    public sealed override void Write(ReadOnlySpan<byte> buffer)
    {
        Write(buffer, position);
        Advance(buffer.Length);
    }
    
    /// <inheritdoc/>
    public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        => SubmitWrite(WriteAsync(buffer, position, token), buffer.Length);

    /// <summary>
    /// Reads bytes to the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer to be modified.</param>
    /// <param name="offset">The offset within the underlying data storage.</param>
    /// <returns>The number of bytes read.</returns>
    protected abstract int Read(Span<byte> buffer, long offset);

    /// <summary>
    /// Reads bytes to the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer to be modified.</param>
    /// <param name="offset">The offset within the underlying data storage.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of bytes read.</returns>
    protected abstract ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token);

    /// <inheritdoc/>
    public sealed override int Read(Span<byte> buffer)
    {
        var bytesRead = Read(buffer, position);
        Advance(bytesRead);
        return bytesRead;
    }

    /// <inheritdoc/>
    public sealed override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        => SubmitRead(ReadAsync(buffer, position, token));
    
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        return position = newPosition >= 0L
            ? newPosition
            : throw new IOException();
    }

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            readCallback = writeCallback = null; // help GC
            readTask = default;
            writeTask = default;
            source = default;
        }

        base.Dispose(disposing);
    }
}