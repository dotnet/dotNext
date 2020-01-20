using System;
using System.Runtime.InteropServices;
using Missing = System.Reflection.Missing;

namespace DotNext.Buffers
{
    [StructLayout(LayoutKind.Auto)]
    internal struct MemoryReader : IBufferReader<Missing>
    {
        private readonly Memory<byte> buffer;
        private int offset;

        internal MemoryReader(Memory<byte> buffer)
        {
            this.buffer = buffer;
            offset = 0;
        }

        int IBufferReader<Missing>.RemainingBytes => buffer.Length - offset;

        Missing IBufferReader<Missing>.Complete() => Missing.Value;

        void IBufferReader<Missing>.Append(ReadOnlySpan<byte> block, ref int consumedBytes)
        {
            block.CopyTo(buffer.Span.Slice(offset));
            offset += block.Length;
        }
    }
}