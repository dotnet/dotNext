using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using ComponentModel;
    using HttpProtocolVersion = Net.Http.HttpProtocolVersion;

    internal class HttpClusterMemberConfiguration : ClusterMemberConfiguration
    {
        static HttpClusterMemberConfiguration() => IPNetworkConverter.Register();

        private const string DefaultClientHandlerName = "raftClient";

        private string? handlerName;
        private TimeSpan? requestTimeout;

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
        /// Gets or sets HTTP version supported by Raft implementation.
        /// </summary>
        public HttpProtocolVersion ProtocolVersion { get; set; }

#if !NETCOREAPP3_1
        /// <summary>
        /// Gets or sets HTTP version policy
        /// </summary>
        public HttpVersionPolicy ProtocolVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrLower;
#endif

        /// <summary>
        /// Gets or sets request timeout used to communicate with cluster members.
        /// </summary>
        /// <value>HTTP request timeout; default is <see cref="ClusterMemberConfiguration.UpperElectionTimeout"/>.</value>
        public TimeSpan RequestTimeout
        {
            get => requestTimeout ?? TimeSpan.FromMilliseconds(UpperElectionTimeout);
            set => requestTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
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
