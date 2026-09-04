namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

/// <summary>
/// Represents incremental snapshot manager.
/// </summary>
public interface ISnapshotManager
{
    /// <summary>
    /// Takes the currently maintaining snapshot.
    /// </summary>
    /// <remarks>
    /// This method should be idempotent.
    /// </remarks>
    /// <returns>The currently maintaining snapshot; or <see langword="null"/> if there is no snapshot.</returns>
    /// <exception cref="InvalidOperationException">
    /// Some implementations may throw this exception to inform the caller that the information about the snapshot
    /// is not yet loaded. The caller needs to restore or initialize the manager first.
    /// </exception>
    ISnapshot? Snapshot { get; }

    /// <summary>
    /// Removes old snapshot versions stored on the disk.
    /// </summary>
    /// <remarks>
    /// The implementation can safely delete all the snapshot versions of the indices less than
    /// <paramref name="watermark"/>. The infrastructure guarantees that no one reader can
    /// get the snapshot of the index less than <paramref name="watermark"/>.
    /// </remarks>
    /// <param name="watermark">The index of the snapshot is known as the latest snapshot.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask ReclaimGarbageAsync(long watermark, CancellationToken token);
}