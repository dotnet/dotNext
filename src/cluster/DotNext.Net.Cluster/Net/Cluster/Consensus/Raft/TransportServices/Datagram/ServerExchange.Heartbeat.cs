using System.Diagnostics;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using IO.Log;
using static Runtime.Intrinsics;

internal partial class ServerExchange
{
    private static readonly ILogEntryProducer<ReceivedLogEntry> EmptyProducer = new LogEntryProducer<ReceivedLogEntry>();

    private void BeginProcessHeartbeat(ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        HeartbeatExchange.Parse(payload.Span, out var sender, out var term, out var prevLogIndex, out var prevLogTerm, out var commitIndex, out var configState);
        task = server.AppendEntriesAsync(sender, term, EmptyProducer, prevLogIndex, prevLogTerm, commitIndex, configState?.Fingerprint, configState is { ApplyConfig: true }, token).AsTask();
    }

    private async ValueTask<(PacketHeaders, int, bool)> EndProcessHearbeat(Memory<byte> output)
    {
        Debug.Assert(task is Task<Result<bool?>>);

        var result = await Cast<Task<Result<bool?>>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
        return (new PacketHeaders(MessageType.Heartbeat, FlowControl.Ack), Result.Write(output.Span, result), false);
    }
}