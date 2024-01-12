using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.CustomTransport;

using Buffers;
using IO.Pipelines;
using ProtocolStream = TransportServices.ConnectionOriented.ProtocolStream;

internal sealed class ProtocolPipeStream : ProtocolStream
{
    private readonly IDuplexPipe pipe;

    internal ProtocolPipeStream(IDuplexPipe pipe, MemoryAllocator<byte> allocator, int transmissionBlockSize)
        : base(allocator, transmissionBlockSize)
        => this.pipe = pipe;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override bool CanRead => true;

    public override bool CanTimeout => true;

    public override int ReadTimeout
    {
        get;
        set;
    }

    public override int WriteTimeout
    {
        get;
        set;
    }

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Length => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private protected override async ValueTask WriteToTransportAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
        => (await pipe.Output.WriteAsync(buffer, token).ConfigureAwait(false)).ThrowIfCancellationRequested(token);

    private protected override ValueTask<int> ReadFromTransportAsync(Memory<byte> destination, CancellationToken token)
        => pipe.Input.ReadAsync(destination, token);

    private protected override ValueTask<int> ReadFromTransportAsync(Memory<byte> destination, int minimumSize, CancellationToken token)
        => pipe.Input.ReadAtLeastAsync(destination, minimumSize, token);
}