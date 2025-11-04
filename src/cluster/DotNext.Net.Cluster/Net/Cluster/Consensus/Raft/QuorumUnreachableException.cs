namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Indicates that the operation cannot be performed on a node is unable to recognize the leader in the cluster.
/// </summary>
/// <param name="innerException">The exception that is the cause of the current exception.</param>
public sealed class QuorumUnreachableException(Exception? innerException = null) : LeaderOperationException(ExceptionMessages.LeaderIsUnavailable, innerException);