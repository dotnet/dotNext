namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Indicates that the operation requires a leader in the cluster.
/// </summary>
public abstract class LeaderOperationException : InvalidOperationException
{
    private protected LeaderOperationException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}