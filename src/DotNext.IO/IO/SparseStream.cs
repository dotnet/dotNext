using System.Runtime.CompilerServices;

namespace DotNext.IO;

/// <summary>
/// Represents multiple streams as a single stream.
/// </summary>
/// <remarks>
/// The stream is available for read-only operations.
/// </remarks>
internal sealed class SparseStream : Stream, IFlushable
{
    private readonly IEnumerator<Stream> enumerator;
    private bool streamAvailable;

    /// <summary>
    /// Initializes a new sparse stream.
    /// </summary>
    /// <param name="streams">A collection of readable streams.</param>
    public SparseStream(IEnumerable<Stream> streams)
    {
        enumerator = streams.GetEnumerator();
        streamAvailable = enumerator.MoveNext();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MoveToNextStream() => streamAvailable = enumerator.MoveNext();

    /// <inheritdoc />
    public override int ReadByte()
    {
        var result = -1;

        for (; streamAvailable; MoveToNextStream())
        {
            result = enumerator.Current.ReadByte();

            if (result >= 0)
                break;
        }

        return result;
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        int count;
        for (count = 0; streamAvailable; MoveToNextStream())
        {
            count = enumerator.Current.Read(buffer);

            if (count > 0)
                break;
        }

        return count;
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);

        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        int count;
        for (count = 0; streamAvailable; MoveToNextStream())
        {
            count = await enumerator.Current.ReadAsync(buffer, token).ConfigureAwait(false);

            if (count > 0)
                break;
        }

        return count;
    }

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token = default)
        => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();

    /// <inheritdoc />
    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);

        for (; streamAvailable; MoveToNextStream())
            enumerator.Current.CopyTo(destination, bufferSize);
    }

    /// <inheritdoc />
    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token = default)
    {
        ValidateCopyToArguments(destination, bufferSize);

        for (; streamAvailable; MoveToNextStream())
            await enumerator.Current.CopyToAsync(destination, bufferSize, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
        if (streamAvailable)
            enumerator.Current.Flush();
    }

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken token = default)
        => streamAvailable ? enumerator.Current.FlushAsync(token) : Task.CompletedTask;

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void WriteByte(byte value) => throw new NotSupportedException();

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

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            enumerator.Dispose();
        }

        streamAvailable = false;
        base.Dispose(disposing);
    }
}