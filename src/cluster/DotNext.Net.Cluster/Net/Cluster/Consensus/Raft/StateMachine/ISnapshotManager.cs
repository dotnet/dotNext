using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

/// <summary>
/// Represents incremental snapshot manager.
/// </summary>
[Experimental("DOTNEXT001")]
public interface ISnapshotManager
{
    /// <summary>
    /// Takes the currently maintaining snapshot.
    /// </summary>
    /// <returns>The currently maintaining snapshot; or <see langowrd="null"/> if there is no snapshot.</returns>
    ISnapshot? TakeSnapshot();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="lowerSnapshotIndex"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    ValueTask ReclaimGarbageAsync(long lowerSnapshotIndex, CancellationToken token);
}