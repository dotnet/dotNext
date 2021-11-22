using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.IO;

[StructLayout(LayoutKind.Auto)]
internal readonly struct Segment : IEquatable<Segment>
{
    internal readonly int Length;
    internal readonly long Offset;

    internal Segment(long offset, int length)
    {
        Length = length;
        Offset = offset;
    }

    internal Segment Next(int length) => new(Length + Offset, length);

    private bool Equals(in Segment other)
        => Length == other.Length && Offset == other.Offset;

    public bool Equals(Segment other)
        => Equals(in other);

    public override bool Equals([NotNullWhen(true)] object? other) => other is Segment window && Equals(in window);

    public override int GetHashCode()
        => HashCode.Combine(Offset, Length);

    public static bool operator ==(in Segment x, in Segment y)
        => x.Equals(in y);

    public static bool operator !=(in Segment x, in Segment y)
        => !x.Equals(in y);
}

internal interface IMemorySegmentProvider
{
    Span<byte> GetSpan(in Segment segment);

    MemoryHandle Pin(in Segment segment, int elementIndex);
}