using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
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
    /// <typeparam name="LogEntry">The type of the log entry in the transaction log.</typeparam>
    public interface IReplicationCluster<LogEntry> : IReplicationCluster
        where LogEntry : class, ILogEntry
    {
        /// <summary>
        /// Gets transaction log used for replication.
        /// </summary>
        new IAuditTrail<LogEntry> AuditTrail { get; }

        /// <summary>
        /// Writes message into the cluster according with the specified concern.
        /// </summary>
        /// <remarks>
        /// Data isolation level should be implemented by the caller code.
        /// </remarks>
        /// <param name="entries">The number of commands to be committed into the audit trail.</param>
        /// <param name="concern">The value describing level of acknowledgment from cluster.</param>
        /// <param name="timeout">The timeout of the asynchronous operation.</param>
        /// <returns>The task representing asynchronous state of this operation.</returns>
        /// <exception cref="InvalidOperationException">The local cluster member is not a leader.</exception>
        /// <exception cref="NotSupportedException">The specified level of acknowledgment is not supported.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task WriteAsync(IReadOnlyList<LogEntry> entries, WriteConcern concern, TimeSpan timeout);
    }
}
