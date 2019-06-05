using System;
using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public class ClusterMemberConfiguration : IClusterMemberConfiguration
    {
        internal readonly Guid MemberId;

        public ClusterMemberConfiguration()
        {
            //recommended election timeout is between 150ms and 300ms
            ElectionTimeout = TimeSpan.FromMilliseconds(new Random().Next(150, 301));
            MessageProcessingTimeout = TimeSpan.FromSeconds(30);
            AbsoluteMajority = true;
            MemberId = Guid.NewGuid();
        }

        public ISet<string> AllowedNetworks { get; } = new HashSet<string>();

        public TimeSpan ElectionTimeout { get; set; }

        public TimeSpan MessageProcessingTimeout { get; set; }

        public bool AbsoluteMajority { get; set; }
    }
}
