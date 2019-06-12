using System;
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
        public ClusterMemberConfiguration()
        {
            //recommended election timeout is between 150ms and 300ms
            var timeout = Raft.ElectionTimeout.Recommended;
            LowerElectionTimeout = timeout.LowerValue;
            UpperElectionTimeout = timeout.UpperValue;
            AbsoluteMajority = true;
        }

        public ISet<string> AllowedNetworks { get; } = new HashSet<string>();

        internal ISet<IPNetwork> ParseAllowedNetworks() => new HashSet<IPNetwork>(AllowedNetworks.Select(IPNetwork.Parse));

        public int LowerElectionTimeout { get; set; }

        public int UpperElectionTimeout { get; set; }

        public bool AbsoluteMajority { get; set; }

        /// <summary>
        /// Gets metadata associated with local cluster member.
        /// </summary>
        public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
    }
}
