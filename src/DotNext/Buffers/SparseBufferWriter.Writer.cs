using System;
using System.Buffers;

namespace DotNext.Buffers
{
    public partial class SparseBufferWriter<T> : IBufferWriter<T>
    {
        private unsafe Memory<T> GetMemory()
        {
            if (last is null)
                first = last = new PooledMemoryChunk(allocator, chunkSize);
            else if (last.FreeCapacity == 0)
                last = new PooledMemoryChunk(allocator, growth(chunkSize, ref chunkIndex), last);

            return last.FreeMemory;
        }

        private Memory<T> GetMemory(int sizeHint)
        {
            ThrowIfDisposed();
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));

            if (sizeHint == 0)
                return GetMemory();

            if (last is null)
            {
                first = last = new PooledMemoryChunk(allocator, sizeHint);
            }
            else if (last.FreeCapacity == 0)
            {
                last = new PooledMemoryChunk(allocator, sizeHint, last);
            }
            else if (last.FreeCapacity < sizeHint)
            {
                // there are two possible cases:
                // the last chunk has occupied elements - attach a new chunk (causes a hole in the memory)
                // the last chunk has no occupied elements - realloc the memory
                if (last is PooledMemoryChunk pooledChunk && pooledChunk.IsUnused)
                    pooledChunk.Realloc(allocator, sizeHint);
                else
                    last = new PooledMemoryChunk(allocator, sizeHint, last);
            }

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
            if (last is not PooledMemoryChunk chunk)
                throw new InvalidOperationException();
            chunk.Advance(count);
            length += count;
        }
    }
}