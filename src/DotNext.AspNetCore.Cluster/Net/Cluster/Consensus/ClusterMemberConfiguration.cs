using System;
using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus
{
    /// <summary>
    /// Represents configuration of cluster member.
    /// </summary>
    public class ClusterMemberConfiguration
    {
        private string memberName;

        public ClusterMemberConfiguration()
        {
            //recommended election timeout is between 150ms and 300ms
            ElectionTimeout = TimeSpan.FromMilliseconds(new Random().Next(150, 301));
            AbsoluteMajority = true;
            ResourcePath = new Uri("/coordination", UriKind.Relative);
        }

        /// <summary>
        /// Gets or sets name of cluster member.
        /// </summary>
        /// <seealso cref="IClusterMember.Name"/>
        public string MemberName
        {
            get => string.IsNullOrEmpty(memberName) ? Environment.MachineName : memberName;
            set => memberName = value;
        }

        /// <summary>
        /// Gets collection of members.
        /// </summary>
        public ISet<Uri> Members { get; } = new HashSet<Uri>();

        /// <summary>
        /// Gets or sets value indicating that TCP connection can be reused
        /// for multiple HTTP requests.
        /// </summary>
        public bool KeepAlive { get; set; }

        public TimeSpan ElectionTimeout { get; set; }

        public bool AbsoluteMajority { get; set; }

        public Uri ResourcePath { get; set; }
    }
}
