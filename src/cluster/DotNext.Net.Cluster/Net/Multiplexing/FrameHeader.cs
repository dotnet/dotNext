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
internal readonly struct FrameHeader(uint id, FrameControl control, ushort length) : IBinaryFormattable<FrameHeader>
{
    private const byte CurrentVersion = 0;
    
    /// <summary>
    /// All protocol-specific fragments have ID = 0.
    /// </summary>
    public const uint SystemStreamId = 0U;

    // Version(1 byte) StreamId(4 bytes) FrameControl(1 byte) PayloadLength(2 bytes)
    public const int Size = sizeof(byte) + sizeof(uint) + sizeof(FrameControl) + sizeof(ushort);
    
    public uint Id => id; // if 0, then the packet is protocol-specific
    public FrameControl Control => control;
    public ushort Length => length;

    public bool CanBeIgnored => control is FrameControl.StreamClosed or FrameControl.StreamRejected or FrameControl.AdjustWindow;

    static int IBinaryFormattable<FrameHeader>.Size => Size;
    
    public void Format(Span<byte> destination)
    {
        var writer = new SpanWriter<byte>(destination);
        writer.Add(CurrentVersion);
        writer.WriteLittleEndian(id);
        writer.Add((byte)control);
        writer.WriteLittleEndian(length);
    }

    public static FrameHeader Parse(ReadOnlySpan<byte> source)
    {
        var reader = new SpanReader<byte>(source);
        var version = reader.Read();

        if (version > CurrentVersion)
            throw new UnsupportedVersionException(version);

        return new(
            reader.ReadLittleEndian<uint>(),
            (FrameControl)reader.Read(),
            reader.ReadLittleEndian<ushort>());
    }
}