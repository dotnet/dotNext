using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
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

            sender = new(ref reader);
            term = reader.ReadInt64(true);
            lastLogIndex = reader.ReadInt64(true);
            lastLogTerm = reader.ReadInt64(true);
        }

        private int CreateOutboundMessage(Span<byte> payload)
        {
            var writer = new SpanWriter<byte>(payload);

            sender.WriteTo(ref writer);
            writer.WriteInt64(currentTerm, true);
            writer.WriteInt64(lastLogIndex, true);
            writer.WriteInt64(lastLogTerm, true);

            return writer.WrittenCount;
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
            => new((new PacketHeaders(MessageType.Vote, FlowControl.None), CreateOutboundMessage(payload.Span), true));
    }
}