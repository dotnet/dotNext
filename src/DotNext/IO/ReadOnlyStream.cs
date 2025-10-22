namespace DotNext.IO;

using Buffers;
using static Threading.Tasks.Synchronization;

internal abstract class ReadOnlyStream : ModernStream
{
    public sealed override bool CanRead => true;

    public sealed override bool CanWrite => false;

    public override bool CanTimeout => false;

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken token)
        => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

    public abstract override int Read(Span<byte> buffer);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        ValueTask<int> result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<int>(token);
        }
        else
        {
            try
            {
                result = new(Read(buffer.Span));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<int>(e);
            }
        }

        return result;
    }

    public sealed override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

    public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.FromException(new NotSupportedException());
}

internal sealed class ReadOnlyStream<TArg>(Func<Memory<byte>, TArg, CancellationToken, ValueTask<int>> reader, TArg arg) : ReadOnlyStream
{
    private const int DefaultTimeout = 4000;
    private int timeout = DefaultTimeout;
    private CancellationTokenSource? timeoutSource;

    public override int ReadTimeout
    {
        get => timeout;
        set => timeout = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public override bool CanTimeout => true;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Length => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override bool CanSeek => false;

    public override void SetLength(long value)
        => throw new NotSupportedException();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        => reader(buffer, arg, token);

    public override int Read(Span<byte> buffer)
    {
        int writtenCount;
        if (buffer.IsEmpty)
        {
            writtenCount = 0;
        }
        else
        {
            var tempBuffer = Memory.AllocateExactly<byte>(buffer.Length);
            timeoutSource ??= new();
            timeoutSource.CancelAfter(timeout);
            var task = ReadAsync(tempBuffer.Memory, timeoutSource.Token);
            try
            {
                writtenCount = task.Wait();
                tempBuffer.Span.Slice(0, writtenCount).CopyTo(buffer);
            }
            finally
            {
                if (!timeoutSource.TryReset())
                {
                    timeoutSource.Dispose();
                    timeoutSource = null;
                }
                
                tempBuffer.Dispose();
            }
        }

        return writtenCount;
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