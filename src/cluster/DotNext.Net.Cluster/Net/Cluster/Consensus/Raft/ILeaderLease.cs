namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents Raft leader lease that can be used for linearizable read.
/// </summary>
public interface ILeaderLease
{
    /// <summary>
    /// Gets a value indicating that lease has expired.
    /// </summary>
    [Obsolete("Use Token property instead")]
    bool IsExpired => Token.IsCancellationRequested;

    /// <summary>
    /// Gets the token that can be used for async linearizable read.
    /// </summary>
    /// <remarks>
    /// The returned token may be different if this property is called at different times.
    /// </remarks>
    CancellationToken Token { get; }
}