using System;
using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RaftClusterMemberConfiguration : ClusterMemberConfiguration
    {
        public RaftClusterMemberConfiguration()
        {
            ResourcePath = new Uri("/cluster-consensus/raft", UriKind.Relative);
        }

        /// <summary>
        /// Gets collection of members.
        /// </summary>
        public ISet<Uri> Members { get; } = new HashSet<Uri>();

        /// <summary>
        /// Gets or sets HTTP resource path used to capture
        /// consensus protocol messages.
        /// </summary>
        public Uri ResourcePath { get; set; }
    }
}
