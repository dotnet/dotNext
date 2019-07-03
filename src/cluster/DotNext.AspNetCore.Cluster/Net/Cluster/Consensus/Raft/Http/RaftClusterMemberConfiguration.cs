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
        /// Gets or sets memory limit (in MB) for the journal of input requests
        /// used to detect duplicate requests
        /// </summary>
        public long RequestJournalMemoryLimit { get; set; } = 10L;

        /// <summary>
        /// Gets or sets the time interval after which the request journal compares the current memory load against the absolute memory limits.
        /// </summary>
        public TimeSpan RequestJournalPollingTime { get; set; } = TimeSpan.FromMinutes(1);
    }
}
