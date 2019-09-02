namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents log entry in the audit trail.
    /// </summary>
    public interface ILogEntry : Messaging.IMessage
    {
        /// <summary>
        /// Gets a value indicating that this entry is a snapshot entry.
        /// </summary>
        bool IsSnapshot { get; }
    }
}