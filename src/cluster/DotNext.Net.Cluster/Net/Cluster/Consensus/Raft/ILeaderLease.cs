namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents Raft leader lease that can be used for linearizable read.
/// </summary>
public interface ILeaderLease
{
    /// <summary>
    /// Gets a value indicating that lease has expired.
    /// </summary>
    bool IsExpired { get; }
}