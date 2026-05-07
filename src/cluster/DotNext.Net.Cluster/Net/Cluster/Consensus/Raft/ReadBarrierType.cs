namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents read barrier type that must be applied on the leader.
/// </summary>
public enum ReadBarrierType
{
    /// <summary>
    /// Ensures that all uncommitted entries are applied.
    /// </summary>
    /// <remarks>
    /// This barrier type forces the replication to the followers.
    /// </remarks>
    Strong = 0,
    
    /// <summary>
    /// Ensures that the leader commits its write barrier only.
    /// </summary>
    /// <remarks>
    /// This barrier doesn't force the replication to the followers.
    /// </remarks>
    Weak = 1,
}