using System.Diagnostics.CodeAnalysis;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;

/// <summary>
/// Provides encoding/decoding routines for transmitting Raft-specific
/// RPC calls over stream-oriented network transports.
/// </summary>
internal sealed partial class ProtocolStream : Stream
{
    private const int FrameHeadersSize = sizeof(int) + sizeof(byte);

    private static int AppendEntriesHeadersSize => AppendEntriesMessage.Size + sizeof(byte) + sizeof(long) + sizeof(long);

    [SuppressMessage("Usage", "CA2213", Justification = "The objec doesn't own the stream")]
    internal readonly Stream BaseStream;
    private MemoryOwner<byte> buffer;

    // for reader, both fields are in use
    // for writer, bufferStart is a beginning of the frame
    private int bufferStart, bufferEnd;

    internal ProtocolStream(Stream transport, MemoryAllocator<byte> allocator, int transmissionBlockSize)
    {
        Debug.Assert(transport is not null);

        buffer = allocator.Invoke(transmissionBlockSize, exactSize: false);
        BaseStream = transport;
    }

    public override bool CanRead => BaseStream.CanRead;

    public override bool CanWrite => BaseStream.CanWrite;

    public override bool CanSeek => BaseStream.CanSeek;

    public override bool CanTimeout => BaseStream.CanTimeout;

    public override long Position
    {
        get => BaseStream.Position;
        set => BaseStream.Position = value;
    }

    public override void SetLength(long value) => BaseStream.SetLength(value);

    public override long Seek(long offset, SeekOrigin origin) => BaseStream.Seek(offset, origin);

    public override long Length => BaseStream.Length;

    internal void Reset()
    {
        bufferStart = bufferEnd = frameSize = 0;
        readState = ReadState.FrameNotStarted;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            buffer.Dispose();
        }

        base.Dispose(disposing);
    }
}