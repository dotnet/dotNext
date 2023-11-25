namespace DotNext.Net.Cluster.Consensus.Raft.Tcp;

using System.Threading;
using System.Threading.Tasks;
using Buffers;
using static IO.StreamExtensions;
using ProtocolStream = TransportServices.ConnectionOriented.ProtocolStream;

internal sealed class TcpProtocolStream : ProtocolStream
{
    internal readonly Stream BaseStream;

    internal TcpProtocolStream(Stream transport, MemoryAllocator<byte> allocator, int transmissionBlockSize)
        : base(allocator, transmissionBlockSize)
    {
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

    private protected override int ReadFromTransport(Span<byte> buffer) => BaseStream.Read(buffer);

    private protected override int ReadFromTransport(int count, Span<byte> buffer) => BaseStream.ReadAtLeast(buffer, count);

    private protected override ValueTask<int> ReadFromTransportAsync(Memory<byte> buffer, CancellationToken token)
        => BaseStream.ReadAsync(buffer, token);

    private protected override ValueTask<int> ReadFromTransportAsync(int minimumSize, Memory<byte> buffer, CancellationToken token)
        => BaseStream.ReadAtLeastAsync(buffer, minimumSize, cancellationToken: token);

    private protected override void WriteToTransport(ReadOnlySpan<byte> buffer)
        => BaseStream.Write(buffer);

    private protected override ValueTask WriteToTransportAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
        => BaseStream.WriteAsync(buffer, token);
}