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
        /// Gets or sets threshold of the heartbeat timeout.
        /// </summary>
        /// <remarks>
        /// The threshold should be in range (0, 1). The heartbeat timeout is computed as
        /// node election timeout X threshold. The default is 0.5.
        /// </remarks>
        double HeartbeatThreshold { get; set; }

        /// <summary>
        /// Gets leader election timeout settings.
        /// </summary>
        ElectionTimeout ElectionTimeout { get; }
    }
}
