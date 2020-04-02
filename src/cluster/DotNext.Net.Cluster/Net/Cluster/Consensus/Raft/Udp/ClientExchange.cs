using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal abstract class ClientExchange<T> : TaskCompletionSource<T>, IExchange
    {
        private protected ClientExchange()
            : base(TaskCreationOptions.RunContinuationsAsynchronously)
        {
        }

        public abstract ValueTask<bool> ProcessInbountMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endpoint, CancellationToken token);

        public abstract ValueTask<(PacketHeaders Headers, int BytesWritten, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token);

        private protected virtual void OnException(Exception e) { }

        void IExchange.OnException(Exception e)
        {
            if(e is OperationCanceledException cancellation ? TrySetCanceled(cancellation.CancellationToken) : TrySetException(e))
                OnException(e);
        }

        private protected virtual void OnCanceled(CancellationToken token) { }

        void IExchange.OnCanceled(CancellationToken token)
        {
            if(TrySetCanceled(token))
                OnCanceled(token);
        }
    }

    internal abstract class ClientExchange : ClientExchange<Result<bool>>
    {
        private protected readonly long CurrentTerm;

        private protected ClientExchange(long term) => CurrentTerm = term;

        public sealed override ValueTask<bool> ProcessInbountMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint sender, CancellationToken token)
        {
            Debug.Assert(headers.Control == FlowControl.Ack);
            TrySetResult(IExchange.ReadResult(payload.Span));
            return new ValueTask<bool>(false);
        }
    }
}