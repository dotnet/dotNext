namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;
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
    new IPersistentState AuditTrail { get; }

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
    /// Gets the term which belongs to the current leadership.
    /// </summary>
    /// <value><see langword="null"/> if the current node is not a leader.</value>
    long? LeadershipTerm { get; }

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
    /// <param name="barrierType">Read barrier type. It has effect only when this instance is a leader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="barrierType"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="QuorumUnreachableException">The quorum is not visible to the local node.</exception>
    ValueTask ApplyReadBarrierAsync(ReadBarrierType barrierType, CancellationToken token = default);

    /// <summary>
    /// Waits until the local node is elected as the leader.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The leadership token.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The local node is disposed.</exception>
    /// <exception cref="QuorumUnreachableException">The local node is stopped and never can be elected as leader.</exception>
    /// <seealso cref="LeadershipToken"/>
    Task<CancellationToken> WaitForLeadershipAsync(CancellationToken token = default);

    /// <summary>
    /// Waits for the next heartbeat round to confirm that the local node is still the leader
    /// in the specified term.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="IReplicationCluster.ForceReplicationAsync(CancellationToken)"/>, this method doesn't start a replication round.
    /// It waits for the next scheduled heartbeat round, or for a round started by another caller.
    /// Only a round that starts after this method is called counts.
    /// The method completes successfully only if the majority of the cluster members confirm the leadership
    /// of the local node in that round. So when the returned task completes,
    /// no other node could have been elected as a leader in a term greater than <paramref name="term"/>
    /// before this method was called.
    /// <para>
    /// Many concurrent callers share the same heartbeat round, so they produce no extra network traffic.
    /// The trade-off is latency, which can reach one heartbeat period plus the duration of one replication round.
    /// </para>
    /// <para>
    /// Unlike <see cref="ApplyReadBarrierAsync(ReadBarrierType, CancellationToken)"/>, the request is never
    /// forwarded to the leader. It fails if the local node is not the leader.
    /// </para>
    /// </remarks>
    /// <param name="term">The term in which the leadership was obtained; see <see cref="LeadershipTerm"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="NotLeaderException">
    /// The local node is not the leader, or its leadership was obtained in a different term,
    /// or it lost its leadership while waiting.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The local node is disposed.</exception>
    /// <seealso cref="LeadershipTerm"/>
    ValueTask ConfirmLeadershipAsync(long term, CancellationToken token = default);

    /// <summary>
    /// Associates the configuration storage with the WAL.
    /// </summary>
    /// <param name="auditTrail">The WAL implementation.</param>
    /// <param name="configurationStorage">The configuration storage.</param>
    protected static void SetConfigurationStorage(IPersistentState auditTrail, IClusterConfigurationStorage configurationStorage)
        => auditTrail.ConfigurationStorage = configurationStorage;
}