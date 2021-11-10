using System.Buffers;

namespace DotNext.IO;

using Buffers;

internal sealed class AsyncWriterStream<TOutput> : WriterStream<TOutput>
    where TOutput : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>, IFlushable
{
    private const int DefaultTimeout = 4000;
    private int timeout;

    internal AsyncWriterStream(TOutput output)
        : base(output)
    {
        timeout = DefaultTimeout;
    }

    public override int WriteTimeout
    {
        get => timeout;
        set => timeout = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public override bool CanTimeout => true;

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
    {
        await output.Invoke(buffer, token).ConfigureAwait(false);
        writtenBytes += buffer.Length;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (!buffer.IsEmpty)
        {
            using var rental = new MemoryOwner<byte>(ArrayPool<byte>.Shared, buffer.Length);
            buffer.CopyTo(rental.Span);
            using var source = new CancellationTokenSource(timeout);
            using var task = WriteAsync(rental.Memory, source.Token).AsTask();
            task.Wait(source.Token);
        }
    }
}