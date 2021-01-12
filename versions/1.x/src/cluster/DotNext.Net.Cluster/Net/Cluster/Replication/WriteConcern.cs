namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Write concern describes the level of acknowledgment requested from cluster for write operations.
    /// </summary>
    public enum WriteConcern : byte
    {
        /// <summary>
        /// Guarantees that new log entries are appended to the audit trail but not yet committed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Guarantees that new log entries are appended and committed by leader node.
        /// </summary>
        LeaderOnly,

        /// <summary>
        /// Guarantees that new log entries are appended and committed by majority of nodes.
        /// </summary>
        Majority,

        /// <summary>
        /// Guarantees that new log entries are appended and committed by all nodes.
        /// </summary>
        Synchronous
    }
}