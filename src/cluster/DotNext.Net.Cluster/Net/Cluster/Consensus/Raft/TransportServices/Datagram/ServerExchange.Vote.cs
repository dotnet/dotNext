namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using static Runtime.Intrinsics;

internal partial class ServerExchange
{
    private void BeginVote(ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        VoteExchange.Parse(payload.Span, out var sender, out var term, out var lastLogIndex, out var lastLogTerm);
        task = server.VoteAsync(sender, term, lastLogIndex, lastLogTerm, token).AsTask();
    }

    private async ValueTask<(PacketHeaders, int, bool)> EndVote(Memory<byte> payload)
    {
        var result = await Cast<Task<Result<bool>>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
        return (new PacketHeaders(MessageType.Vote, FlowControl.Ack), Result.Write(payload.Span, result), false);
    }
}