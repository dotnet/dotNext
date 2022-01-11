using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

/// <summary>
/// Provides encoding/decoding routines for transmitting Raft-specific
/// RPC calls over stream-oriented network transports.
/// </summary>
internal sealed partial class ProtocolStream : Stream
{
    private const int FrameHeadersSize = sizeof(int) + sizeof(byte);
    private readonly Stream transport;
    private readonly Memory<byte> buffer;
    private int bufferStart, bufferEnd;

    internal ProtocolStream(Stream transport, Memory<byte> buffer)
    {
        Debug.Assert(transport is not null);

        this.buffer = buffer;
        this.transport = transport;
    }

    public override bool CanRead => transport.CanRead;

    public override bool CanWrite => transport.CanWrite;

    public override bool CanSeek => transport.CanSeek;

    public override bool CanTimeout => transport.CanTimeout;

    public override long Position
    {
        get => transport.Position;
        set => transport.Position = value;
    }

    public override void SetLength(long value) => transport.SetLength(value);

    public override long Seek(long offset, SeekOrigin origin) => transport.Seek(offset, origin);

    public override long Length => transport.Length;

    private int AvailableBytes => bufferEnd - bufferStart;

    internal void Reset()
    {
        bufferStart = bufferEnd = frameSize = 0;
        readState = ReadState.FrameNotStarted;
    }
}