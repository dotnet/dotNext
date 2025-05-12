using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO.Log;

public interface ISnapshot : IRaftLogEntry
{
    /// <inheritdoc/>
    bool ILogEntry.IsSnapshot => true;

    /// <summary>
    /// Gets the index of the snapshot.
    /// </summary>
    long Index { get; }
}

public interface ISnapshotManager
{
    ISnapshot? TakeSnapshot();

    ValueTask ReclaimGarbageAsync(CancellationToken token);
}

public interface IStateManager : ISnapshotManager
{
    ValueTask<long> ApplyAsync(CancellationToken token);
}