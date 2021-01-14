using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;
    using static Threading.Tasks.Continuation;

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
                buffer.CopyTo(rental.Memory.Span);
                using var source = new CancellationTokenSource(timeout);
                using var task = WriteAsync(rental.Memory, source.Token).AsTask();
                task.Wait(source.Token);
            }
        }

        private async Task WriteWithTimeoutAsync(ReadOnlyMemory<byte> buffer)
        {
            using var source = new CancellationTokenSource(timeout);
            await WriteAsync(buffer, source.Token).ConfigureAwait(false);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            var task = WriteWithTimeoutAsync(buffer.AsMemory(offset, count));

            // attach state only if it's necessary
            if (state is not null)
                task = task.AttachState(state);

            if (callback is not null)
            {
                if (task.IsCompleted)
                    callback(task);
                else
                    task.ConfigureAwait(false).GetAwaiter().OnCompleted(() => callback(task));
            }

            return task;
        }
    }
}