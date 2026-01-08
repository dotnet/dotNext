namespace DotNext.Benchmarks.WAL;

using Net.Cluster.Consensus.Raft.StateMachine;

internal sealed class NoOpStateMachine : IStateMachine
{
    ISnapshot? ISnapshotManager.Snapshot => null;

    ValueTask ISnapshotManager.ReclaimGarbageAsync(long watermark, CancellationToken token)
        => ValueTask.CompletedTask;

    ValueTask<long> IStateMachine.ApplyAsync(LogEntry entry, CancellationToken token)
        => ValueTask.FromResult(entry.Index);
}