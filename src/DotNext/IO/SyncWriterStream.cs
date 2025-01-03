namespace DotNext.IO;

using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<byte>;

internal sealed class SyncWriterStream<TOutput>(TOutput output) : WriterStream<TOutput>(output)
    where TOutput : IReadOnlySpanConsumer, IFlushable
{
    public override bool CanTimeout => false;

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        output.Invoke(buffer);
        writtenBytes += buffer.Length;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
    {
        await output.Invoke(buffer, token).ConfigureAwait(false);
        writtenBytes += buffer.Length;
    }
}