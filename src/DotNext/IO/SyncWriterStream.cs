using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<byte>;
    using static Threading.AsyncDelegate;

    internal sealed class SyncWriterStream<TOutput> : WriterStream<TOutput>
        where TOutput : notnull, IReadOnlySpanConsumer, IFlushable
    {
        internal SyncWriterStream(TOutput output)
            : base(output)
        {
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            output.Invoke(buffer);
            writtenBytes += buffer.Length;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
        {
            Task result;

            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                try
                {
                    Write(buffer.Span);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => new Action<object?>(_ => Write(buffer, offset, count)).BeginInvoke(state, callback);
    }
}
