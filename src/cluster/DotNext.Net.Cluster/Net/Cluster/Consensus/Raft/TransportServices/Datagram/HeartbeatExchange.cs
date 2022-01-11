namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal sealed class HeartbeatExchange : ClientExchange
{
    private readonly long prevLogIndex, prevLogTerm, commitIndex;
    private readonly EmptyClusterConfiguration? configuration;

    internal HeartbeatExchange(long term, long prevLogIndex, long prevLogTerm, long commitIndex, EmptyClusterConfiguration? configState)
        : base(term)
    {
        this.prevLogIndex = prevLogIndex;
        this.prevLogTerm = prevLogTerm;
        this.commitIndex = commitIndex;
        configuration = configState;
    }

    internal static void Parse(ReadOnlySpan<byte> payload, out ClusterMemberId sender, out long term, out long prevLogIndex, out long prevLogTerm, out long commitIndex, out EmptyClusterConfiguration? configuration)
    {
        var reader = new SpanReader<byte>(payload);

        sender = new(ref reader);
        (term, prevLogIndex, prevLogTerm, commitIndex, configuration) = HeartbeatMessage.Read(ref reader);
    }

    private int CreateOutboundMessage(Span<byte> payload)
    {
        var writer = new SpanWriter<byte>(payload);

        sender.Format(ref writer);
        HeartbeatMessage.Write(ref writer, currentTerm, prevLogIndex, prevLogTerm, commitIndex, in configuration);

        return writer.WrittenCount;
    }

    public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        => new((new PacketHeaders(MessageType.Heartbeat, FlowControl.None), CreateOutboundMessage(payload.Span), true));
}