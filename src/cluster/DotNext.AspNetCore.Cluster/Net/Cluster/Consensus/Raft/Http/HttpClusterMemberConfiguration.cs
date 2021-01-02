using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Http.Features;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using ComponentModel;
    using HostAddressHintFeature = DotNext.Hosting.Server.Features.HostAddressHintFeature;

    internal class HttpClusterMemberConfiguration : ClusterMemberConfiguration
    {
        static HttpClusterMemberConfiguration()
        {
            IPNetworkConverter.Register();
            IPAddressConverter.Register();
        }

        private const string DefaultClientHandlerName = "raftClient";

        private string? handlerName;
        private TimeSpan? rpcTimeout;

        /// <summary>
        /// Represents a set of networks from which remote member can make
        /// a request to the local member.
        /// </summary>
        /// <remarks>
        /// Example of IPv6 network: 2001:0db8::/32
        /// Example of IPv4 network: 192.168.0.0/24.
        /// </remarks>
        public ISet<IPNetwork> AllowedNetworks { get; } = new HashSet<IPNetwork>();

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
        /// Gets or sets Raft RPC timeout.
        /// </summary>
        public TimeSpan RpcTimeout
        {
            get => rpcTimeout ?? TimeSpan.FromMilliseconds(UpperElectionTimeout / 2D);
            set => rpcTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        internal void SetupHostAddressHint(IFeatureCollection features)
        {
            var address = HostAddressHint;
            if (address is not null && !features.IsReadOnly)
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
