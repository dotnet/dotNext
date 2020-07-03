using System;
using System.Runtime.InteropServices;
using Missing = System.Reflection.Missing;

namespace DotNext.Buffers
{
    [StructLayout(LayoutKind.Auto)]
    internal struct MemoryReader : IBufferReader<int>, IBufferReader<Missing>
    {
        private readonly Memory<byte> buffer;
        private int offset;
        private bool eosReached;

        internal MemoryReader(Memory<byte> buffer)
        {
            this.buffer = buffer;
            offset = 0;
            eosReached = false;
        }

        public readonly int RemainingBytes => eosReached ? 0 : buffer.Length - offset;

        int IBufferReader<int>.Complete() => offset;

        Missing IBufferReader<Missing>.Complete() => Missing.Value;

        internal readonly int BytesWritten => offset;

        internal readonly bool IsCompleted => offset >= buffer.Length;

        public void Append(ReadOnlySpan<byte> block, ref int consumedBytes)
        {
            block.CopyTo(buffer.Span.Slice(offset));
            offset += block.Length;
        }

        void IBufferReader<int>.EndOfStream() => eosReached = true;
    }
}