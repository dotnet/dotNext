using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
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
        /// <param name="timeout">The time to wait until replication ends.</param>
        /// <param name="token">The token that can be used to cancel waiting.</param>
        /// <returns><see langword="true"/> if replication is completed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">The local cluster member is not a leader.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task<bool> ForceReplicationAsync(TimeSpan timeout, CancellationToken token = default);

        /// <summary>
        /// Represents an event raised when the local node completes its replication with another
        /// node.
        /// </summary>
        event ReplicationCompletedEventHandler? ReplicationCompleted;
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
        /// <param name="timeout">The timeout of the operation.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if the appended log entry has been committed by the majority of nodes; <see langword="false"/> if retry is required.</returns>
        Task<bool> ReplicateAsync<TEntryImpl>(TEntryImpl entry, TimeSpan timeout, CancellationToken token = default)
            where TEntryImpl : notnull, TEntry;
    }
}
