using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.IO;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct Segment(int Length, long Offset)
{
    private long End => Length + Offset;

    public static Segment operator >>(in Segment segment, int length)
        => new() { Offset = segment.End, Length = length };
}

internal interface IMemorySegmentProvider
{
    Span<byte> GetSpan(in Segment segment);

    MemoryHandle Pin(in Segment segment, int elementIndex);
}