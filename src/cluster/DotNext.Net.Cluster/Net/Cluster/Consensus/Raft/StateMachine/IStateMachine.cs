using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

/// <summary>
/// Represents state machine.
/// </summary>
[Experimental("DOTNEXT001")]
public interface IStateMachine : ISnapshotManager
{
    /// <summary>
    /// Applies the log entry to the underlying state machine.
    /// </summary>
    /// <remarks>
    /// This method is never called concurrently by <see cref="WriteAheadLog"/> infrastructure. However,
    /// it can be called concurrently with <see cref="ISnapshotManager.Snapshot"/> or <see cref="ISnapshotManager.ReclaimGarbageAsync"/>
    /// methods. The implementation can create a new snapshot on the disk. In this case, it should replace the current snapshot
    /// and <see cref="ISnapshotManager.Snapshot"/> will return newly generated snapshot.
    /// </remarks>
    /// <param name="entry">The log entry to be applied.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the last applied log entry. It should be greater than or equal to <see cref="LogEntry.Index"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask<long> ApplyAsync(LogEntry entry, CancellationToken token);
}