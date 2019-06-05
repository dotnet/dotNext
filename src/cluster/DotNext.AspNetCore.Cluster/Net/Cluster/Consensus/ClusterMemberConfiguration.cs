using System;
using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public class ClusterMemberConfiguration : IClusterMemberConfiguration
    {
        public ClusterMemberConfiguration()
        {
            //recommended election timeout is between 150ms and 300ms
            ElectionTimeout = TimeSpan.FromMilliseconds(new Random().Next(150, 301));
            AbsoluteMajority = true;
        }

        public ISet<string> AllowedNetworks { get; } = new HashSet<string>();

        public TimeSpan ElectionTimeout { get; set; }

        public bool AbsoluteMajority { get; set; }

        /// <summary>
        /// Gets metadata associated with local cluster member.
        /// </summary>
        public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();
    }
}
