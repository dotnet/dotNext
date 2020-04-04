using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using IO.Log;
    using static IO.Pipelines.PipeExtensions;
    using static Runtime.Intrinsics;

    internal sealed class ServerExchange : PipeExchange
    {
        private static readonly ILogEntryProducer<IRaftLogEntry> EmptyProducer = new LogEntryProducer<IRaftLogEntry>();

        private enum State : byte
        {
            Ready = 0,
            VoteRequestReceived,
            MetadataRequestReceived,
            SendingMetadata,
            ResignRequestReceived,
            HeartbeatRequestReceived,
        }

        private readonly ILocalMember server;
        private Task? task;
        private State state;

        internal ServerExchange(ILocalMember server, PipeOptions? options = null)
            : base(options)
            => this.server = server;
        
        private void BeginResign(CancellationToken token)
            => task = server.ResignAsync(token);
        
        private void BeginVote(ReadOnlyMemory<byte> payload, EndPoint member, CancellationToken token)
        {
            VoteExchange.Parse(payload.Span, out var term, out var lastLogIndex, out var lastLogTerm);
            task = server.ReceiveVoteAsync(member, term, lastLogIndex, lastLogTerm, token);
        }

        private void BeginSendMetadata(CancellationToken token)
        {
            task = MetadataExchange.WriteAsync(Writer, server.Metadata, token);
        }

        private void BeginProcessHeartbeat(ReadOnlyMemory<byte> payload, EndPoint member, CancellationToken token)
        {
            HeartbeatExchange.Parse(payload.Span, out var term, out var prevLogIndex, out var prevLogTerm, out var commitIndex);
            task = server.ReceiveEntriesAsync(member, term, EmptyProducer, prevLogIndex, prevLogTerm, commitIndex, token);
        }
        
        public override ValueTask<bool> ProcessInbountMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endpoint, CancellationToken token)
        {
            switch(headers.Type)
            {
                default:
                    return new ValueTask<bool>(false);
                case MessageType.Vote:
                    state = State.VoteRequestReceived;
                    BeginVote(payload, endpoint, token);
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
                case MessageType.Heartbeat:
                    state = State.HeartbeatRequestReceived;
                    BeginProcessHeartbeat(payload, endpoint, token);
                    break;
            }
            return new ValueTask<bool>(true);
        }

        private async ValueTask<(PacketHeaders, int, bool)> EndVote(Memory<byte> payload)
        {
            var result = await Cast<Task<Result<bool>>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
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
            var result = await Cast<Task<bool>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
            task = null;
            payload.Span[0] = (byte)result.ToInt32();
            return (new PacketHeaders(MessageType.Resign, FlowControl.Ack), 1, false);
        }

        private async ValueTask<(PacketHeaders, int, bool)> EndProcessHearbeat(Memory<byte> output)
        {
            var result = await Cast<Task<Result<bool>>>(Interlocked.Exchange(ref task, null)).ConfigureAwait(false);
            return (new PacketHeaders(MessageType.Heartbeat, FlowControl.Ack), IExchange.WriteResult(result, output.Span), false);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> output, CancellationToken token)
            => state switch
            {
                State.VoteRequestReceived => EndVote(output),
                State.MetadataRequestReceived => SendMetadataPortionAsync(true, output, token),
                State.SendingMetadata => SendMetadataPortionAsync(false, output, token),
                State.ResignRequestReceived => EndResign(output),
                State.HeartbeatRequestReceived => EndProcessHearbeat(output),
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