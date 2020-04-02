using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using static IO.Pipelines.PipeExtensions;

    internal sealed class ServerExchange : PipeExchange
    {
        private enum State : byte
        {
            Ready = 0,
            VoteRequestReceived,
            MetadataRequestReceived,
            SendingMetadata
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

        private void BeginSendMetadata(CancellationToken token)
        {
            task = MetadataExchange.WriteAsync(Writer, server.Metadata, token);
        }
        
        public override ValueTask<bool> ProcessInbountMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endpoint, CancellationToken token)
        {
            switch(headers.Type)
            {
                case MessageType.Vote:
                    state = State.VoteRequestReceived;
                    BeginVote(headers.Term, payload, token);
                    break;
                case MessageType.Metadata:
                    if(headers.Control == FlowControl.None)
                    {
                        state = State.MetadataRequestReceived;
                        BeginSendMetadata(token);
                    }
                    else 
                        state = State.SendingMetadata;
                    break;
                default:
                    return new ValueTask<bool>(false);
            }
            return new ValueTask<bool>(true);
        }

        private async ValueTask<(PacketHeaders, int, bool)> EndVote(Memory<byte> payload)
        {
            Debug.Assert(task is Task<Result<bool>>);
            var result = await ((Task<Result<bool>>)task).ConfigureAwait(false);
            task = null;
            payload.Span[0] = (byte)result.Value.ToInt32();
            return (new PacketHeaders(MessageType.Vote, FlowControl.Ack, result.Term), 1, false);
        }

        private async ValueTask<(PacketHeaders, int, bool)> SendMetadataPortionAsync(bool startStream, Memory<byte> output, CancellationToken token)
        {
            var continueSending = true;
            FlowControl control;
            var bytesWritten = await Reader.CopyToAsync(output, token).ConfigureAwait(false);
            if(bytesWritten == 0)
            {
                control = FlowControl.StreamEnd;
                continueSending = false;
            }
            else if(Reader.TryRead(out var readResult))
            {
                if(readResult.IsCanceled)
                {
                    control = FlowControl.Cancel;
                    continueSending = false;
                }
                else if(readResult.Buffer.Length == 0)
                {
                    control = FlowControl.StreamEnd;
                    continueSending = false;
                }
                else
                    control = FlowControl.Fragment;
            }
            else
            {
                control = startStream ? FlowControl.StreamStart : FlowControl.Fragment;
                continueSending = true;
            }
            return (new PacketHeaders(MessageType.Metadata, control, 0L), bytesWritten, continueSending);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> output, CancellationToken token)
            => state switch
            {
                State.VoteRequestReceived => EndVote(output),
                State.MetadataRequestReceived => SendMetadataPortionAsync(true, output, token),
                State.SendingMetadata => SendMetadataPortionAsync(false, output, token),
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