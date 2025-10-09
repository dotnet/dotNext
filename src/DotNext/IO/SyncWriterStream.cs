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

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        => SubmitWrite(buffer.Length, output.Invoke(buffer, token));
}