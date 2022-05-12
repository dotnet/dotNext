using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.IO;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct Segment
{
    internal int Length { get; init; }

    internal long Offset { get; init; }

    private long End => Length + Offset;

    public static Segment operator >>(in Segment segment, int length)
        => new() { Offset = segment.End, Length = length };
}

internal interface IMemorySegmentProvider
{
    Span<byte> GetSpan(in Segment segment);

    MemoryHandle Pin(in Segment segment, int elementIndex);
}