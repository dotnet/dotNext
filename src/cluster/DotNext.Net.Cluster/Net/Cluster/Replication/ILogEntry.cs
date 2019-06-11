namespace DotNext.Net.Cluster.Replication
{
    using Messaging;

    /// <summary>
    /// Represents log entry in the form of replication message.
    /// </summary>
    public interface ILogEntry : IMessage
    {
        /// <summary>
        /// Gets identifier of this log entry.
        /// </summary>
        ref readonly LogEntryId Id { get; }
    }
}
