using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    [StructLayout(LayoutKind.Auto)]
    [RequiresPreviewFeatures]
    private readonly struct InstallSnapshotExchange : IClientExchange<Result<bool>>
    {
        private readonly ILocalMember localMember;
        private readonly IRaftLogEntry snapshot;
        private readonly long term, snapshotIndex;

        internal InstallSnapshotExchange(ILocalMember localMember, long term, IRaftLogEntry snapshot, long snapshotIndex)
        {
            Debug.Assert(localMember is not null);
            Debug.Assert(snapshot is not null);
            Debug.Assert(snapshot.IsSnapshot);

            this.localMember = localMember;
            this.term = term;
            this.snapshot = snapshot;
            this.snapshotIndex = snapshotIndex;
        }

        ValueTask IClientExchange<Result<bool>>.RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteInstallSnapshotRequestAsync(localMember.Id, term, snapshotIndex, snapshot, buffer, token);

        static ValueTask<Result<bool>> IClientExchange<Result<bool>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadResultAsync(token);
    }

    [RequiresPreviewFeatures]
    private protected sealed override Task<Result<bool>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
        => RequestAsync<InstallSnapshotExchange, Result<bool>>(new(localMember, term, snapshot, snapshotIndex), token);
}