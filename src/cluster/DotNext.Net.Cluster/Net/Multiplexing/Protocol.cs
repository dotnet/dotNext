using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Multiplexing;

internal static class Protocol
{
    private static void WriteEmptyFrame(IBufferWriter<byte> writer, [ConstantExpected] FrameControl control, ulong streamId)
    {
        var header = new FrameHeader(streamId, control, length: 0);
        header.Format(writer.GetSpan(FrameHeader.Size));
        writer.Advance(FrameHeader.Size);
    }

    public static void WriteHeartbeat(IBufferWriter<byte> writer)
        => WriteEmptyFrame(writer, FrameControl.Heartbeat, FrameHeader.SystemStreamId);

    public static void WriteStreamRejected(IBufferWriter<byte> writer, ulong streamId)
        => WriteEmptyFrame(writer, FrameControl.StreamRejected, streamId);

    public static void WriteStreamClosed(IBufferWriter<byte> writer, ulong streamId)
        => WriteEmptyFrame(writer, FrameControl.StreamClosed, streamId);

    public static void WriteAdjustWindow(IBufferWriter<byte> writer, ulong streamId, int windowSize)
    {
        const int messageSize = FrameHeader.Size + sizeof(int);

        var buffer = writer.GetSpan(messageSize);
        new FrameHeader(streamId, FrameControl.AdjustWindow, length: sizeof(int)).Format(buffer);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(FrameHeader.Size), windowSize);
        writer.Advance(messageSize);
    }

    public static int ReadAdjustWindow(ReadOnlySpan<byte> payload)
        => BinaryPrimitives.ReadInt32LittleEndian(payload);
}