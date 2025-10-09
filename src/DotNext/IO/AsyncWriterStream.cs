namespace DotNext.IO;

using static Threading.Tasks.Synchronization;

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

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        => SubmitWrite(buffer.Length, output.Invoke(buffer, token));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (!buffer.IsEmpty)
        {
            var rental = buffer.Copy();
            timeoutSource ??= new();
            timeoutSource.CancelAfter(timeout);
            var task = WriteAsync(rental.Memory, timeoutSource.Token);
            try
            {
                task.Wait();
            }
            finally
            {
                if (!timeoutSource.TryReset())
                {
                    timeoutSource.Dispose();
                    timeoutSource = null;
                }
                
                rental.Dispose();
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