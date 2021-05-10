using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    internal sealed class ResignExchange : ClientExchange<bool>
    {
        public override ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endpoint, CancellationToken token)
        {
            Debug.Assert(headers.Control == FlowControl.Ack);
            TrySetResult(ValueTypeExtensions.ToBoolean(payload.Span[0]));
            return new(false);
        }

        public override ValueTask<(PacketHeaders Headers, int BytesWritten, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
            => new((new PacketHeaders(MessageType.Resign, FlowControl.None), 0, true));
    }
}