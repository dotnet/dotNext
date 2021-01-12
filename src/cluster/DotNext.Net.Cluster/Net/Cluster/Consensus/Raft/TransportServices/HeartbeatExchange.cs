using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

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

            remotePort = ReadUInt16LittleEndian(reader.Read(sizeof(ushort)));
            term = ReadInt64LittleEndian(reader.Read(sizeof(long)));
            prevLogIndex = ReadInt64LittleEndian(reader.Read(sizeof(long)));
            prevLogTerm = ReadInt64LittleEndian(reader.Read(sizeof(long)));
            commitIndex = ReadInt64LittleEndian(reader.Read(sizeof(long)));
        }

        private int CreateOutboundMessage(Span<byte> payload)
        {
            var writer = new SpanWriter<byte>(payload);

            WriteUInt16LittleEndian(writer.Slide(sizeof(ushort)), myPort);
            WriteInt64LittleEndian(writer.Slide(sizeof(long)), currentTerm);
            WriteInt64LittleEndian(writer.Slide(sizeof(long)), prevLogIndex);
            WriteInt64LittleEndian(writer.Slide(sizeof(long)), prevLogTerm);
            WriteInt64LittleEndian(writer.Slide(sizeof(long)), commitIndex);

            return writer.WrittenCount;
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
            => new ValueTask<(PacketHeaders, int, bool)>((new PacketHeaders(MessageType.Heartbeat, FlowControl.None), CreateOutboundMessage(payload.Span), true));
    }
}