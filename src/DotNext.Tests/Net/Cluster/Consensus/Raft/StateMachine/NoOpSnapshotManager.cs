using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

[Experimental("DOTNEXT001")]
internal abstract class NoOpSnapshotManager : ISnapshotManager
{
    ISnapshot ISnapshotManager.TakeSnapshot() => null;

    ValueTask ISnapshotManager.ReclaimGarbageAsync(long watermark, CancellationToken token)
        => ValueTask.CompletedTask;
}