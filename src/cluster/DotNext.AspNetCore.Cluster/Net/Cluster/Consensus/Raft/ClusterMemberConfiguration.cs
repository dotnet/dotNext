using System.Collections.Generic;
using System.Net;
using System.Linq;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public class ClusterMemberConfiguration : IClusterMemberConfiguration
    {
        private ElectionTimeout electionTimeout;

        public ClusterMemberConfiguration()
        {
            //recommended election timeout is between 150ms and 300ms
            electionTimeout = Raft.ElectionTimeout.Recommended;
            AbsoluteMajority = true;
        }

        public ISet<string> AllowedNetworks { get; } = new HashSet<string>();

        internal ISet<IPNetwork> ParseAllowedNetworks() => new HashSet<IPNetwork>(AllowedNetworks.Select(IPNetwork.Parse));

        public int LowerElectionTimeout
        {
            get => electionTimeout.LowerValue;
            set => electionTimeout = electionTimeout.ModifiedClone(value, electionTimeout.UpperValue);
        }

        public int UpperElectionTimeout
        {
            get => electionTimeout.UpperValue;
            set => electionTimeout = electionTimeout.ModifiedClone(electionTimeout.LowerValue, value);
        }

        ElectionTimeout IClusterMemberConfiguration.ElectionTimeout => electionTimeout;

        public bool AbsoluteMajority { get; set; }

        /// <summary>
        /// Gets metadata associated with local cluster member.
        /// </summary>
        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
    }
}
