namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IMessage = Messaging.IMessage;

    /// <summary>
    /// Represents log entry in Raft audit trail.
    /// </summary>
    public interface IRaftLogEntry : IMessage
    {
        /// <summary>
        /// Gets Term value associated with this log entry.
        /// </summary>
        long Term { get; }
    }
}