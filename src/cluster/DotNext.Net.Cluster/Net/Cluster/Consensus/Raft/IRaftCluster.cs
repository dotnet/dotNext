namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Replication;

/// <summary>
/// Represents cluster of nodes coordinated using Raft consensus protocol.
/// </summary>
public interface IRaftCluster : IReplicationCluster<IRaftLogEntry>, IPeerMesh<IRaftClusterMember>
{
    /// <summary>
    /// Represents metrics attribute containing the address of the local node.
    /// </summary>
    protected const string LocalAddressMeterAttributeName = "dotnext.raft.server.address";

    /// <summary>
    /// Gets term number used by Raft algorithm to check the consistency of the cluster.
    /// </summary>
    long Term => AuditTrail.Term;

    /// <summary>
    /// Gets election timeout used by local cluster member.
    /// </summary>
    TimeSpan ElectionTimeout { get; }

    /// <summary>
    /// Gets a set of visible cluster members.
    /// </summary>
    IReadOnlyCollection<IRaftClusterMember> Members { get; }

    /// <summary>
    /// Defines persistent state for the Raft-based cluster.
    /// </summary>
    new IPersistentState AuditTrail { get; set; }

    /// <summary>
    /// Tries to get the lease that can be used to perform the read with linearizability guarantees.
    /// </summary>
    /// <param name="token">The token representing lease.</param>
    /// <returns><see langword="true"/> if the current node is leader; otherwise, <see langword="false"/>.</returns>
    bool TryGetLeaseToken(out CancellationToken token);

    /// <summary>
    /// Gets the token that can be used to track leader state.
    /// </summary>
    /// <remarks>
    /// The token moves to canceled state if the current node downgrades to the follower state.
    /// </remarks>
    CancellationToken LeadershipToken { get; }

    /// <summary>
    /// Gets a token that remains non-canceled while the local node is a part of the majority of the cluster and
    /// has communication with the leader.
    /// </summary>
    /// <remarks>
    /// The token moves to canceled state if the current node upgrades to the candidate state or loses connection with the leader.
    /// </remarks>
    CancellationToken ConsensusToken { get; }

    /// <summary>
    /// Represents a task indicating that the current node is ready to serve requests.
    /// </summary>
    Task Readiness { get; }

    /// <inheritdoc/>
    IAuditTrail<IRaftLogEntry> IReplicationCluster<IRaftLogEntry>.AuditTrail => AuditTrail;

    /// <summary>
    /// Ensures linearizable read from underlying state machine.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="QuorumUnreachableException">The quorum is not visible to the local node.</exception>
    ValueTask ApplyReadBarrierAsync(CancellationToken token = default);

    /// <summary>
    /// Waits until the local node is elected as the leader.
    /// </summary>
    /// <param name="timeout">The time to wait; or <see cref="Timeout.InfiniteTimeSpan"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The leadership token.</returns>
    /// <exception cref="TimeoutException">The operation is timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The local node is disposed.</exception>
    /// <seealso cref="LeadershipToken"/>
    ValueTask<CancellationToken> WaitForLeadershipAsync(TimeSpan timeout, CancellationToken token = default);
}