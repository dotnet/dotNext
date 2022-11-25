namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using static Runtime.Intrinsics;

internal partial class ServerExchange
{
    private void BeginResign(CancellationToken token)
        => task = server.ResignAsync(token).AsTask();

    private async ValueTask<(PacketHeaders, int, bool)> EndResign(Memory<byte> payload)
    {
        var result = await Cast<Task<bool>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
        task = null;
        payload.Span[0] = result.ToByte();
        return (new PacketHeaders(MessageType.Resign, FlowControl.Ack), 1, false);
    }
}