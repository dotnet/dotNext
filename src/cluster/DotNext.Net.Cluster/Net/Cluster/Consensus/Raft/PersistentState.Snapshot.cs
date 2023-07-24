namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Specialized;
using IO.Log;
using IAsyncBinaryReader = IO.IAsyncBinaryReader;

public partial class PersistentState
{
    private protected ref readonly SnapshotMetadata SnapshotInfo => ref state.Snapshot;

    private protected void UpdateSnapshotInfo(in SnapshotMetadata metadata)
        => state.UpdateSnapshotMetadata(in metadata);

    private protected abstract ValueTask InstallSnapshotAsync<TSnapshot>(TSnapshot snapshot, long snapshotIndex)
        where TSnapshot : notnull, IRaftLogEntry;

    private protected abstract ValueTask<IAsyncBinaryReader> BeginReadSnapshotAsync(int sessionId, CancellationToken token);

    private protected abstract void EndReadSnapshot(int sessionId);

    private ValueTask<TResult> UnsafeReadSnapshotAsync<TResult>(ILogEntryConsumer<IRaftLogEntry, TResult> reader, int sessionId, CancellationToken token)
    {
        return reader.LogEntryMetadataOnly ? ReadMetadataOnlyAsync() : ReadSlowAsync();

        ValueTask<TResult> ReadMetadataOnlyAsync()
            => reader.ReadAsync<LogEntry, SingletonList<LogEntry>>(new LogEntry(in SnapshotInfo), SnapshotInfo.Index, token);

        async ValueTask<TResult> ReadSlowAsync()
        {
            var entry = new LogEntry(in SnapshotInfo)
            {
                ContentReader = await BeginReadSnapshotAsync(sessionId, token).ConfigureAwait(false),
            };

            try
            {
                return await reader.ReadAsync<LogEntry, SingletonList<LogEntry>>(entry, entry.SnapshotIndex, token).ConfigureAwait(false);
            }
            finally
            {
                EndReadSnapshot(sessionId);
            }
        }
    }
}