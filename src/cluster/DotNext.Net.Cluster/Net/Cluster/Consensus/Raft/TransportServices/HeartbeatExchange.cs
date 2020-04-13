using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
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

        internal static void Parse(ReadOnlySpan<byte> payload, out ushort remotePort, out long term, out long prevLogIndex, out long prevLogTerm, out long commitIndex)
        {
            remotePort = ReadUInt16LittleEndian(payload);
            payload = payload.Slice(sizeof(ushort));

            term = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            prevLogIndex = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            prevLogTerm = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            commitIndex = ReadInt64LittleEndian(payload);
        }

        private int CreateOutboundMessage(Span<byte> payload)
        {
            WriteUInt16LittleEndian(payload, myPort);
            payload = payload.Slice(sizeof(ushort));

            WriteInt64LittleEndian(payload, currentTerm);
            payload = payload.Slice(sizeof(long));

            WriteInt64LittleEndian(payload, prevLogIndex);
            payload = payload.Slice(sizeof(long));

            WriteInt64LittleEndian(payload, prevLogTerm);
            payload = payload.Slice(sizeof(long));

            WriteInt64LittleEndian(payload, commitIndex);

            return sizeof(ushort) + sizeof(long) + sizeof(long) + sizeof(long) + sizeof(long);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
            => new ValueTask<(PacketHeaders, int, bool)>((new PacketHeaders(MessageType.Heartbeat, FlowControl.None), CreateOutboundMessage(payload.Span), true));
    }
}