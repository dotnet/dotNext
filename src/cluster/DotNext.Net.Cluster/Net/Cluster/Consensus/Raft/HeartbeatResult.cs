namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// A result of heartbeat processing returned by the Follower member.
/// </summary>
public enum HeartbeatResult : byte
{
    /// <summary>
    /// Incoming replication is outdated in comparison to the local state of the received.
    /// </summary>
    Rejected = 0,

    /// <summary>
    /// Incoming replication is applied to underlying state machine.
    /// </summary>
    Replicated = 1,

    /// <summary>
    /// Incoming replication is applied to underlying state machine, and at least one of the
    /// log entries from the replica has the same Term as the sender.
    /// </summary>
    ReplicatedWithLeaderTerm = 2,
    
    /// <summary>
    /// Incoming replication is rejected because the sender has higher state machine version.
    /// </summary>
    /// <remarks>
    /// No need to decrement <see cref="IRaftClusterMember.ReplicationState.NextIndex"/> in contrast
    /// to <see cref="Rejected"/> status.
    /// When the receiver has higher term than the sender, this status should not be reported.
    /// </remarks>
    UnsupportedVersion = 3,
}