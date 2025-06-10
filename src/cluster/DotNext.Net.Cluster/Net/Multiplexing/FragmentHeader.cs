using System.Runtime.InteropServices;

namespace DotNext.Net.Multiplexing;

using Buffers;
using Buffers.Binary;

[StructLayout(LayoutKind.Auto)]
internal readonly struct FragmentHeader(ulong id, FragmentControl control, ushort length) : IBinaryFormattable<FragmentHeader>
{
    public const int Size = sizeof(long) + sizeof(FragmentControl) + sizeof(ushort);
    
    public ulong Id => id;
    public FragmentControl Control => control;
    public ushort Length => length;

    static int IBinaryFormattable<FragmentHeader>.Size => Size;
    
    public void Format(Span<byte> destination)
    {
        var writer = new SpanWriter<byte>(destination);
        writer.WriteLittleEndian(id);
        writer.WriteLittleEndian((ushort)control);
        writer.WriteLittleEndian(length);
    }

    public static FragmentHeader Parse(ReadOnlySpan<byte> source)
    {
        var reader = new SpanReader<byte>(source);
        return new(
            reader.ReadLittleEndian<ulong>(),
            (FragmentControl)reader.ReadLittleEndian<ushort>(),
            reader.ReadLittleEndian<ushort>());
    }
}