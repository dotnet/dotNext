namespace DotNext.Net.Cluster.Replication;

using IO.Log;

/// <summary>
/// Represents replication cluster.
/// </summary>
public interface IReplicationCluster : ICluster
{
    /// <summary>
    /// Gets transaction log used for replication.
    /// </summary>
    IAuditTrail AuditTrail { get; }

    /// <summary>
    /// Forces replication.
    /// </summary>
    /// <param name="token">The token that can be used to cancel waiting.</param>
    /// <exception cref="InvalidOperationException">The local cluster member is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    Task ForceReplicationAsync(CancellationToken token = default);

    /// <summary>
    /// Represents an event raised when the local node completes its replication with another
    /// node.
    /// </summary>
    event Action<IReplicationCluster, IClusterMember> ReplicationCompleted;
}

/// <summary>
/// Represents replication cluster.
/// </summary>
/// <typeparam name="TEntry">The type of the log entry in the transaction log.</typeparam>
public interface IReplicationCluster<TEntry> : IReplicationCluster
    where TEntry : class, ILogEntry
{
    /// <summary>
    /// Gets transaction log used for replication.
    /// </summary>
    new IAuditTrail<TEntry> AuditTrail { get; }

    /// <inheritdoc/>
    IAuditTrail IReplicationCluster.AuditTrail => AuditTrail;

    /// <summary>
    /// Appends a new log entry and ensures that it is replicated and committed.
    /// </summary>
    /// <typeparam name="TEntryImpl">The type of the log entry.</typeparam>
    /// <param name="entry">The log entry to be added.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the appended log entry has been committed by the majority of nodes; <see langword="false"/> if retry is required.</returns>
    /// <exception cref="InvalidOperationException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    Task<bool> ReplicateAsync<TEntryImpl>(TEntryImpl entry, CancellationToken token = default)
        where TEntryImpl : notnull, TEntry;
}