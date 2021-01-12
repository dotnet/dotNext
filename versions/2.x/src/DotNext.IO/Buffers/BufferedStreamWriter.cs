using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Buffers
{
    internal sealed class BufferedStreamWriter : IFlushableBufferWriter<byte>
    {
        private readonly Stream output;
        private readonly MemoryAllocator<byte>? allocator;
        private MemoryOwner<byte> buffer;
        private int position;

        internal BufferedStreamWriter(Stream output, MemoryAllocator<byte>? allocator)
        {
            this.output = output;
            this.allocator = allocator;
        }

        public Memory<byte> GetMemory(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            if (sizeHint == 0)
                sizeHint = 1;
            if (sizeHint > buffer.Length - position)
            {
                buffer.Dispose();
                position = 0;
                buffer = allocator.Invoke(sizeHint, false);
            }

            return buffer.Memory.Slice(position);
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
                position = 0;
            }
        }
    }
}