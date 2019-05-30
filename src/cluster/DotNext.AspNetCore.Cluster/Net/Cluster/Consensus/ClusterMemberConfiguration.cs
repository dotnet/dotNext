using System;

namespace DotNext.Net.Cluster.Consensus
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public class ClusterMemberConfiguration : IClusterMemberConfiguration
    {
        private string memberName;

        public ClusterMemberConfiguration()
        {
            //recommended election timeout is between 150ms and 300ms
            ElectionTimeout = TimeSpan.FromMilliseconds(new Random().Next(150, 301));
            MessageProcessingTimeout = TimeSpan.FromSeconds(30);
            AbsoluteMajority = true;
            
        }

        /// <summary>
        /// Gets or sets name of cluster member.
        /// </summary>
        /// <seealso cref="IClusterMemberIdentity.Name"/>
        public string MemberName
        {
            get => string.IsNullOrEmpty(memberName) ? Environment.MachineName : memberName;
            set => memberName = value;
        }

        public TimeSpan ElectionTimeout { get; set; }

        public TimeSpan MessageProcessingTimeout { get; set; }

        public bool AbsoluteMajority { get; set; }
    }
}
