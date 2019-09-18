using System;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents log entry in the audit trail.
    /// </summary>
    public interface ILogEntry : IDataTransferObject
    {
        /// <summary>
        /// Gets UTC time of the log entry when it was created.
        /// </summary>
        DateTimeOffset Timestamp { get; }
    }
}