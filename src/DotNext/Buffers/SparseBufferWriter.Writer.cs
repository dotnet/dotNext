using System;
using System.Buffers;

namespace DotNext.Buffers
{
    public partial class SparseBufferWriter<T> : IBufferWriter<T>
    {
        private Memory<T> GetMemory(int sizeHint)
        {
            ThrowIfDisposed();
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            if (sizeHint == 0)
                sizeHint = chunkSize;

            if (last is null)
                first = last = new PooledMemoryChunk(allocator, sizeHint);
            else if (last.FreeCapacity > sizeHint || sizeHint < 0)
                last = new PooledMemoryChunk(allocator, sizeHint, last);

            return last.FreeMemory;
        }

        /// <inheritdoc />
        Memory<T> IBufferWriter<T>.GetMemory(int sizeHint)
            => GetMemory(sizeHint);

        /// <inheritdoc />
        Span<T> IBufferWriter<T>.GetSpan(int sizeHint)
            => GetMemory(sizeHint).Span;

        /// <inheritdoc />
        void IBufferWriter<T>.Advance(int count)
        {
            ThrowIfDisposed();
            if (last is null)
                throw new InvalidOperationException();
            last.Advance(count);
            length += count;
        }
    }
}