using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    internal sealed partial class ServerExchange : PipeExchange, IDisposable
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

            ReadyToReceiveEntry,    //ready to receive next entry
            ReadyToReadEntry,   //log entry header is obtained, content is available
            ReceivingEntriesFinished,
            SnapshotReceived,
            ReceivingSnapshotFinished
        }


        private readonly ILocalMember server;
        private Task? task;
        private State state;

        internal ServerExchange(ILocalMember server, PipeOptions? options = null)
            : base(options) => this.server = server;

        private static void ChangePort(ref EndPoint endPoint, ushort port)
        {
            switch (endPoint)
            {
                case IPEndPoint ip:
                    endPoint = ip.Port == port ? ip : new IPEndPoint(ip.Address, port);
                    break;
                case DnsEndPoint dns:
                    endPoint = dns.Port == port ? dns : new DnsEndPoint(dns.Host, port, dns.AddressFamily);
                    break;
            }
        }

        public override ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endpoint, CancellationToken token)
        {
            switch (headers.Type)
            {
                default:
                    return new ValueTask<bool>(false);
                case MessageType.Vote:
                    state = State.VoteRequestReceived;
                    BeginVote(payload, endpoint, token);
                    break;
                case MessageType.Metadata:
                    if (headers.Control == FlowControl.None)
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
                    switch (state)
                    {
                        case State.Ready:
                            BeginReceiveEntries(endpoint, payload.Span, token);
                            break;
                        case State.ReadyToReceiveEntry:
                            BeginReceiveEntry(payload);
                            break;
                        case State.ReadyToReadEntry:
                            return ReceivingEntry(payload, headers.Control == FlowControl.StreamEnd, token);
                        default:
                            return default;
                    }
                    break;
                case MessageType.InstallSnapshot:
                    switch (state)
                    {
                        case State.Ready:
                            state = State.SnapshotReceived;
                            BeginReceiveSnapshot(payload.Span, endpoint, token);
                            break;
                        case State.SnapshotReceived:
                            return ReceivingSnapshot(payload, headers.Control == FlowControl.StreamEnd, token);
                        default:
                            return default;
                    }

                    break;
            }
            return new ValueTask<bool>(true);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> output, CancellationToken token)
        {
            switch (state)
            {
                default:
                    return default;
                case State.VoteRequestReceived:
                    return EndVote(output);
                case State.MetadataRequestReceived:
                    return SendMetadataPortionAsync(true, output, token);
                case State.SendingMetadata:
                    return SendMetadataPortionAsync(false, output, token);
                case State.ResignRequestReceived:
                    return EndResign(output);
                case State.HeartbeatRequestReceived:
                    return EndProcessHearbeat(output);
                case State.AppendEntriesReceived:
                case State.ReadyToReceiveEntry:
                case State.ReadyToReadEntry:
                case State.ReceivingEntriesFinished:
                    return TransmissionControl(output, token);
                case State.SnapshotReceived:
                    return RequestSnapshotChunk();
                case State.ReceivingSnapshotFinished:
                    return EndReceiveSnapshot(output);
            }
        }

        internal void Reset()
        {
            ReusePipe();
            task = null;
            state = State.Ready;
            transmissionStateTrigger.CancelSuspendedCallers(new CancellationToken(true));
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                transmissionStateTrigger.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ServerExchange() => Dispose(false);
    }
}