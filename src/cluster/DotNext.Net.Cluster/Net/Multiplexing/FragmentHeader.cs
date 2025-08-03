using System.Runtime.InteropServices;

namespace DotNext.Net.Multiplexing;

using Buffers;
using Buffers.Binary;

/// <summary>
/// Represents fragment header.
/// </summary>
/// <param name="id">The identifier of the stream.</param>
/// <param name="control">The fragment behavior.</param>
/// <param name="length">The length of the fragment data.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly struct FragmentHeader(ulong id, FragmentControl control, ushort length) : IBinaryFormattable<FragmentHeader>
{
    /// <summary>
    /// All protocol-specific fragments have ID = 0.
    /// </summary>
    private const ulong SystemStreamId = 0U;
    
    public const int Size = sizeof(long) + sizeof(FragmentControl) + sizeof(ushort);
    
    public ulong Id => id; // if 0, then the packet is protocol-specific
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

    public static int WriteHeartbeat(Span<byte> buffer)
    {
        var header = new FragmentHeader(SystemStreamId, FragmentControl.Heartbeat, length: 0);
        header.Format(buffer);
        return Size;
    }
}