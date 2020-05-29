using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Buffers
{
    internal sealed class BufferedStreamWriter : IFlushableBufferWriter<byte>
    {
        /// <summary>
        /// Represents default initial buffer size.
        /// </summary>
        private const int DefaultInitialBufferSize = 256;

        private readonly Stream output;
        private readonly MemoryAllocator<byte> allocator;
        private MemoryOwner<byte> buffer;
        private int position;

        internal BufferedStreamWriter(Stream output, MemoryAllocator<byte> allocator)
        {
            this.output = output;
            this.allocator = allocator;
        }

        private int FreeCapacity => buffer.Length - position;

        private void Resize(int newSize)
        {
            var newBuffer = allocator(newSize);
            buffer.Memory.CopyTo(newBuffer.Memory);
            buffer.Dispose();
            buffer = newBuffer;
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            if (sizeHint == 0)
                sizeHint = 1;
            if (sizeHint > buffer.Length)
            {
                int currentLength = FreeCapacity, growBy = Math.Max(currentLength, sizeHint);
                if (currentLength == 0)
                    growBy = Math.Max(growBy, DefaultInitialBufferSize);
                var newSize = currentLength + growBy;
                if ((uint)newSize > int.MaxValue)
                {
                    newSize = currentLength + sizeHint;
                    if ((uint)newSize > int.MaxValue)
                        throw new OutOfMemoryException();
                }

                Resize(newSize);
            }
        }

        public Memory<byte> GetMemory(int sizeHint)
        {
            CheckAndResizeBuffer(sizeHint);
            return buffer.Memory.Slice(0, position);
        }

        public Span<byte> GetSpan(int sizeHint) => GetMemory(sizeHint).Span;

        public void Flush() => output.Flush();

        public Task FlushAsync(CancellationToken token) => output.FlushAsync(token);

        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (position > buffer.Length - count)
                throw new InvalidOperationException();

            output.Write(buffer.Memory.Span.Slice(position, count));
            position += count;
            // release buffer if it is cannot be reused
            if (position == buffer.Length)
            {
                buffer.Dispose();
                buffer = default;
            }
        }
    }
}