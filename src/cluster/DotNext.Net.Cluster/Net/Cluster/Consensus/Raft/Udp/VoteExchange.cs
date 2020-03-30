using System;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal sealed class VoteExchange : SimpleExchange
    {
        internal readonly long LastLogIndex;
        internal readonly long LastLogTerm;

        internal VoteExchange(long term, long lastLogIndex, long lastLogTerm)
            : base(term)
        {
            LastLogIndex = lastLogIndex;
            LastLogTerm = lastLogTerm;
        } 

        internal static void Parse(ReadOnlySpan<byte> payload, out long lastLogIndex, out long lastLogTerm)
        {
            lastLogIndex = BinaryPrimitives.ReadInt64LittleEndian(payload);
            payload = payload.Slice(sizeof(long));

            lastLogTerm = BinaryPrimitives.ReadInt64LittleEndian(payload);
        }

        public override ValueTask<(PacketHeaders Headers, int BytesWritten, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        {
            const int payloadSize = sizeof(long) + sizeof(long);
            
            BinaryPrimitives.WriteInt64LittleEndian(payload.Span, LastLogIndex);
            payload = payload.Slice(sizeof(long));

            BinaryPrimitives.WriteInt64LittleEndian(payload.Span, LastLogTerm);

            return new ValueTask<(PacketHeaders Headers, int BytesWritten, bool)>((new PacketHeaders(MessageType.Vote, FlowControl.None, CurrentTerm), payloadSize, true));
        }
    }
}