using System.Runtime.CompilerServices;
using DotNext.IO;

namespace DotNext.Runtime.Caching;

using Buffers;

public partial class DiskSpacePool
{
    private sealed class SegmentStream(WeakReference<DiskSpacePool?> pool, long absoluteOffset) : RandomAccessStream
    {
        private int length = pool.TryGetTarget(out var target) ? target.MaxSegmentSize : 0;
        
        public override void Flush()
        {
            // nothing to do, the handle is opened in WriteThrough mode
        }

        public override Task FlushAsync(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        public override void SetLength(long value)
        {
            ObjectDisposedException.ThrowIf(!pool.TryGetTarget(out var target), this);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)value, (uint)target.MaxSegmentSize, nameof(value));

            length = (int)value;
        }

        public override bool CanRead => true;
        
        public override bool CanWrite => true;
        
        public override long Length => length;

        private void Write(DiskSpacePool pool, ReadOnlySpan<byte> buffer, int offset, int length)
        {
            buffer = buffer.TrimLength(length);
            pool.Write(absoluteOffset, buffer, offset);
            this.length = int.Max(this.length, buffer.Length + offset);
        }

        protected override void Write(ReadOnlySpan<byte> buffer, long offset)
        {
            ObjectDisposedException.ThrowIf(!pool.TryGetTarget(out var target), this);

            var newLength = target.MaxSegmentSize - offset;
            if (newLength > 0L)
            {
                Write(target, buffer, (int)offset, (int)newLength);
            }
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        private async ValueTask WriteAsync(DiskSpacePool pool, ReadOnlyMemory<byte> buffer, int offset, int length, CancellationToken token)
        {
            await WriteWithoutLengthAdjustmentAsync(pool, buffer, offset, length, token).ConfigureAwait(false);
            this.length = int.Max(this.length, buffer.Length + offset);
        }

        private ValueTask WriteWithoutLengthAdjustmentAsync(DiskSpacePool pool, ReadOnlyMemory<byte> buffer, int offset, int length,
            CancellationToken token)
            => pool.WriteAsync(absoluteOffset, buffer.TrimLength(length), offset, token);

        protected override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, long offset, CancellationToken token)
        {
            ValueTask task;
            long newLength;

            if (!pool.TryGetTarget(out var target))
            {
                task = ValueTask.FromException(new ObjectDisposedException(GetType().Name));
            }
            else if ((newLength = target.MaxSegmentSize - offset) > 0L)
            {
                task = length == target.MaxSegmentSize
                    ? WriteWithoutLengthAdjustmentAsync(target, buffer, (int)offset, (int)newLength, token)
                    : WriteAsync(target, buffer, (int)offset, (int)newLength, token);
            }
            else
            {
                task = ValueTask.CompletedTask;
            }

            return task;
        }

        protected override int Read(Span<byte> buffer, long offset)
        {
            ObjectDisposedException.ThrowIf(!pool.TryGetTarget(out var target), this);

            var newLength = length - offset;

            return newLength > 0L ? target.Read(absoluteOffset, buffer, (int)offset, (int)newLength) : 0;
        }

        protected override ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token)
        {
            ValueTask<int> task;
            long newLength;

            if (!pool.TryGetTarget(out var target))
            {
                task = ValueTask.FromException<int>(new ObjectDisposedException(GetType().Name));
            }
            else if ((newLength = length - offset) > 0L)
            {
                task = target.ReadAsync(absoluteOffset, buffer, (int)offset, (int)newLength, token);
            }
            else
            {
                task = new(0);
            }

            return task;
        }
    }
}