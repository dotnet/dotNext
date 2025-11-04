using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents multiple streams as a single stream.
/// </summary>
/// <remarks>
/// The stream is available for read-only operations.
/// </remarks>
internal abstract class SparseStream(bool leaveOpen) : ModernStream
{
    private int runningIndex;
    
    protected abstract ReadOnlySpan<Stream> Streams { get; }

    private Stream? Current
    {
        get
        {
            var streams = Streams;

            return (uint)runningIndex < (uint)streams.Length ? streams[runningIndex] : null;
        }
    }

    /// <inheritdoc />
    public sealed override int Read(Span<byte> buffer)
    {
        int count;
        for (count = 0; Current is { } current; runningIndex++)
        {
            count = current.Read(buffer);

            if (count > 0)
                break;
        }

        return count;
    }

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public sealed override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        int count;
        for (count = 0; Current is { } current; runningIndex++)
        {
            count = await current.ReadAsync(buffer, token).ConfigureAwait(false);

            if (count > 0)
                break;
        }

        return count;
    }

    /// <inheritdoc />
    public sealed override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);

        for (; Current is { } current; runningIndex++)
            current.CopyTo(destination, bufferSize);
    }

    /// <inheritdoc />
    public sealed override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
    {
        ValidateCopyToArguments(destination, bufferSize);

        for (; Current is { } current; runningIndex++)
            await current.CopyToAsync(destination, bufferSize, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public sealed override bool CanRead => true;

    /// <inheritdoc />
    public sealed override bool CanWrite => false;

    /// <inheritdoc />
    public sealed override bool CanSeek => false;

    /// <inheritdoc />
    public sealed override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc cref="Stream.Flush"/>
    public sealed override void Flush() => Current?.Flush();

    /// <inheritdoc cref="Stream.FlushAsync(CancellationToken)"/>
    public sealed override Task FlushAsync(CancellationToken token)
        => Current?.FlushAsync(token) ?? Task.CompletedTask;

    /// <inheritdoc />
    public sealed override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public sealed override long Length
    {
        get
        {
            var length = 0L;

            foreach (var stream in Streams)
            {
                length += stream.Length;
            }

            return length;
        }
    }

    /// <inheritdoc />
    public sealed override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public sealed override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

    /// <inheritdoc/>
    public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new NotSupportedException());

    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen)
        {
            Disposable.Dispose(Streams);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (!leaveOpen)
        {
            for (var i = 0; i < Streams.Length; i++)
            {
                await Streams[i].DisposeAsync().ConfigureAwait(false);
            }
        }

        GC.SuppressFinalize(this);
    }

    public static SparseStream Create<T>(T streams, bool leaveOpen)
        where T : struct, ITuple
        => new SparseStream<T>(streams, leaveOpen);

    public static SparseStream Create(Stream stream, ReadOnlySpan<Stream> streams, bool leaveOpen)
        => new UnboundedSparseStream(stream, streams, leaveOpen);
}

file sealed class SparseStream<T>(T streams, bool leaveOpen) : SparseStream(leaveOpen)
    where T : struct, ITuple
{
    protected override ReadOnlySpan<Stream> Streams
        => MemoryMarshal.CreateReadOnlySpan(in Unsafe.As<T, Stream>(ref Unsafe.AsRef(in streams)), streams.Length);
}

file sealed class UnboundedSparseStream : SparseStream
{
    private MemoryOwner<Stream> streams;

    internal UnboundedSparseStream(Stream stream, ReadOnlySpan<Stream> streams, bool leaveOpen)
        : base(leaveOpen)
    {
        Debug.Assert(streams.Length < int.MaxValue);

        this.streams = Memory.AllocateExactly<Stream>(streams.Length + 1);
        var output = this.streams.Span;
        output[0] = stream;
        streams.CopyTo(output.Slice(1));
    }

    protected override ReadOnlySpan<Stream> Streams => streams.Span;

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            streams.Dispose();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            streams.Dispose();
        }
    }
}