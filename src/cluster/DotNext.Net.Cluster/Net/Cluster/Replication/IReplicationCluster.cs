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

        IAuditTrail IReplicationCluster.AuditTrail => AuditTrail;

        /// <summary>
        /// Writes message into the cluster according with the specified concern.
        /// </summary>
        /// <remarks>
        /// Data isolation level should be implemented by the caller code.
        /// </remarks>
        /// <typeparam name="TEntryImpl">The actual type of the log entry returned by the supplier.</typeparam>
        /// <param name="entries">The number of commands to be committed into the audit trail.</param>
        /// <param name="concern">The value describing level of acknowledgment from cluster.</param>
        /// <param name="timeout">The timeout of the asynchronous operation.</param>
        /// <returns><see langword="false"/> if timeout occurred or changeset is rejected due to conflict; <see langword="true"/> if changeset is committed successfully.</returns>
        /// <exception cref="InvalidOperationException">The local cluster member is not a leader.</exception>
        /// <exception cref="NotSupportedException">The specified level of acknowledgment is not supported.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task<bool> WriteAsync<TEntryImpl>(ILogEntryProducer<TEntryImpl> entries, WriteConcern concern, TimeSpan timeout)
            where TEntryImpl : TEntry;
        
        /// <summary>
        /// Forces replication.
        /// </summary>
        /// <param name="timeout">The time to wait until replication ends.</param>
        /// <param name="token">The token that can be used to cancel waiting.</param>
        /// <returns><see langword="true"/> if replication is completed; <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">The local cluster member is not a leader.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task<bool> ForceReplicationAsync(TimeSpan timeout, CancellationToken token = default);
    }
}
