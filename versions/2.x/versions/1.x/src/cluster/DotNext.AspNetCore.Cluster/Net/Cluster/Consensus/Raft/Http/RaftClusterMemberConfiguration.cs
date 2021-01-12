using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using HostAddressHintFeature = AspNetCore.Hosting.Server.Features.HostAddressHintFeature;

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
        /// Gets or sets address of the local node.
        /// </summary>
        public IPAddress HostAddressHint { get; set; }

        /// <summary>
        /// Gets or sets HTTP version supported by Raft implementation.
        /// </summary>
        public HttpVersion ProtocolVersion { get; set; }

        internal void SetupHostAddressHint(IFeatureCollection features)
        {
            var address = HostAddressHint;
            if (!(address is null))
                features.Set(new HostAddressHintFeature(address));
        }

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
