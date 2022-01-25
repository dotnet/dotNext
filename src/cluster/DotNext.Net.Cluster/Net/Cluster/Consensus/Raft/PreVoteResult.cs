namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents a result of pre-voting phase.
/// </summary>
public enum PreVoteResult : byte
{
    /// <summary>
    /// Indicates that the remote node supposes that the leader is alive.
    /// </summary>
    RejectedByFollower = 0,

    /// <summary>
    /// Indicates that the remote node supposes that the leader is not alive.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// Indicates that the remote node is a leader.
    /// </summary>
    RejectedByLeader = 2,
}