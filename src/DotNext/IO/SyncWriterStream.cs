using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<byte>;

    internal sealed class SyncWriterStream<TOutput> : WriterStream<TOutput>
        where TOutput : notnull, IReadOnlySpanConsumer, IFlushable
    {
        internal SyncWriterStream(TOutput output)
            : base(output)
        {
        }

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
}
