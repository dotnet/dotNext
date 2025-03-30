using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using DotNext.IO;

namespace DotNext.Runtime.Caching;

using Buffers;

public partial class DiskSpacePool
{
    private sealed class SegmentStream(SegmentHandle handle) : RandomAccessStream
    {
        private int length = handle.TryGetOwner() is { } owner ? owner.MaxSegmentSize : 0;
        
        private bool TryGetOwnerAndOffset([NotNullWhen(true)] out DiskSpacePool? owner, out long absoluteOffset)
        {
            if (handle.TryGetOwner() is { } ownerCopy)
            {
                owner = ownerCopy;
                absoluteOffset = handle.Offset;
                return true;
            }

            owner = null;
            absoluteOffset = 0L;
            return false;
        }
        
        public override void Flush()
        {
            // nothing to do, the handle is opened in WriteThrough mode
        }

        public override Task FlushAsync(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        public override void SetLength(long value)
        {
            ObjectDisposedException.ThrowIf(!TryGetOwnerAndOffset(out var owner, out _), this);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)value, (uint)owner.MaxSegmentSize, nameof(value));

            length = (int)value;
        }

        public override bool CanRead => true;
        
        public override bool CanWrite => true;
        
        public override long Length => length;

        private void Write(DiskSpacePool pool, long absoluteOffset, ReadOnlySpan<byte> buffer, int offset, int length)
        {
            buffer = buffer.TrimLength(length);
            pool.Write(absoluteOffset, buffer, offset);
            this.length = int.Max(this.length, buffer.Length + offset);
        }

        protected override void Write(ReadOnlySpan<byte> buffer, long offset)
        {
            ObjectDisposedException.ThrowIf(!TryGetOwnerAndOffset(out var owner, out var absoluteOffset), this);

            var newLength = owner.MaxSegmentSize - offset;
            if (newLength > 0L)
            {
                Write(owner, absoluteOffset, buffer, (int)offset, (int)newLength);
            }
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        private async ValueTask WriteAsync(DiskSpacePool pool, long absoluteOffset, ReadOnlyMemory<byte> buffer, int offset, int length,
            CancellationToken token)
        {
            await WriteWithoutLengthAdjustmentAsync(pool, absoluteOffset, buffer, offset, length, token).ConfigureAwait(false);
            this.length = int.Max(this.length, buffer.Length + offset);
        }

        private ValueTask WriteWithoutLengthAdjustmentAsync(DiskSpacePool pool, long absoluteOffset, ReadOnlyMemory<byte> buffer, int offset, int length,
            CancellationToken token)
            => pool.WriteAsync(absoluteOffset, buffer.TrimLength(length), offset, token);

        protected override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, long offset, CancellationToken token)
        {
            ValueTask task;
            long newLength;

            if (!TryGetOwnerAndOffset(out var owner, out var absoluteOffset))
            {
                task = ValueTask.FromException(new ObjectDisposedException(GetType().Name));
            }
            else if ((newLength = owner.MaxSegmentSize - offset) > 0L)
            {
                task = length == owner.MaxSegmentSize
                    ? WriteWithoutLengthAdjustmentAsync(owner, absoluteOffset, buffer, (int)offset, (int)newLength, token)
                    : WriteAsync(owner, absoluteOffset, buffer, (int)offset, (int)newLength, token);
            }
            else
            {
                task = ValueTask.CompletedTask;
            }

            return task;
        }

        protected override int Read(Span<byte> buffer, long offset)
        {
            ObjectDisposedException.ThrowIf(!TryGetOwnerAndOffset(out var owner, out var absoluteOffset), this);

            var newLength = length - offset;

            return newLength > 0L ? owner.Read(absoluteOffset, buffer, (int)offset, (int)newLength) : 0;
        }

        protected override ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken token)
        {
            ValueTask<int> task;
            long newLength;

            if (!TryGetOwnerAndOffset(out var owner, out var absoluteOffset))
            {
                task = ValueTask.FromException<int>(new ObjectDisposedException(GetType().Name));
            }
            else if ((newLength = length - offset) > 0L)
            {
                task = owner.ReadAsync(absoluteOffset, buffer, (int)offset, (int)newLength, token);
            }
            else
            {
                task = new(0);
            }

            return task;
        }
    }
}