namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public interface IClusterMemberConfiguration
    {
        /// <summary>
        /// Indicates that each part of cluster in partitioned network allow to elect its own leader.
        /// </summary>
        /// <remarks>
        /// <see langword="false"/> value allows to build CA distributed cluster
        /// while <see langword="true"/> value allows to build CP/AP distributed cluster. 
        /// </remarks>
        bool Partitioning { get; }

        /// <summary>
        /// Gets leader election timeout settings.
        /// </summary>
        ElectionTimeout ElectionTimeout { get; }
    }
}
