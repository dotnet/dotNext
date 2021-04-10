using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Buffers;

    internal sealed class HeartbeatExchange : ClientExchange
    {
        private readonly long prevLogIndex, prevLogTerm, commitIndex;

        internal HeartbeatExchange(long term, long prevLogIndex, long prevLogTerm, long commitIndex)
            : base(term)
        {
            this.prevLogIndex = prevLogIndex;
            this.prevLogTerm = prevLogTerm;
            this.commitIndex = commitIndex;
        }

        internal static void Parse(ReadOnlySpan<byte> payload, out ushort remotePort, out long term, out long prevLogIndex, out long prevLogTerm, out long commitIndex)
        {
            var reader = new SpanReader<byte>(payload);

            remotePort = reader.ReadUInt16(true);
            term = reader.ReadInt64(true);
            prevLogIndex = reader.ReadInt64(true);
            prevLogTerm = reader.ReadInt64(true);
            commitIndex = reader.ReadInt64(true);
        }

        private int CreateOutboundMessage(Span<byte> payload)
        {
            var writer = new SpanWriter<byte>(payload);

            writer.WriteUInt16(myPort, true);
            writer.WriteInt64(currentTerm, true);
            writer.WriteInt64(prevLogIndex, true);
            writer.WriteInt64(prevLogTerm, true);
            writer.WriteInt64(commitIndex, true);

            return writer.WrittenCount;
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
            => new ((new PacketHeaders(MessageType.Heartbeat, FlowControl.None), CreateOutboundMessage(payload.Span), true));
    }
}