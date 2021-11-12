namespace DotNext.Net.Cluster.Consensus.Raft;

using IAsyncBinaryReader = IO.IAsyncBinaryReader;

public partial class PersistentState
{
    private protected interface ISnapshotReader
    {
        ref readonly SnapshotMetadata Metadata { get; }

        ValueTask<IAsyncBinaryReader> BeginReadSnapshotAsync(int sessionId, CancellationToken token);

        void EndReadSnapshot();
    }

    private protected abstract ValueTask<Partition?> InstallSnapshotAsync<TSnapshot>(TSnapshot snapshot, long snapshotIndex)
        where TSnapshot : notnull, IRaftLogEntry;

    private protected abstract ISnapshotReader? SnapshotReader { get; }
}