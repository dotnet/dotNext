namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using static Runtime.Intrinsics;

internal partial class ServerExchange
{
    private void BeginPreVote(ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        PreVoteExchange.Parse(payload.Span, out var sender, out var term, out var lastLogIndex, out var lastLogTerm);
        task = server.PreVoteAsync(sender, term, lastLogIndex, lastLogTerm, token).AsTask();
    }

    private async ValueTask<(PacketHeaders, int, bool)> EndPreVote(Memory<byte> payload)
    {
        var result = await Cast<Task<Result<PreVoteResult>>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
        return (new PacketHeaders(MessageType.PreVote, FlowControl.Ack), Result.WritePreVoteResult(payload.Span, result), false);
    }
}