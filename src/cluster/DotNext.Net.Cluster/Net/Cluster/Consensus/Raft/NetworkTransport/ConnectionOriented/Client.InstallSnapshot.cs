using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport.ConnectionOriented;

using IO;
using static Buffers.ByteBuffer;

internal partial class Client
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct InstallSnapshotExchange(long term, IRaftLogEntry snapshot, long snapshotIndex,
        IDataTransferObject configuration, long configurationVersion) : IClientExchange<Result<HeartbeatResult>>
    {
        private const string Name = "InstallSnapshot";

        ValueTask IClientExchange<Result<HeartbeatResult>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer,
            CancellationToken token)
        {
            protocol.AdvanceWriteCursor(WriteHeaders(protocol, in localMember.Id));
            return RequestAsync(snapshot, configuration, protocol, buffer, token);
        }

        private static async ValueTask RequestAsync(IRaftLogEntry snapshot, IDataTransferObject configuration, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            // write configuration
            protocol.StartFrameWrite();
            await configuration.WriteToAsync(protocol, buffer, token).ConfigureAwait(false);
            protocol.WriteFinalFrame();

            // ensure that the subsequent write can place at least 1 frame with 1 byte payload
            if (!protocol.CanWriteFrameSynchronously(frameSize: 1))
                await protocol.WriteToTransportAsync(token).ConfigureAwait(false);
            
            // write snapshot
            protocol.StartFrameWrite();
            await snapshot.WriteToAsync(protocol, buffer, token).ConfigureAwait(false);
            protocol.WriteFinalFrame();
            
            await protocol.WriteToTransportAsync(token).ConfigureAwait(false);
        }

        private int WriteHeaders(ProtocolStream protocol, in ClusterMemberId sender)
        {
            var writer = protocol.BeginRequestMessage(MessageType.InstallSnapshot);
            writer.Write<SnapshotMessage>(new(sender, term, snapshotIndex, snapshot, configurationVersion));
            return writer.WrittenCount;
        }

        static ValueTask<Result<HeartbeatResult>> IClientExchange<Result<HeartbeatResult>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadHeartbeatResultAsync(token);

        static string IClientExchange<Result<HeartbeatResult>>.Name => Name;
    }

    private protected sealed override Task<Result<HeartbeatResult>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex,
        IDataTransferObject configuration, long configurationVersion, CancellationToken token)
        => RequestAsync<Result<HeartbeatResult>, InstallSnapshotExchange>(new(term, snapshot, snapshotIndex, configuration, configurationVersion), token);
}