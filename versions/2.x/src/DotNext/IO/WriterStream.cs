using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.IO
{
    internal abstract class WriterStream<TArg> : Stream
    {
        private protected readonly TArg argument;
        private readonly Action<TArg>? flush;
        private readonly Func<TArg, CancellationToken, Task>? flushAsync;
        private protected long writtenBytes;

        private protected WriterStream(TArg arg, Action<TArg>? flush, Func<TArg, CancellationToken, Task>? flushAsync)
        {
            this.flush = flush;
            this.flushAsync = flushAsync;
            argument = arg;
        }

        public sealed override bool CanRead => false;

        public sealed override bool CanWrite => true;

        public sealed override bool CanSeek => false;

        public override bool CanTimeout => false;

        public sealed override long Position
        {
            get => writtenBytes;
            set => throw new NotSupportedException();
        }

        public sealed override long Length => writtenBytes;

        public sealed override void SetLength(long value) => throw new NotSupportedException();

        public sealed override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public abstract override void Write(ReadOnlySpan<byte> buffer);

        public sealed override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<byte>(buffer, offset, count));

        public sealed override void WriteByte(byte value) => Write(CreateReadOnlySpan(ref value, 1));

        public abstract override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token);

        public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => WriteAsync(buffer.AsMemory(offset, count), token).AsTask();

        public abstract override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state);

        private static void EndWrite(Task task)
        {
            using (task)
                task.ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public override void EndWrite(IAsyncResult ar) => EndWrite((Task)ar);

        public sealed override void CopyTo(Stream destination, int bufferSize) => throw new NotSupportedException();

        public sealed override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException());

        public sealed override void Flush()
        {
            if (flush is null)
            {
                if (flushAsync != null)
                    flushAsync(argument, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else
            {
                flush(argument);
            }
        }

        public sealed override Task FlushAsync(CancellationToken token)
        {
            if (flushAsync is null)
            {
                return flush is null ?
                    Task.CompletedTask
                    : Task.Factory.StartNew(() => flush(argument), token, TaskCreationOptions.None, TaskScheduler.Current);
            }
            else
            {
                return flushAsync(argument, token);
            }
        }

        public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => throw new NotSupportedException();

        public sealed override int EndRead(IAsyncResult ar) => throw new InvalidOperationException();

        public sealed override int Read(Span<byte> buffer) => throw new NotSupportedException();

        public sealed override int ReadByte() => throw new NotSupportedException();

        public sealed override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled<int>(token) : Task.FromException<int>(new NotSupportedException());

        public sealed override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
            => new ValueTask<int>(token.IsCancellationRequested ? Task.FromCanceled<int>(token) : Task.FromException<int>(new NotSupportedException()));
    }
}