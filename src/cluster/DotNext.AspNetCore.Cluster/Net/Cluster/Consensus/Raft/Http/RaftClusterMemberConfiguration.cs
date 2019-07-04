using System;
using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal class RaftClusterMemberConfiguration : ClusterMemberConfiguration
    {
        /// <summary>
        /// Gets collection of members.
        /// </summary>
        public ISet<Uri> Members { get; } = new HashSet<Uri>();

        public RequestJournalConfiguration RequestJournal { get; } = new RequestJournalConfiguration();
    }
}
