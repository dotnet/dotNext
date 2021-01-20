using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Http.Features;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using ComponentModel;
    using HostAddressHintFeature = DotNext.Hosting.Server.Features.HostAddressHintFeature;

    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public class ClusterMemberConfiguration : IClusterMemberConfiguration
    {
        // default port for Hosted Mode
        private protected const int DefaultPort = 32999;

        static ClusterMemberConfiguration() => IPAddressConverter.Register();

        private ElectionTimeout electionTimeout = ElectionTimeout.Recommended;
        private TimeSpan? rpcTimeout;

        /// <summary>
        /// Gets or sets the address of the local node.
        /// </summary>
        public IPAddress? HostAddressHint { get; set; }

        /// <summary>
        /// Gets or sets DNS name of the local node visible to other nodes in the network.
        /// </summary>
        public string? HostNameHint { get; set; }

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
        /// Gets or sets Raft RPC timeout.
        /// </summary>
        public TimeSpan RpcTimeout
        {
            get => rpcTimeout ?? TimeSpan.FromMilliseconds(UpperElectionTimeout / 2D);
            set => rpcTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets or sets threshold of the heartbeat timeout.
        /// </summary>
        public double HeartbeatThreshold { get; set; } = 0.5D;

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
        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets or sets a value indicating that the cluster member
        /// represents standby node which is never become a leader.
        /// </summary>
        public bool Standby { get; set; }

        internal void SetupHostAddressHint(IFeatureCollection features)
        {
            var address = HostAddressHint;
            var name = HostNameHint;
            HostAddressHintFeature? feature = null;

            if (!features.IsReadOnly)
            {
                if (address is not null)
                    feature += address.ToEndPoint;

                if (!string.IsNullOrWhiteSpace(name))
                    feature += name.ToEndPoint;
            }

            if (feature is not null)
                features.Set(feature);
        }
    }
}
