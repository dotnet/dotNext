namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents log entry in Raft audit trail.
    /// </summary>
    public interface IRaftLogEntry : Replication.ILogEntry
    {
        /// <summary>
        /// Gets Term value associated with this log entry.
        /// </summary>
        long Term { get; }
    }
}