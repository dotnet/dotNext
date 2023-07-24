using System.Diagnostics;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using static Runtime.Intrinsics;

internal partial class ServerExchange
{
    private async ValueTask<bool> BeginReceiveSnapshot(ReadOnlyMemory<byte> input, bool completed, CancellationToken token)
    {
        var snapshot = new ReceivedLogEntry(ref input, Reader, out var sender, out var senderTerm, out var snapshotIndex);
        var result = await Writer.WriteAsync(input, token).ConfigureAwait(false);
        task = server.InstallSnapshotAsync(sender, senderTerm, snapshot, snapshotIndex, token).AsTask();
        if (result.IsCompleted | completed)
        {
            await Writer.CompleteAsync().ConfigureAwait(false);
            state = State.ReceivingSnapshotFinished;
        }

        return true;
    }

    private async ValueTask<bool> ReceivingSnapshot(ReadOnlyMemory<byte> content, bool completed, CancellationToken token)
    {
        if (content.IsEmpty)
        {
            completed = true;
        }
        else
        {
            var result = await Writer.WriteAsync(content, token).ConfigureAwait(false);
            completed |= result.IsCompleted;
        }

        if (completed)
        {
            await Writer.CompleteAsync().ConfigureAwait(false);
            state = State.ReceivingSnapshotFinished;
        }

        return true;
    }

    private static ValueTask<(PacketHeaders, int, bool)> RequestSnapshotChunk()
        => new((new PacketHeaders(MessageType.Continue, FlowControl.Ack), 0, true));

    private async ValueTask<(PacketHeaders, int, bool)> EndReceiveSnapshot(Memory<byte> output)
    {
        Debug.Assert(task is Task<Result<bool?>>);

        var result = await Cast<Task<Result<bool?>>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
        return (new PacketHeaders(MessageType.None, FlowControl.Ack), Result.Write(output.Span, result), false);
    }
}