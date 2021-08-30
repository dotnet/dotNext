using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
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
            term = reader.ReadInt64(true);
            prevLogIndex = reader.ReadInt64(true);
            prevLogTerm = reader.ReadInt64(true);
            commitIndex = reader.ReadInt64(true);
            configuration = EmptyClusterConfiguration.ReadFrom(ref reader);
        }

        private int CreateOutboundMessage(Span<byte> payload)
        {
            var writer = new SpanWriter<byte>(payload);

            sender.WriteTo(ref writer);
            writer.WriteInt64(currentTerm, true);
            writer.WriteInt64(prevLogIndex, true);
            writer.WriteInt64(prevLogTerm, true);
            writer.WriteInt64(commitIndex, true);
            EmptyClusterConfiguration.WriteTo(in configuration, ref writer);

            return writer.WrittenCount;
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
            => new((new PacketHeaders(MessageType.Heartbeat, FlowControl.None), CreateOutboundMessage(payload.Span), true));
    }
}