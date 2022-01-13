using System.IO.Pipelines;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using IO;
using static IO.Pipelines.PipeExtensions;

internal sealed class SnapshotExchange : ClientExchange<Result<bool>>, IAsyncDisposable
{
    private readonly Pipe pipe;
    private readonly long term, snapshotIndex;
    private readonly IRaftLogEntry snapshot;
    private Task? transmission;

    internal SnapshotExchange(long term, IRaftLogEntry snapshot, long snapshotIndex, PipeOptions? options = null)
    {
        this.term = term;
        this.snapshotIndex = snapshotIndex;
        this.snapshot = snapshot;
        pipe = new Pipe(options ?? PipeOptions.Default);
    }

    internal static int ParseAnnouncement(ReadOnlySpan<byte> input, out ClusterMemberId sender, out long term, out long snapshotIndex, out LogEntryMetadata metadata)
    {
        var reader = new SpanReader<byte>(input);
        (sender, term, snapshotIndex, metadata) = SnapshotMessage.Read(ref reader);
        return reader.ConsumedCount;
    }

    private int WriteAnnouncement(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        SnapshotMessage.Write(ref writer, in sender, term, snapshotIndex, snapshot);
        return writer.WrittenCount;
    }

    private static async Task WriteSnapshotAsync(IDataTransferObject snapshot, PipeWriter writer, CancellationToken token)
    {
        await snapshot.WriteToAsync(writer, token).ConfigureAwait(false);
        await writer.CompleteAsync().ConfigureAwait(false);
    }

    public override async ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
    {
        var count = default(int);
        FlowControl control;
        if (transmission is null)
        {
            count = WriteAnnouncement(payload.Span);
            payload = payload.Slice(count);
            control = FlowControl.StreamStart;
            transmission = WriteSnapshotAsync(snapshot, pipe.Writer, token);
        }
        else
        {
            control = FlowControl.Fragment;
        }

        count += await pipe.Reader.CopyToAsync(payload, token).ConfigureAwait(false);
        if (count < payload.Length)
            control = FlowControl.StreamEnd;
        return (new PacketHeaders(MessageType.InstallSnapshot, control), count, true);
    }

    public override ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        ValueTask<bool> result;
        if (transmission is { IsFaulted: true })
        {
            result = ValueTask.FromException<bool>(transmission.Exception!);
        }
        else if (headers.Type is MessageType.Continue)
        {
            result = new(true);
        }
        else
        {
            TrySetResult(Result.Read(payload.Span));
            result = new(false);
        }

        return result;
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        var e = new ObjectDisposedException(GetType().Name);
        await pipe.Writer.CompleteAsync(e).ConfigureAwait(false);
        await pipe.Reader.CompleteAsync(e).ConfigureAwait(false);
    }
}