namespace DotNext.IO;

using static Buffers.BufferHelpers;

/// <summary>
/// Represents read-only view over the portion of underlying stream.
/// </summary>
/// <remarks>
/// The segmentation is supported only for seekable streams.
/// </remarks>
public sealed class StreamSegment : Stream, IFlushable
{
    private readonly bool leaveOpen;
    private long length, offset;

    /// <summary>
    /// Initializes a new segment of the specified stream.
    /// </summary>
    /// <param name="stream">The underlying stream represented by the segment.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open after the object is disposed; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
    public StreamSegment(Stream stream, bool leaveOpen = true)
    {
        BaseStream = stream ?? throw new ArgumentNullException(nameof(stream));
        length = stream.Length;
        offset = 0L;
        this.leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Gets underlying stream.
    /// </summary>
    public Stream BaseStream { get; }

    /// <summary>
    /// Establishes segment bounds.
    /// </summary>
    /// <remarks>
    /// This method modifies <see cref="Stream.Position"/> property of the underlying stream.
    /// </remarks>
    /// <param name="offset">The offset in the underlying stream.</param>
    /// <param name="length">The length of the segment.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is larger than the reamining length of the underlying stream; or <paramref name="offset"/> if greater than the length of the underlying stream.</exception>
    public void Adjust(long offset, long length)
    {
        if (offset < 0L || offset > BaseStream.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (length < 0L || length > BaseStream.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(length));
        this.length = length;
        this.offset = offset;
        BaseStream.Position = offset;
    }

    /// <summary>
    /// Gets a value indicating whether the current stream supports reading.
    /// </summary>
    /// <value><see langword="true"/> if the stream supports reading; otherwise, <see langword="false"/>.</value>
    public override bool CanRead => BaseStream.CanRead;

    /// <summary>
    /// Gets a value indicating whether the current stream supports seeking.
    /// </summary>
    /// <value><see langword="true"/> if the stream supports seeking; otherwise, <see langword="false"/>.</value>
    public override bool CanSeek => BaseStream.CanSeek;

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
        get => BaseStream.Position - offset;
        set
        {
            if (value < 0L || value > length)
                throw new ArgumentOutOfRangeException(nameof(value));
            BaseStream.Position = offset + value;
        }
    }

    private long RemainingBytes => length - Position;

    /// <inheritdoc/>
    public override void Flush() => BaseStream.Flush();

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken token = default) => BaseStream.FlushAsync(token);

    /// <inheritdoc/>
    public override bool CanTimeout => BaseStream.CanTimeout;

    /// <inheritdoc/>
    public override int ReadByte()
        => Position < length ? BaseStream.ReadByte() : -1;

    /// <inheritdoc/>
    public override void WriteByte(byte value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
        => BaseStream.Read(buffer, offset, (int)Math.Min(count, RemainingBytes));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
        => BaseStream.Read(buffer.TrimLength(RemainingBytes.Truncate()));

    /// <inheritdoc/>
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        count = (int)Math.Min(count, RemainingBytes);
        return BaseStream.BeginRead(buffer, offset, count, callback, state);
    }

    /// <inheritdoc/>
    public override int EndRead(IAsyncResult asyncResult) => BaseStream.EndRead(asyncResult);

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token = default)
        => BaseStream.ReadAsync(buffer, offset, (int)Math.Min(count, RemainingBytes), token);

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        => BaseStream.ReadAsync(buffer.TrimLength(RemainingBytes.Truncate()), token);

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

        if (newPosition < 0)
            throw new IOException();

        if (newPosition > length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        Position = newPosition;
        return newPosition;
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        if (value > BaseStream.Length - BaseStream.Position)
            throw new ArgumentOutOfRangeException(nameof(value));
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
        get => BaseStream.ReadTimeout;
        set => BaseStream.ReadTimeout = value;
    }

    /// <inheritdoc/>
    public override int WriteTimeout
    {
        get => BaseStream.WriteTimeout;
        set => BaseStream.WriteTimeout = value;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen)
            BaseStream.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (!leaveOpen)
            await BaseStream.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}