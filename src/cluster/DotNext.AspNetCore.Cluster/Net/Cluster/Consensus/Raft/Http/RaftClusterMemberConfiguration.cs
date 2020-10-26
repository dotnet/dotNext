using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Http.Features;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using HostAddressHintFeature = DotNext.Hosting.Server.Features.HostAddressHintFeature;

    internal class RaftClusterMemberConfiguration : ClusterMemberConfiguration
    {
        private const string DefaultClientHandlerName = "raftClient";

        private string? handlerName;

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
        public IPAddress? HostAddressHint { get; set; }

        /// <summary>
        /// Gets or sets HTTP version supported by Raft implementation.
        /// </summary>
        public HttpVersion ProtocolVersion { get; set; }

        /// <summary>
        /// Gets or sets the request timeout, in milliseconds, used for custom messages sent to other nodes in the cluster.
        /// If not specified, defaults to the value of <see cref="ClusterMemberConfiguration.UpperElectionTimeout"/>.
        /// </summary>
        public int? RequestTimeout { get; set; }

        internal void SetupHostAddressHint(IFeatureCollection features)
        {
            var address = HostAddressHint;
            if (!(address is null) && !features.IsReadOnly)
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
