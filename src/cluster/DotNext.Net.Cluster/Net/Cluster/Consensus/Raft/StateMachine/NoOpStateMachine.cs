using System.Diagnostics;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;

internal sealed class NoOpStateMachine : IStateMachine
{
    private readonly long snapshotDepth;
    private ISnapshot? snapshot;

    public NoOpStateMachine(long snapshotDepth)
    {
        Debug.Assert(snapshotDepth >= 2L);
        
        this.snapshotDepth = snapshotDepth;
    }

    private long GetSnapshotIndex(long index)
    {
        var snapshotIndex = long.Max(0L, index - 1L);
        return snapshotIndex - snapshotIndex % snapshotDepth;
    }

    internal void SetLastCommittedIndex(long committedIndex)
    {
        var snapshotIndex = GetSnapshotIndex(committedIndex);
        snapshot = snapshotIndex > 0L ? new EmptySnapshot(snapshotIndex) : null;
    }

    /// <inheritdoc/>
    ISnapshot? ISnapshotManager.Snapshot => snapshot;

    /// <inheritdoc/>
    ValueTask ISnapshotManager.ReclaimGarbageAsync(long watermark, CancellationToken token) => ValueTask.CompletedTask;

    /// <inheritdoc/>
    ValueTask<long> IStateMachine.ApplyAsync(LogEntry entry, CancellationToken token)
    {
        long appliedIndex;
        var snapshotIndex = GetSnapshotIndex(entry.Index);
        var snapshotCopy = snapshot;

        if (snapshotIndex is 0L)
        {
            appliedIndex = entry.Index;
        }
        else if (snapshotCopy is null || snapshotCopy.Index < snapshotIndex)
        {
            snapshot = new EmptySnapshot(snapshotIndex);
            appliedIndex = entry.Index;
        }
        else if (snapshotCopy.Index > entry.Index)
        {
            appliedIndex = snapshotCopy.Index;
        }
        else
        {
            appliedIndex = entry.Index;
        }

        return ValueTask.FromResult(appliedIndex);
    }

    private sealed class EmptySnapshot(long index) : ISnapshot
    {
        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length => 0L;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => ValueTask.CompletedTask;

        long IRaftLogEntry.Term => 0L;

        long ISnapshot.Index => index;
    }
}