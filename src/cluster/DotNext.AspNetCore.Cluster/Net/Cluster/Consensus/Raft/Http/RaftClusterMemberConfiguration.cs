using System;
using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal class RaftClusterMemberConfiguration : ClusterMemberConfiguration
    {
        private const string DefaultClientHandlerName = "raftClient";

        private string handlerName;

        /// <summary>
        /// Gets collection of members.
        /// </summary>
        public ISet<Uri> Members { get; } = new HashSet<Uri>();

        /// <summary>
        /// Gets configuration of request journal.
        /// </summary>
        public RequestJournalConfiguration RequestJournal { get; } = new RequestJournalConfiguration();

        /// <summary>
        /// Specifies that each request should create individual TCP connection (no KeepAlive).
        /// </summary>
        public bool OpenConnectionForEachRequest { get; set; }

        /// <summary>
        /// Gets or sets HTTP handler name used by Raft node client.
        /// </summary>
        public string ClientHandlerName
        {
            get => handlerName.IfNullOrEmpty(DefaultClientHandlerName);
            set => handlerName = value;
        }
    }
}
