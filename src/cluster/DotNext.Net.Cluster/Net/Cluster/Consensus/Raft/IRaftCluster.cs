namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Replication;

/// <summary>
/// Represents cluster of nodes coordinated using Raft consensus protocol.
/// </summary>
public interface IRaftCluster : IReplicationCluster<IRaftLogEntry>, IPeerMesh<IRaftClusterMember>
{
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
    /// Establishes metrics collector.
    /// </summary>
    MetricsCollector Metrics { set; }

    /// <summary>
    /// Defines persistent state for the Raft-based cluster.
    /// </summary>
    new IPersistentState AuditTrail { get; set; }

    /// <summary>
    /// Gets the lease that can be used to perform read with linerizability guarantees.
    /// </summary>
    ILeaderLease? Lease { get; }

    /// <summary>
    /// Gets the token that can be used to track leader state.
    /// </summary>
    /// <remarks>
    /// The token moves to canceled state if the current node downgrades to the follower state.
    /// </remarks>
    CancellationToken LeadershipToken { get; }

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
    ValueTask ApplyReadBarrierAsync(CancellationToken token = default);
}