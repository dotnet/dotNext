using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.IO
{
    using static Threading.AsyncDelegate;

    internal abstract class ReadOnlyStream : Stream, IFlushable
    {
        public sealed override bool CanRead => true;

        public sealed override bool CanWrite => false;

        public sealed override bool CanTimeout => false;

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        public override abstract int Read(Span<byte> buffer);

        public sealed override int ReadByte()
        {
            byte result = 0;
            return Read(CreateSpan(ref result, 1)) == 1 ? result : -1;
        }

        public sealed override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public sealed override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            Task<int> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<int>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<int>(Read(buffer.Span));
                }
                catch (Exception e)
                {
                    result = Task.FromException<int>(e);
                }
            }

            return new ValueTask<int>(result);
        }

        public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();

        public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => new Func<object?, int>(_ => Read(buffer, offset, count)).BeginInvoke(state, callback);

        private static int EndRead(Task<int> task)
        {
            using (task)
                return task.Result;
        }

        public sealed override int EndRead(IAsyncResult ar) => EndRead((Task<int>)ar);

        public sealed override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public sealed override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

        public sealed override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException());

        public sealed override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
            => new ValueTask(token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException()));

        public sealed override void WriteByte(byte value) => throw new NotSupportedException();

        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => throw new NotSupportedException();

        public sealed override void EndWrite(IAsyncResult ar) => throw new InvalidOperationException();
    }
}