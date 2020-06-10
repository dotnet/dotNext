using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.IO
{
    using static Threading.AsyncDelegate;

    internal sealed class BufferWriterStream<TWriter> : Stream
        where TWriter : class, IBufferWriter<byte>
    {
        private readonly TWriter writer;
        private readonly Action<TWriter>? flush;
        private readonly Func<TWriter, CancellationToken, Task>? flushAsync;
        private long writtenBytes;

        internal BufferWriterStream(TWriter writer, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
        {
            this.writer = writer;
            this.flush = flush;
            this.flushAsync = flushAsync;
        }

        public override bool CanRead => false;

        public override bool CanWrite => true;

        public override bool CanSeek => false;

        public override bool CanTimeout => false;

        public override long Position
        {
            get => writtenBytes;
            set => throw new NotSupportedException();
        }

        public override long Length => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
                return;

            writer.Write(buffer);
            writtenBytes += buffer.Length;
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<byte>(buffer, offset, count));

        public override void WriteByte(byte value) => Write(CreateReadOnlySpan(ref value, 1));

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return new ValueTask(Task.FromCanceled(token));

            Write(buffer.Span);
            return new ValueTask();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return Task.FromCanceled(token);

            Write(new ReadOnlySpan<byte>(buffer, offset, count));
            return Task.CompletedTask;
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => new Action<object?>(_ => Write(buffer, offset, count)).BeginInvoke(state, callback);

        private static void EndWrite(Task task)
        {
            try
            {
                task.ConfigureAwait(false).GetAwaiter().GetResult();
            }
            finally
            {
                task.Dispose();
            }
        }

        public override void EndWrite(IAsyncResult ar) => EndWrite((Task)ar);

        public override void CopyTo(Stream destination, int bufferSize) => throw new NotSupportedException();

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException());

        public override void Flush()
        {
            if (flush is null)
            {
                if (flushAsync != null)
                    flushAsync(writer, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else
            {
                flush(writer);
            }
        }

        public override Task FlushAsync(CancellationToken token)
        {
            if (flushAsync is null)
            {
                return flush is null ?
                    Task.CompletedTask
                    : Task.Factory.StartNew(() => flush(writer), token, TaskCreationOptions.None, TaskScheduler.Current);
            }
            else
            {
                return flushAsync(writer, token);
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => throw new NotSupportedException();

        public override int EndRead(IAsyncResult ar) => throw new InvalidOperationException();

        public override int Read(Span<byte> buffer) => throw new NotSupportedException();

        public override int ReadByte() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled<int>(token) : Task.FromException<int>(new NotSupportedException());

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
            => new ValueTask<int>(token.IsCancellationRequested ? Task.FromCanceled<int>(token) : Task.FromException<int>(new NotSupportedException()));
    }
}
