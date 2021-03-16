namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents log entry in Raft audit trail.
    /// </summary>
    public interface IRaftLogEntry : IO.Log.ILogEntry
    {
        /// <summary>
        /// Gets Term value associated with this log entry.
        /// </summary>
        long Term { get; }

        /// <summary>
        /// Represents identifier of the command encapsulated
        /// by this log entry.
        /// </summary>
        int? CommandId => null;
    }
}