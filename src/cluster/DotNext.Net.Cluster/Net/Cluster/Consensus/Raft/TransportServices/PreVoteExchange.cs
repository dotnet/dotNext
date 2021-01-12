using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Buffers;

    internal sealed class PreVoteExchange : ClientExchange
    {
        private readonly long lastLogIndex, lastLogTerm;

        internal PreVoteExchange(long term, long lastLogIndex, long lastLogTerm)
            : base(term)
        {
            this.lastLogIndex = lastLogIndex;
            this.lastLogTerm = lastLogTerm;
        }

        internal static void Parse(ReadOnlySpan<byte> payload, out ushort remotePort, out long term, out long lastLogIndex, out long lastLogTerm)
        {
            var reader = new SpanReader<byte>(payload);

            remotePort = reader.ReadUInt16(true);
            term = reader.ReadInt64(true);
            lastLogIndex = reader.ReadInt64(true);
            lastLogTerm = reader.ReadInt64(true);
        }

        private int CreateOutboundMessage(Span<byte> payload)
        {
            var writer = new SpanWriter<byte>(payload);

            writer.WriteUInt16(myPort, true);
            writer.WriteInt64(currentTerm, true);
            writer.WriteInt64(lastLogIndex, true);
            writer.WriteInt64(lastLogTerm, true);

            return writer.WrittenCount;
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
            => new ValueTask<(PacketHeaders, int, bool)>((new PacketHeaders(MessageType.PreVote, FlowControl.None), CreateOutboundMessage(payload.Span), true));
    }
}