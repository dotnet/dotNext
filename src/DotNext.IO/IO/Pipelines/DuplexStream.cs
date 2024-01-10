using System.IO.Pipelines;

namespace DotNext.IO.Pipelines;

/// <summary>
/// Represents duplex stream suitable for reading and writing from the duplex pipe.
/// </summary>
/// <param name="pipe">The duplex pipe.</param>
/// <param name="leaveInputOpen"><see langword="true"/> to leave <see cref="IDuplexPipe.Input"/> available for reads; otherwise, <see langword="false"/>.</param>
/// <param name="leaveOutputOpen"><see langword="true"/> to leave <see cref="IDuplexPipe.Output"/> available for writes; otherwise, <see langword="false"/>.</param>
public sealed class DuplexStream(IDuplexPipe pipe, bool leaveInputOpen = false, bool leaveOutputOpen = false) : Stream
{
    private readonly Stream reader = pipe.Input.AsStream(leaveInputOpen);
    private readonly Stream writer = pipe.Output.AsStream(leaveOutputOpen);

    /// <inheritdoc/>
    public override bool CanRead => reader.CanRead;

    /// <inheritdoc/>
    public override bool CanWrite => writer.CanWrite;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanTimeout => reader.CanTimeout && writer.CanTimeout;

    /// <inheritdoc/>
    public override int ReadTimeout
    {
        get => reader.ReadTimeout;
        set => reader.ReadTimeout = value;
    }

    /// <inheritdoc/>
    public override int WriteTimeout
    {
        get => writer.WriteTimeout;
        set => writer.WriteTimeout = value;
    }

    /// <inheritdoc/>
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        => reader.BeginRead(buffer, offset, count, callback, state);

    /// <inheritdoc/>
    public override int EndRead(IAsyncResult asyncResult)
        => reader.EndRead(asyncResult);

    /// <inheritdoc/>
    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        => writer.BeginWrite(buffer, offset, count, callback, state);

    /// <inheritdoc/>
    public override void EndWrite(IAsyncResult asyncResult) => writer.EndWrite(asyncResult);

    /// <inheritdoc/>
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
        => reader.CopyToAsync(destination, bufferSize, token);

    /// <inheritdoc/>
    public override void CopyTo(Stream destination, int bufferSize)
        => reader.CopyTo(destination, bufferSize);

    /// <inheritdoc/>
    public override void Flush() => writer.Flush();

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken token)
        => writer.FlushAsync(token);

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        => writer.WriteAsync(buffer, token);

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        => writer.WriteAsync(buffer, offset, count, token);

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer) => writer.Write(buffer);

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => writer.Write(buffer, offset, count);

    /// <inheritdoc/>
    public override void WriteByte(byte value) => writer.WriteByte(value);

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        => reader.ReadAsync(buffer, token);

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        => reader.ReadAsync(buffer, offset, count, token);

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer) => reader.Read(buffer);

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
        => reader.Read(buffer, offset, count);

    /// <inheritdoc/>
    public override int ReadByte() => reader.ReadByte();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            reader.Dispose();
            writer.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            await reader.DisposeAsync().ConfigureAwait(false);
            await writer.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}