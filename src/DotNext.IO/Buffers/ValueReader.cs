using System.Runtime.InteropServices;

namespace DotNext.Buffers;

[StructLayout(LayoutKind.Auto)]
internal struct ValueReader<T> : IBufferReader<T>
    where T : unmanaged
{
    private T result;
    private int offset;

    readonly unsafe int IBufferReader<T>.RemainingBytes => sizeof(T) - offset;

    readonly T IBufferReader<T>.Complete() => result;

    void IBufferReader<T>.Append(ReadOnlySpan<byte> block, ref int consumedBytes)
    {
        block.CopyTo(Span.AsBytes(ref result).Slice(offset));
        offset += block.Length;
    }
}