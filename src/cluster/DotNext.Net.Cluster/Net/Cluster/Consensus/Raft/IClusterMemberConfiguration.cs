﻿namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public interface IClusterMemberConfiguration
    {
        /// <summary>
        /// Indicates that each part of cluster in partitioned network allow to elect its own leader.
        /// </summary>
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

        /// <summary>
        /// Gets a value indicating that the cluster member
        /// represents standby node which will never become a leader.
        /// </summary>
        bool Standby { get; }
    }
}
