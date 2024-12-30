namespace DotNext.IO;

internal sealed class AsyncWriterStream<TOutput>(TOutput output) : WriterStream<TOutput>(output)
    where TOutput : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>, IFlushable
{
    private const int DefaultTimeout = 4000;
    private int timeout = DefaultTimeout;
    private CancellationTokenSource? timeoutSource;

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
            using var rental = buffer.Copy();

            timeoutSource ??= new();
            timeoutSource.CancelAfter(timeout);
            var task = WriteAsync(rental.Memory, timeoutSource.Token).AsTask();
            try
            {
                task.Wait();
            }
            finally
            {
                task.Dispose();

                if (!timeoutSource.TryReset())
                {
                    timeoutSource.Dispose();
                    timeoutSource = null;
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timeoutSource?.Dispose();
        }

        base.Dispose(disposing);
    }
}