using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using IO;
using static Buffers.ByteBuffer;

internal partial class Client
{
    private sealed class InstallSnapshotExchange(long term, IRaftLogEntry snapshot, long snapshotIndex) : IClientExchange<Result<HeartbeatResult>>
    {
        private const string Name = "InstallSnapshot";

        async ValueTask IClientExchange<Result<HeartbeatResult>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            protocol.AdvanceWriteCursor(WriteHeaders(protocol, in localMember.Id));
            protocol.StartFrameWrite();
            await snapshot.WriteToAsync(protocol, buffer, token).ConfigureAwait(false);
            protocol.WriteFinalFrame();
            await protocol.WriteToTransportAsync(token).ConfigureAwait(false);
        }

        private int WriteHeaders(ProtocolStream protocol, in ClusterMemberId sender)
        {
            var writer = protocol.BeginRequestMessage(MessageType.InstallSnapshot);
            writer.Write<SnapshotMessage>(new(sender, term, snapshotIndex, snapshot));
            return writer.WrittenCount;
        }

        static ValueTask<Result<HeartbeatResult>> IClientExchange<Result<HeartbeatResult>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadHeartbeatResultAsync(token);

        static string IClientExchange<Result<HeartbeatResult>>.Name => Name;
    }

    private protected sealed override Task<Result<HeartbeatResult>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
        => RequestAsync<Result<HeartbeatResult>, InstallSnapshotExchange>(new(term, snapshot, snapshotIndex), token);
}