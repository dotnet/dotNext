using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using static IO.Pipelines.PipeExtensions;
    using static Runtime.Intrinsics;

    internal sealed class ServerExchange : PipeExchange
    {
        private enum State : byte
        {
            Ready = 0,
            VoteRequestReceived,
            MetadataRequestReceived,
            SendingMetadata,
            ResignRequestReceived,
        }

        private readonly IRaftRpcServer server;
        private Task? task;
        private State state;

        internal ServerExchange(IRaftRpcServer server, PipeOptions? options = null)
            : base(options)
            => this.server = server;
        
        private void BeginResign(CancellationToken token)
            => task = server.ResignAsync(token);
        
        private void BeginVote(ReadOnlyMemory<byte> payload, CancellationToken token)
        {
            VoteExchange.Parse(payload.Span, out var term, out var lastLogIndex, out var lastLogTerm);
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
                    BeginVote(payload, token);
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
                case MessageType.Resign:
                    state = State.ResignRequestReceived;
                    BeginResign(token);
                    break;
                default:
                    return new ValueTask<bool>(false);
            }
            return new ValueTask<bool>(true);
        }

        private async ValueTask<(PacketHeaders, int, bool)> EndVote(Memory<byte> payload)
        {
            Debug.Assert(task is Task<Result<bool>>);
            var result = await Cast<Task<Result<bool>>>(task).ConfigureAwait(false);
            task = null;
            return (new PacketHeaders(MessageType.Vote, FlowControl.Ack), IExchange.WriteResult(result, payload.Span), false);
        }

        private async ValueTask<(PacketHeaders, int, bool)> SendMetadataPortionAsync(bool startStream, Memory<byte> output, CancellationToken token)
        {
            var continueSending = true;
            FlowControl control;
            var bytesWritten = await Reader.CopyToAsync(output, token).ConfigureAwait(false);
            if(bytesWritten == output.Length)    //final packet detected
            {
                control = startStream ? FlowControl.StreamStart : FlowControl.Fragment;
                continueSending = true;
            }
            else
            {
                control = FlowControl.StreamEnd;
                continueSending = false;
            }
            return (new PacketHeaders(MessageType.Metadata, control), bytesWritten, continueSending);
        }

        private async ValueTask<(PacketHeaders, int, bool)> EndResign(Memory<byte> payload)
        {
            var result = await Cast<Task<bool>>(task).ConfigureAwait(false);
            task = null;
            payload.Span[0] = (byte)result.ToInt32();
            return (new PacketHeaders(MessageType.Resign, FlowControl.Ack), 1, false);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> output, CancellationToken token)
            => state switch
            {
                State.VoteRequestReceived => EndVote(output),
                State.MetadataRequestReceived => SendMetadataPortionAsync(true, output, token),
                State.SendingMetadata => SendMetadataPortionAsync(false, output, token),
                State.ResignRequestReceived => EndResign(output),
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