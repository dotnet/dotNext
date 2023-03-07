using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

using Buffers;

internal sealed class PreVoteExchange : ClientExchange<Result<PreVoteResult>>
{
    private const string Name = "PreVote";

    private readonly long lastLogIndex, lastLogTerm, currentTerm;

    internal PreVoteExchange(long term, long lastLogIndex, long lastLogTerm)
        : base(Name)
    {
        this.lastLogIndex = lastLogIndex;
        this.lastLogTerm = lastLogTerm;
        currentTerm = term;
    }

    internal static void Parse(ReadOnlySpan<byte> payload, out ClusterMemberId sender, out long term, out long lastLogIndex, out long lastLogTerm)
    {
        var reader = new SpanReader<byte>(payload);
        (sender, term, lastLogIndex, lastLogTerm) = PreVoteMessage.Read(ref reader);
    }

    private int CreateOutboundMessage(Span<byte> payload)
    {
        var writer = new SpanWriter<byte>(payload);
        PreVoteMessage.Write(ref writer, in sender, currentTerm, lastLogIndex, lastLogTerm);
        return writer.WrittenCount;
    }

    public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        => new((new PacketHeaders(MessageType.PreVote, FlowControl.None), CreateOutboundMessage(payload.Span), true));

    public override ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        Debug.Assert(headers.Control is FlowControl.Ack, "Unexpected response", $"Message type {headers.Type} control {headers.Control}");
        TrySetResult(Result.ReadPreVoteResult(payload.Span));
        return new(false);
    }
}