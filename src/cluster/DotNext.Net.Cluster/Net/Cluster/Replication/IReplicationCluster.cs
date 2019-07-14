using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    using IMessage = Messaging.IMessage;

    /// <summary>
    /// Represents replication cluster.
    /// </summary>
    /// <typeparam name="LogEntry">The type of the log entry in the transaction log.</typeparam>
    public interface IReplicationCluster<LogEntry> : ICluster
        where LogEntry : class, IMessage
    {
        /// <summary>
        /// Gets transaction log used for replication.
        /// </summary>
        IAuditTrail<LogEntry> AuditTrail { get; }

        /// <summary>
        /// Writes message into the cluster according with the specified concern.
        /// </summary>
        /// <param name="content">The message to be written into the cluster.</param>
        /// <param name="concern">The value describing level of acknowledgment from cluster.</param>
        /// <returns>The task representing asynchronous state of this operation.</returns>
        /// <exception cref="InvalidOperationException">There is no consensus in cluster.</exception>
        /// <exception cref="NotSupportedException">The specified level of acknowledgment is not supported.</exception>
        Task WriteAsync(IMessage content, WriteConcern concern);
    }
}
