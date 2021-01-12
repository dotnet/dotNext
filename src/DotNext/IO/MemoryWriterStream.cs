using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;
    using static Threading.Tasks.Continuation;

    internal sealed class MemoryWriterStream<TArg> : WriterStream<TArg>
    {
        private const int DefaultTimeout = 4000;
        private readonly Func<ReadOnlyMemory<byte>, TArg, CancellationToken, ValueTask> writer;
        private int timeout;

        internal MemoryWriterStream(Func<ReadOnlyMemory<byte>, TArg, CancellationToken, ValueTask> writer, TArg arg, Action<TArg>? flush, Func<TArg, CancellationToken, Task>? flushAsync)
            : base(arg, flush, flushAsync)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
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
            await writer(buffer, argument, token).ConfigureAwait(false);
            writtenBytes += buffer.Length;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (!buffer.IsEmpty)
            {
                using var rental = new ArrayRental<byte>(buffer.Length);
                buffer.CopyTo(rental.Span);
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
            if (state != null)
                task = task.AttachState(state);

            if (callback != null)
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