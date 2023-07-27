namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using Buffers;

internal sealed class HeartbeatExchange : ClientExchange<Result<HeartbeatResult>>
{
    private const string Name = "Heartbeat";
    private readonly long prevLogIndex, prevLogTerm, commitIndex, currentTerm;
    private readonly EmptyClusterConfiguration? configuration;

    internal HeartbeatExchange(long term, long prevLogIndex, long prevLogTerm, long commitIndex, EmptyClusterConfiguration? configState)
        : base(Name)
    {
        this.prevLogIndex = prevLogIndex;
        this.prevLogTerm = prevLogTerm;
        this.commitIndex = commitIndex;
        currentTerm = term;
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

    public override ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        TrySetResult(Result.ReadHeartbeatResult(payload.Span));
        return new(false);
    }

    public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        => new((new PacketHeaders(MessageType.Heartbeat, FlowControl.None), CreateOutboundMessage(payload.Span), true));
}