namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using Buffers;

internal sealed class VoteExchange : ClientExchange
{
    private readonly long lastLogIndex, lastLogTerm;

    internal VoteExchange(long term, long lastLogIndex, long lastLogTerm)
        : base(term)
    {
        this.lastLogIndex = lastLogIndex;
        this.lastLogTerm = lastLogTerm;
    }

    internal static void Parse(ReadOnlySpan<byte> payload, out ClusterMemberId sender, out long term, out long lastLogIndex, out long lastLogTerm)
    {
        var reader = new SpanReader<byte>(payload);
        (sender, term, lastLogIndex, lastLogTerm) = VoteMessage.Read(ref reader);
    }

    private int CreateOutboundMessage(Span<byte> payload)
    {
        var writer = new SpanWriter<byte>(payload);
        VoteMessage.Write(ref writer, in sender, currentTerm, lastLogIndex, lastLogTerm);
        return writer.WrittenCount;
    }

    public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        => new((new PacketHeaders(MessageType.Vote, FlowControl.None), CreateOutboundMessage(payload.Span), true));
}