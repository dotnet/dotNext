using System;
using System.Collections.Generic;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public class ClusterMemberConfiguration : IClusterMemberConfiguration
    {
        private ElectionTimeout electionTimeout;
        private TimeSpan? requestTimeout;

        /// <summary>
        /// Initializes a new default configuration.
        /// </summary>
        public ClusterMemberConfiguration()
        {
            electionTimeout = ElectionTimeout.Recommended;
            HeartbeatThreshold = 0.5D;
            Metadata = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets lower possible value of leader election timeout, in milliseconds.
        /// </summary>
        public int LowerElectionTimeout
        {
            get => electionTimeout.LowerValue;
            set => electionTimeout.Update(value, null);
        }

        /// <summary>
        /// Gets upper possible value of leader election timeout, in milliseconds.
        /// </summary>
        public int UpperElectionTimeout
        {
            get => electionTimeout.UpperValue;
            set => electionTimeout.Update(null, value);
        }

        /// <summary>
        /// Gets or sets request timeout used to communicate with cluster members.
        /// </summary>
        /// <value>HTTP request timeout; default is <see cref="ClusterMemberConfiguration.UpperElectionTimeout"/>.</value>
        public TimeSpan RequestTimeout
        {
            get => requestTimeout ?? TimeSpan.FromMilliseconds(UpperElectionTimeout);
            set => requestTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets or sets threshold of the heartbeat timeout.
        /// </summary>
        public double HeartbeatThreshold { get; set; }

        /// <inheritdoc/>
        ElectionTimeout IClusterMemberConfiguration.ElectionTimeout => electionTimeout;

        /// <summary>
        /// Indicates that each part of cluster in partitioned network allow to elect its own leader.
        /// </summary>
        /// <remarks>
        /// <see langword="false"/> value allows to build CA distributed cluster
        /// while <see langword="true"/> value allows to build CP/AP distributed cluster.
        /// </remarks>
        public bool Partitioning { get; set; }

        /// <summary>
        /// Gets metadata associated with local cluster member.
        /// </summary>
        public IDictionary<string, string> Metadata { get; }

        /// <summary>
        /// Gets or sets a value indicating that the cluster member
        /// represents standby node which is never become a leader.
        /// </summary>
        public bool Standby { get; set; }
    }
}
