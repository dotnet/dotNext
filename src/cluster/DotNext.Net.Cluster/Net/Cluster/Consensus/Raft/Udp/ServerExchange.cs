using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal sealed partial class ServerExchange : PipeExchange
    {
        private enum State : byte
        {
            Ready = 0,
            VoteRequestReceived,
            MetadataRequestReceived,
            SendingMetadata,
            ResignRequestReceived,
            HeartbeatRequestReceived,

            AppendEntriesReceived,
            ReadyToReceiveEntry,
            ReceivingEntry,
            ReceivingEntriesFinished
        }

        private readonly ILocalMember server;
        private Task? task;
        private State state;

        internal ServerExchange(ILocalMember server, PipeOptions? options = null)
            : base(options)
            => this.server = server;
        
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
                case MessageType.AppendEntries:
                    if(state == State.AppendEntriesReceived)
                    {

                    }
                    else
                    {
                        state = State.AppendEntriesReceived;
                        BeginReceiveEntries(endpoint, payload.Span, token);
                    }
                    break;
            }
            return new ValueTask<bool>(true);
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