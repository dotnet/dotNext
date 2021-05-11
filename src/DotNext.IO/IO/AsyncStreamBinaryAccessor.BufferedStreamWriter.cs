using System;
using System.Buffers;

namespace DotNext.IO
{
    using Buffers;

    internal partial struct AsyncStreamBinaryAccessor
    {
        // This writer allows to reuse pre-allocated buffer for the stream.
        // If overflow occurs then switch to the rented memory.
        private sealed class BufferedStreamWriter : Disposable, IBufferWriter<byte>
        {
            private readonly Memory<byte> buffer;
            private int position;   // written bytes to the pre-allocated buffer
            private PooledBufferWriter<byte>? extraBuffer;

            internal BufferedStreamWriter(Memory<byte> buffer)
            {
                this.buffer = buffer;
            }

            internal ReadOnlyMemory<byte> WrittenMemory => extraBuffer is null ? buffer.Slice(0, position) : extraBuffer.WrittenMemory;

            public Memory<byte> GetMemory(int sizeHint)
            {
                if (sizeHint < 0)
                    throw new ArgumentOutOfRangeException(nameof(sizeHint));

                var result = buffer.Slice(position);
                if (!result.IsEmpty && result.Length >= sizeHint)
                    goto exit;

                if (extraBuffer is null)
                {
                    extraBuffer = new(null, sizeHint + position);
                    extraBuffer.Write(buffer.Span);
                }

                result = extraBuffer.GetMemory(sizeHint);

            exit:
                return result;
            }

            public Span<byte> GetSpan(int sizeHint) => GetMemory(sizeHint).Span;

            public void Advance(int count)
            {
                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count));

                if (extraBuffer is not null)
                {
                    extraBuffer.Advance(count);
                }
                else if (count + position > buffer.Length)
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    position += count;
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    extraBuffer?.Dispose();
                    extraBuffer = null;
                }

                base.Dispose(disposing);
            }
        }
    }
}