using System;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    using Intrinsics = Runtime.Intrinsics;

    [StructLayout(LayoutKind.Auto)]
    internal struct ValueReader<T> : IBufferReader<T>
        where T : unmanaged
    {
        private T result;
        private int offset;

        unsafe readonly int IBufferReader<T>.RemainingBytes => sizeof(T) - offset;

        readonly T IBufferReader<T>.Complete() => result;

        void IBufferReader<T>.Append(ReadOnlySpan<byte> block, ref int consumedBytes)
        {
            block.CopyTo(Intrinsics.AsSpan(ref result).Slice(offset));
            offset += block.Length;
        }
    }
}