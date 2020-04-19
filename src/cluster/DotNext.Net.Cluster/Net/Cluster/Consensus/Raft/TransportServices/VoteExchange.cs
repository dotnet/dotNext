using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    internal sealed class VoteExchange : ClientExchange
    {
        private readonly long lastLogIndex, lastLogTerm;

        internal VoteExchange(long term, long lastLogIndex, long lastLogTerm)
            : base(term)
        {
            this.lastLogIndex = lastLogIndex;
            this.lastLogTerm = lastLogTerm;
        }

        internal static void Parse(ReadOnlySpan<byte> payload, out ushort remotePort, out long term, out long lastLogIndex, out long lastLogTerm)
        {
            remotePort = ReadUInt16LittleEndian(payload);
            payload = payload.Slice(sizeof(ushort));

            term = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            lastLogIndex = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            lastLogTerm = ReadInt64LittleEndian(payload);
        }

        private int CreateOutboundMessage(Span<byte> payload)
        {
            WriteUInt16LittleEndian(payload, myPort);
            payload = payload.Slice(sizeof(ushort));

            WriteInt64LittleEndian(payload, currentTerm);
            payload = payload.Slice(sizeof(long));

            WriteInt64LittleEndian(payload, lastLogIndex);
            payload = payload.Slice(sizeof(long));

            WriteInt64LittleEndian(payload, lastLogTerm);

            return sizeof(ushort) + sizeof(long) + sizeof(long) + sizeof(long);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
            => new ValueTask<(PacketHeaders, int, bool)>((new PacketHeaders(MessageType.Vote, FlowControl.None), CreateOutboundMessage(payload.Span), true));
    }
}