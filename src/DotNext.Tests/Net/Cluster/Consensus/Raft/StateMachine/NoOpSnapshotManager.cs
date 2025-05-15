using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

[Experimental("DOTNEXT001")]
internal abstract class NoOpSnapshotManager : ISnapshotManager
{
    ISnapshot ISnapshotManager.TakeSnapshot() => null;

    ValueTask ISnapshotManager.ReclaimGarbageAsync(long lowerSnapshotIndex, CancellationToken token)
        => ValueTask.CompletedTask;
}