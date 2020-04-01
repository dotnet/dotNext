using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal abstract class SimpleExchange : ClientExchange<Result<bool>>
    {
        private protected SimpleExchange(long term)
            : base(term)
        {
        }

        public sealed override ValueTask<bool> ProcessInbountMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint sender, CancellationToken token)
        {
            Debug.Assert(headers.Type == MessageType.Vote);
            Debug.Assert(headers.Control == FlowControl.Ack);
            TrySetResult(new Result<bool>(headers.Term, ValueTypeExtensions.ToBoolean(payload.Span[0])));
            return new ValueTask<bool>(false);
        }
    }
}