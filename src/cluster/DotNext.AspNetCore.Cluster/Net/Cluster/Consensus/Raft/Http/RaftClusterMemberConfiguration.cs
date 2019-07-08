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

        /// <summary>
        /// Gets configuration of request journal.
        /// </summary>
        public RequestJournalConfiguration RequestJournal { get; } = new RequestJournalConfiguration();

        public bool OpenConnectionForEachRequest { get; set; }
    }
}
