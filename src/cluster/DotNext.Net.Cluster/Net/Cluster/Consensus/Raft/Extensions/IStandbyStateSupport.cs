namespace DotNext.Net.Cluster.Consensus.Raft.Extensions;

/// <summary>
/// Provides support of Standby state in addition to standard cluster member states defined in Raft (follower, candidate, leader).
/// </summary>
public interface IStandbyStateSupport : IRaftCluster
{
    /// <summary>
    /// Turns this node into regular state when the node can be elected as leader.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if state transition is resumed successfully;
    /// <see langword="false"/> if state transition was not suspended.</returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    ValueTask<bool> ResumeStateTransitionAsync(CancellationToken token = default);

    /// <summary>
    /// Suspends any transition over Raft states.
    /// </summary>
    /// <remarks>
    /// This method completes successfully only if the local member is in Follower state.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if any further state transitions are suspended successfully because the local member is in Follower state;
    /// <see langword="false"/> if operation fails because state transition is already suspended or the local member is not in Follower state.
    /// </returns>
    /// <exception cref="ObjectDisposedException">This object has been disposed.</exception>
    ValueTask<bool> SuspendStateTransitionAsync(CancellationToken token = default);

    /// <summary>
    /// Gets a value indicating that the local member cannot be elected as cluster leader.
    /// </summary>
    bool IsStateTransitionSuspended { get; }
}