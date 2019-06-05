using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RaftClusterMemberConfiguration : ClusterMemberConfiguration
    {
        private const string UserAgent = "Raft.NET";

        public RaftClusterMemberConfiguration()
        {
            ResourcePath = new Uri("/coordination", UriKind.Relative);
        }

        /// <summary>
        /// Gets collection of members.
        /// </summary>
        public ISet<Uri> Members { get; } = new HashSet<Uri>();

        public Uri ResourcePath { get; set; }
    }
}
