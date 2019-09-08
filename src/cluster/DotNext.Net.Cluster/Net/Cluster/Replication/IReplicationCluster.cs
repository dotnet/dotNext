using System;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents replication cluster.
    /// </summary>
    /// <typeparam name="LogEntry">The type of the log entry in the transaction log.</typeparam>
    public interface IReplicationCluster<LogEntry> : ICluster
        where LogEntry : class, ILogEntry
    {
        /// <summary>
        /// Gets transaction log used for replication.
        /// </summary>
        IAuditTrail<LogEntry> AuditTrail { get; }

        /// <summary>
        /// Writes message into the cluster according with the specified concern.
        /// </summary>
        /// <remarks>
        /// Data isolation level should be implemented by delegate passed into
        /// <paramref name="handler"/> parameter.
        /// </remarks>
        /// <typeparam name="T">The type of the argument to be passed into handler.</typeparam>
        /// <param name="handler">The handler used to produce change set in the form of log entries.</param>
        /// <param name="input">The value to be passed into the data handler.</param>
        /// <param name="concern">The value describing level of acknowledgment from cluster.</param>
        /// <param name="timeout">The timeout of the asynchronous operation.</param>
        /// <returns>The task representing asynchronous state of this operation.</returns>
        /// <exception cref="InvalidOperationException">The local cluster member is not a leader.</exception>
        /// <exception cref="NotSupportedException">The specified level of acknowledgment is not supported.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task WriteAsync<T>(DataHandler<T, LogEntry> handler, T input, WriteConcern concern, TimeSpan timeout);
    }
}
