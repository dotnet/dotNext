using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.CustomTransport;

using Buffers;
using ProtocolStream = TransportServices.ConnectionOriented.ProtocolStream;
using static IO.Pipelines.ResultExtensions;

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

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private static async ValueTask<int> ReadAsync(PipeReader reader, Memory<byte> destination, CancellationToken token)
    {
        var result = await reader.ReadAsync(token).ConfigureAwait(false);
        var readCount = result.Buffer.CopyTo(destination.Span, out SequencePosition position);
        reader.AdvanceTo(position);
        if (result.IsCanceled)
            throw new OperationCanceledException(token.IsCancellationRequested ? token : new(true));

        return readCount;
    }

    private protected override ValueTask<int> ReadFromTransportAsync(Memory<byte> destination, CancellationToken token)
        => ReadAsync(pipe.Input, destination, token);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private static async ValueTask<int> ReadAsync(PipeReader reader, int minimumSize, Memory<byte> destination, CancellationToken token)
    {
        var result = await reader.ReadAtLeastAsync(minimumSize, token).ConfigureAwait(false);
        int readCount;
        var position = result.Buffer.Start;
        try
        {
            readCount = result.Buffer.CopyTo(destination.Span, out position);
            if (minimumSize > readCount)
                throw new EndOfStreamException();
        }
        finally
        {
            reader.AdvanceTo(position);
        }

        if (result.IsCanceled)
            throw new OperationCanceledException(token.IsCancellationRequested ? token : new(true));

        return readCount;
    }

    private protected override ValueTask<int> ReadFromTransportAsync(int minimumSize, Memory<byte> destination, CancellationToken token)
        => ReadAsync(pipe.Input, minimumSize, destination, token);
}