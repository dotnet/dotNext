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
            ValueTask<int> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<int>(token));
#else
                result = ValueTask.FromCanceled<int>(token);
#endif
            }
            else
            {
                try
                {
                    result = new (Read(buffer.Span));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<int>(e));
#else
                    result = ValueTask.FromException<int>(e);
#endif
                }
            }

            return result;
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
#if NETSTANDARD2_1
            => new (token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException()));
#else
            => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.FromException(new NotSupportedException());
#endif

        public sealed override void WriteByte(byte value) => throw new NotSupportedException();

        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => throw new NotSupportedException();

        public sealed override void EndWrite(IAsyncResult ar) => throw new InvalidOperationException();
    }
}