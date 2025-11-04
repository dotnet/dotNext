namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Indicates that the operation cannot be performed on a node which is not a leader.
/// </summary>
/// <param name="innerException">The exception that is the cause of the current exception.</param>
public sealed class NotLeaderException(Exception? innerException = null) : LeaderOperationException(ExceptionMessages.LocalNodeNotLeader, innerException);