using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    private sealed class InstallSnapshotRequest : Request<Result<bool>>
    {
        private readonly ILocalMember localMember;
        private readonly IRaftLogEntry snapshot;
        private readonly long term, snapshotIndex;

        internal InstallSnapshotRequest(ILocalMember localMember, long term, IRaftLogEntry snapshot, long snapshotIndex)
        {
            Debug.Assert(localMember is not null);
            Debug.Assert(snapshot is not null);
            Debug.Assert(snapshot.IsSnapshot);

            this.localMember = localMember;
            this.term = term;
            this.snapshot = snapshot;
            this.snapshotIndex = snapshotIndex;
        }

        private protected override ValueTask RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteInstallSnapshotRequestAsync(localMember.Id, term, snapshotIndex, snapshot, buffer, token);

        private protected override ValueTask<Result<bool>> ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadResultAsync(token);
    }

    private protected sealed override Task<Result<bool>> InstallSnapshotAsync(long term, IRaftLogEntry snapshot, long snapshotIndex, CancellationToken token)
        => RequestAsync(new InstallSnapshotRequest(localMember, term, snapshot, snapshotIndex), token);
}