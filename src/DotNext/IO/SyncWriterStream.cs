namespace DotNext.IO;

internal sealed class SyncWriterStream<TOutput>(TOutput output) : WriterStream<TOutput>(output)
    where TOutput : IConsumer<ReadOnlySpan<byte>>, IFlushable
{
    public override bool CanTimeout => false;

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        output.Invoke(buffer);
        writtenBytes += buffer.Length;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    {
        var task = ValueTask.CompletedTask;
        try
        {
            Write(buffer.Span);
        }
        catch (Exception e)
        {
            task = ValueTask.FromException(e);
        }

        return task;
    }
}