namespace DotNext.IO;

using static Buffers.Memory;

/// <summary>
/// Represents read-only view over the portion of underlying stream.
/// </summary>
/// <remarks>
/// The segmentation is supported only for seekable streams.
/// </remarks>
/// <param name="stream">The underlying stream represented by the segment.</param>
/// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open after the object is disposed; otherwise, <see langword="false"/>.</param>
public sealed class StreamSegment(Stream stream, bool leaveOpen = true) : Stream, IFlushable
{
    private long length = stream.Length, offset;

    /// <summary>
    /// Gets underlying stream.
    /// </summary>
    public Stream BaseStream => stream;

    /// <summary>
    /// Establishes segment bounds.
    /// </summary>
    /// <remarks>
    /// This method modifies <see cref="Stream.Position"/> property of the underlying stream.
    /// </remarks>
    /// <param name="offset">The offset in the underlying stream.</param>
    /// <param name="length">The length of the segment.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is larger than the remaining length of the underlying stream; or <paramref name="offset"/> if greater than the length of the underlying stream.</exception>
    public void Adjust(long offset, long length)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)offset, (ulong)stream.Length, nameof(offset));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)length, (ulong)(stream.Length - offset), nameof(length));

        this.length = length;
        this.offset = offset;
        stream.Position = offset;
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports reading.
    /// </summary>
    /// <value><see langword="true"/> if the stream supports reading; otherwise, <see langword="false"/>.</value>
    public override bool CanRead => stream.CanRead;

    /// <summary>
    /// Gets a value indicating whether the current stream supports seeking.
    /// </summary>
    /// <value><see langword="true"/> if the stream supports seeking; otherwise, <see langword="false"/>.</value>
    public override bool CanSeek => stream.CanSeek;

    /// <summary>
    /// Gets a value indicating whether the current stream supports writing.
    /// </summary>
    /// <value>Always <see langword="false"/>.</value>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => length;

    /// <inheritdoc/>
    public override long Position
    {
        get => stream.Position - offset;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)value, (ulong)length, nameof(value));

            stream.Position = offset + value;
        }
    }

    private long RemainingBytes => length - Position;

    /// <inheritdoc/>
    public override void Flush() => stream.Flush();

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken token = default) => stream.FlushAsync(token);

    /// <inheritdoc/>
    public override bool CanTimeout => stream.CanTimeout;

    /// <inheritdoc/>
    public override int ReadByte()
        => Position < length ? stream.ReadByte() : -1;

    /// <inheritdoc/>
    public override void WriteByte(byte value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        return stream.Read(buffer, offset, (int)Math.Min(count, RemainingBytes));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
        => stream.Read(buffer.TrimLength(int.CreateSaturating(RemainingBytes)));

    /// <inheritdoc/>
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        count = (int)Math.Min(count, RemainingBytes);
        return stream.BeginRead(buffer, offset, count, callback, state);
    }

    /// <inheritdoc/>
    public override int EndRead(IAsyncResult asyncResult) => stream.EndRead(asyncResult);

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token = default)
        => stream.ReadAsync(buffer, offset, (int)Math.Min(count, RemainingBytes), token);

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        => stream.ReadAsync(buffer.TrimLength(int.CreateSaturating(RemainingBytes)), token);

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPosition < 0L)
            throw new IOException();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, length, nameof(offset));

        Position = newPosition;
        return newPosition;
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)value, (ulong)(stream.Length - stream.Position), nameof(value));

        length = value;
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token = default) => Task.FromException(new NotSupportedException());

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new NotSupportedException());

    /// <inheritdoc/>
    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void EndWrite(IAsyncResult asyncResult) => throw new InvalidOperationException();

    /// <inheritdoc/>
    public override int ReadTimeout
    {
        get => stream.ReadTimeout;
        set => stream.ReadTimeout = value;
    }

    /// <inheritdoc/>
    public override int WriteTimeout
    {
        get => stream.WriteTimeout;
        set => stream.WriteTimeout = value;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen)
            stream.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (!leaveOpen)
            await stream.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}