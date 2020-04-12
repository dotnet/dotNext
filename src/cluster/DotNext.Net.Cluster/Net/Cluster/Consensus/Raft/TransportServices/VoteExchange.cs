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

        internal static void Parse(ReadOnlySpan<byte> payload, out long term, out long lastLogIndex, out long lastLogTerm)
        {
            term = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            lastLogIndex = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            lastLogTerm = ReadInt64LittleEndian(payload);
        }

        private void CreateOutboundMessage(Span<byte> payload)
        {
            WriteInt64LittleEndian(payload, currentTerm);
            payload = payload.Slice(sizeof(long));
            
            WriteInt64LittleEndian(payload, lastLogIndex);
            payload = payload.Slice(sizeof(long));

            WriteInt64LittleEndian(payload, lastLogTerm);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        {
            const int payloadSize = sizeof(long) + sizeof(long) + sizeof(long);

            CreateOutboundMessage(payload.Span);

            return new ValueTask<(PacketHeaders, int, bool)>((new PacketHeaders(MessageType.Vote, FlowControl.None), payloadSize, true));
        }
    }
}