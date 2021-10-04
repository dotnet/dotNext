using System.Runtime.InteropServices;

namespace DotNext.Buffers;

[StructLayout(LayoutKind.Auto)]
internal struct MemoryWriter : IConsumer<byte>
{
    private readonly Memory<byte> buffer;
    private int offset;

    internal MemoryWriter(Memory<byte> output)
    {
        buffer = output;
        offset = 0;
    }

    internal readonly int ConsumedBytes => offset;

    internal readonly Memory<byte> Result => buffer.Slice(0, offset);

    void IConsumer<byte>.Invoke(byte value) => buffer.Span[offset++] = value;
}