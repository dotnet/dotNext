using System;
using System.Net;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal abstract class ClientExchange : PipeExchange
    {
        private protected readonly long CurrentTerm;

        private protected ClientExchange(long term, PipeOptions? options = null)
            : base(options)
            => CurrentTerm = term;
    }

    internal abstract class ClientExchange<T> : TaskCompletionSource<T>, IExchange
    {
        private protected readonly long CurrentTerm;

        private protected ClientExchange(long term)
            : base(TaskCreationOptions.RunContinuationsAsynchronously)
            => CurrentTerm = term;

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
}