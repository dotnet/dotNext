namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents extension methods for <see cref="IRaftCluster"/> interface.
/// </summary>
public static class RaftClusterExtensions
{
    /// <summary>
    /// Appends a new log entry and ensures that it is replicated and committed.
    /// </summary>
    /// <param name="cluster">The cluster node.</param>
    /// <param name="payload">The log entry payload.</param>
    /// <param name="context">The context to be passed to the state machine.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the appended log entry has been committed by the majority of nodes; <see langword="false"/> if retry is required.</returns>
    /// <exception cref="InvalidOperationException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<bool> ReplicateAsync(this IRaftCluster cluster, ReadOnlyMemory<byte> payload, object? context = null,
        CancellationToken token = default)
        => cluster.ReplicateAsync<BinaryLogEntry>(new() { Content = payload, Term = cluster.Term, Context = context }, token);
}