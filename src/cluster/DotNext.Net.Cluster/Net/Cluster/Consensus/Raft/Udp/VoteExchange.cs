using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal sealed class VoteExchange : ClientExchange
    {
        internal readonly long LastLogIndex;
        internal readonly long LastLogTerm;

        internal VoteExchange(long term, long lastLogIndex, long lastLogTerm)
            : base(term)
        {
            LastLogIndex = lastLogIndex;
            LastLogTerm = lastLogTerm;
        } 

        internal static void Parse(ReadOnlySpan<byte> payload, out long term, out long lastLogIndex, out long lastLogTerm)
        {
            term = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            lastLogIndex = ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            lastLogTerm = ReadInt64LittleEndian(payload);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        {
            const int payloadSize = sizeof(long) + sizeof(long) + sizeof(long);

            WriteInt64LittleEndian(payload.Span, CurrentTerm);
            payload = payload.Slice(sizeof(long));
            
            WriteInt64LittleEndian(payload.Span, LastLogIndex);
            payload = payload.Slice(sizeof(long));

            WriteInt64LittleEndian(payload.Span, LastLogTerm);

            return new ValueTask<(PacketHeaders, int, bool)>((new PacketHeaders(MessageType.Vote, FlowControl.None), payloadSize, true));
        }
    }
}