using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal sealed class ServerExchange : PipeExchange
    {
        private enum State : byte
        {
            Ready = 0,
            VoteRequestProcessing
        }

        private readonly IRaftRpcServer server;
        private Task? task;
        private State state;

        internal ServerExchange(IRaftRpcServer server, PipeOptions? options = null)
            : base(options)
            => this.server = server;
        
        private void BeginVote(long term, ReadOnlyMemory<byte> payload, CancellationToken token)
        {
            VoteExchange.Parse(payload.Span, out var lastLogIndex, out var lastLogTerm);
            task = server.VoteAsync(term, lastLogIndex, lastLogTerm, token);
        }
        
        public override ValueTask<bool> ProcessInbountMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endpoint, CancellationToken token)
        {
            switch(headers.Type)
            {
                case MessageType.Vote:
                    state = State.VoteRequestProcessing;
                    BeginVote(headers.Term, payload, token);
                    return new ValueTask<bool>(true);
                default:
                    return new ValueTask<bool>(false);
            }
        }

        private async ValueTask<(PacketHeaders, int, bool)> EndVote(Memory<byte> payload)
        {
            Debug.Assert(task is Task<Result<bool>>);
            var result = await ((Task<Result<bool>>)task).ConfigureAwait(false);
            task = null;
            payload.Span[0] = (byte)result.Value.ToInt32();
            return (new PacketHeaders(MessageType.Vote, FlowControl.Ack, result.Term), 1, false);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
            => state switch
            {
                State.VoteRequestProcessing => EndVote(payload),
                _ => default
            };

        internal void Reset()
        {
            ReusePipe();
            task = null;
            state = State.Ready;
        }
    }
}