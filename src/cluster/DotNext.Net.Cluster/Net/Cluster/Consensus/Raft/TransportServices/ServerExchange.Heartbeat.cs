namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using IO.Log;
using static Runtime.Intrinsics;

internal partial class ServerExchange
{
    private static readonly ILogEntryProducer<ReceivedLogEntry> EmptyProducer = new LogEntryProducer<ReceivedLogEntry>();

    private void BeginProcessHeartbeat(ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        HeartbeatExchange.Parse(payload.Span, out var sender, out var term, out var prevLogIndex, out var prevLogTerm, out var commitIndex, out var configState);
        task = server.AppendEntriesAsync(sender, term, EmptyProducer, prevLogIndex, prevLogTerm, commitIndex, configState?.Fingerprint, configState is { ApplyConfig: true }, token);
    }

    private async ValueTask<(PacketHeaders, int, bool)> EndProcessHearbeat(Memory<byte> output)
    {
        var result = await Cast<Task<Result<bool>>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
        return (new PacketHeaders(MessageType.Heartbeat, FlowControl.Ack), IExchange.WriteResult(result, output.Span), false);
    }
}