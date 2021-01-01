using System;
using System.Collections.Generic;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using ComponentModel;

    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public class ClusterMemberConfiguration : IClusterMemberConfiguration
    {
        static ClusterMemberConfiguration()
        {
            IPNetworkConverter.Register();
            IPAddressConverter.Register();
        }

        private ElectionTimeout electionTimeout;

        /// <summary>
        /// Initializes a new default configuration.
        /// </summary>
        public ClusterMemberConfiguration()
        {
            electionTimeout = ElectionTimeout.Recommended;
            HeartbeatThreshold = 0.5D;
            Metadata = new Dictionary<string, string>();
            AllowedNetworks = new HashSet<IPNetwork>();
        }

        /// <summary>
        /// Represents set of networks from which remote member can make
        /// a request to the local member.
        /// </summary>
        /// <remarks>
        /// Example of IPv6 network: 2001:0db8::/32
        /// Example of IPv4 network: 192.168.0.0/24.
        /// </remarks>
        [CLSCompliant(false)]
        public ISet<IPNetwork> AllowedNetworks { get; }

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
