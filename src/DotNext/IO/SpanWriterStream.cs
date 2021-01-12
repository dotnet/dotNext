using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using static Threading.AsyncDelegate;

    internal sealed class SpanWriterStream<TArg> : WriterStream<TArg>
    {
        private readonly ReadOnlySpanAction<byte, TArg> writer;

        internal SpanWriterStream(ReadOnlySpanAction<byte, TArg> writer, TArg arg, Action<TArg>? flush, Func<TArg, CancellationToken, Task>? flushAsync)
            : base(arg, flush, flushAsync)
            => this.writer = writer ?? throw new ArgumentNullException(nameof(writer));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (!buffer.IsEmpty)
            {
                writer(buffer, argument);
                writtenBytes += buffer.Length;
            }
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
