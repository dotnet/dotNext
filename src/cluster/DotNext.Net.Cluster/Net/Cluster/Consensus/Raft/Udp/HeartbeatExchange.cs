using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
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

        internal static void Parse(ReadOnlySpan<byte> payload, out long term, out long prevLogIndex, out long prevLogTerm, out long commitIndex)
        {
            term = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            prevLogIndex = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            prevLogTerm = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            commitIndex = ReadInt64LittleEndian(payload);
        }

        private void CreateOutboundMessage(Span<byte> payload)
        {
            WriteInt64LittleEndian(payload, currentTerm);
            payload = payload.Slice(sizeof(long));
            
            WriteInt64LittleEndian(payload, prevLogIndex);
            payload = payload.Slice(sizeof(long));

            WriteInt64LittleEndian(payload, prevLogTerm);
            payload = payload.Slice(sizeof(long));

            WriteInt64LittleEndian(payload, commitIndex);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        {
            const int payloadSize = sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long);

            CreateOutboundMessage(payload.Span);

            return new ValueTask<(PacketHeaders, int, bool)>((new PacketHeaders(MessageType.Heartbeat, FlowControl.None), payloadSize, true));
        }
    }
}