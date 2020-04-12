using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using TransportServices;

    /// <summary>
    /// Represents transport-agnostic configuration of cluster member.
    /// </summary>
    /// <typeparam name="TTransport">The network transport type.</typeparam>
    /// <seealso cref="Udp.UdpTransport">UDP transport</seealso>
    public class ClusterMemberConfiguration<TTransport> : IClusterMemberConfiguration
        where TTransport : TransportBinding, new()
    {
        private double heartbeatThreshold;
        private ElectionTimeout electionTimeout;
        private IPAddress? publicAddress;

        /// <summary>
        /// Initializes a new configuration of local cluster member.
        /// </summary>
        /// <param name="hostAddress">The address used for hosting of local member.</param>
        public ClusterMemberConfiguration(IPEndPoint hostAddress)
        {
            electionTimeout = ElectionTimeout.Recommended;
            heartbeatThreshold = 0.5D;
            Metadata = new Dictionary<string, string>();
            Members = new HashSet<IPEndPoint>();
            NetworkTransport = new TTransport();
            HostAddress = hostAddress;
        }

        /// <summary>
        /// Gets configuration of the network transport.
        /// </summary>
        /// <value>The configuration of the network transport.</value>
        public TTransport NetworkTransport { get; }

        /// <summary>
        /// Gets the address used for hosting of local member.
        /// </summary>
        public IPEndPoint HostAddress { get; }

        /// <summary>
        /// Gets the address of the local member visible to other members.
        /// </summary>
        /// <remarks>
        /// This property is useful when local member hosted in a container (Windows, LXC or Docker)
        /// because <see cref="HostAddress"/> may return <see cref="IPAddress.Any"/> or
        /// <see cref="IPAddress.IPv6Any"/>.
        /// </remarks>
        [AllowNull]
        public IPAddress PublicHostAddress
        {
            get => publicAddress ?? HostAddress.Address;
            set => publicAddress = value;
        }

        /// <summary>
        /// Indicates that each part of cluster in partitioned network allow to elect its own leader.
        /// </summary>
        /// <remarks>
        /// <see langword="false"/> value allows to build CA distributed cluster
        /// while <see langword="true"/> value allows to build CP/AP distributed cluster. 
        /// </remarks>
        public bool Partitioning { get; set; }

        /// <summary>
        /// Gets or sets threshold of the heartbeat timeout.
        /// </summary>
        /// <remarks>
        /// The threshold should be in range (0, 1). The heartbeat timeout is computed as
        /// node election timeout X threshold. The default is 0.5
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Attempts to set invalid value.</exception>
        public double HeartbeatThreshold 
        { 
            get => heartbeatThreshold;
            set => heartbeatThreshold = value.Between(double.Epsilon, 1D, BoundType.Closed) ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets lower possible value of leader election timeout, in milliseconds.
        /// </summary>
        public int LowerElectionTimeout
        {
            get => electionTimeout.LowerValue;
            set => electionTimeout = electionTimeout.Modify(value, electionTimeout.UpperValue);
        }

        /// <summary>
        /// Gets upper possible value of leader election timeout, in milliseconds.
        /// </summary>
        public int UpperElectionTimeout
        {
            get => electionTimeout.UpperValue;
            set => electionTimeout = electionTimeout.Modify(electionTimeout.LowerValue, value);
        }

        ElectionTimeout IClusterMemberConfiguration.ElectionTimeout => electionTimeout;

        /// <summary>
        /// Gets metadata associated with local cluster member.
        /// </summary>
        public IDictionary<string, string> Metadata { get; }

        /// <summary>
        /// Gets collection of cluster members.
        /// </summary>
        /// <value>The collection of cluster members.</value>
        public ICollection<IPEndPoint> Members { get; }

        internal IClient CreateClient(IPEndPoint address)
        {
            return NetworkTransport.CreateClient(address);
        }

        internal IServer CreateServer()
        {
            if(NetworkTransport.UseDefaultServerChannels)
                NetworkTransport.ServerBacklog = Members.Count + 1;
            return NetworkTransport.CreateServer(HostAddress);
        }
    }
}