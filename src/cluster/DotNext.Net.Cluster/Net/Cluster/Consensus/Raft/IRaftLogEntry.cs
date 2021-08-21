namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents log entry in Raft audit trail.
    /// </summary>
    public interface IRaftLogEntry : IO.Log.ILogEntry
    {
        /// <summary>
        /// Represents reserved command identifier for AddServer command.
        /// </summary>
        public const int AddServerCommandId = int.MinValue;

        /// <summary>
        /// Represents reserved command identifier for RemoveServer command.
        /// </summary>
        public const int RemoveServerCommandId = int.MinValue + 1;

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