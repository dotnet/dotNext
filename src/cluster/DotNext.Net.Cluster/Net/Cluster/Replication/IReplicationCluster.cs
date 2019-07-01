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
    }
}
