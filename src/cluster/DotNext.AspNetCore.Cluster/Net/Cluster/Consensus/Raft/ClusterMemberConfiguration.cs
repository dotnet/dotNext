using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public class ClusterMemberConfiguration : IClusterMemberConfiguration
    {
        private ElectionTimeout electionTimeout;

        /// <summary>
        /// Initializes a new default configuration.
        /// </summary>
        public ClusterMemberConfiguration()
        {
            //recommended election timeout is between 150ms and 300ms
            electionTimeout = ElectionTimeout.Recommended;
            AbsoluteMajority = true;
        }

        /// <summary>
        /// Represents set of networks from which remote member can make
        /// a request to the local member.
        /// </summary>
        /// <remarks>
        /// Example of IPv6 network: 2001:0db8::/32
        /// Example of IPv4 network: 192.168.0.0/24
        /// </remarks>
        public ISet<string> AllowedNetworks { get; } = new HashSet<string>();

        internal ISet<IPNetwork> ParseAllowedNetworks() => new HashSet<IPNetwork>(AllowedNetworks.Select(IPNetwork.Parse));

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
        /// Indicates that votes of unavailable cluster members are
        /// taken into account during voting process.
        /// </summary>
        /// <remarks>
        /// <see langword="true"/> value allows to build CA distributed cluster
        /// while <see langword="false"/> value allows to build CP/AP distributed cluster. 
        /// </remarks>
        public bool AbsoluteMajority { get; set; }

        /// <summary>
        /// Gets metadata associated with local cluster member.
        /// </summary>
        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
    }
}
