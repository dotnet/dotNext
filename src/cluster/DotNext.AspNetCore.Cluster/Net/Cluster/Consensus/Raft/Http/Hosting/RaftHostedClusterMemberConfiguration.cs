using System;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    using Net.Http;

    [Obsolete]
    internal sealed class RaftHostedClusterMemberConfiguration : HttpClusterMemberConfiguration
    {
        private const int DefaultPort = 32999;
        private int? port;

        public int Port
        {
            get => port ?? PublicEndPoint?.Port ?? DefaultPort;
            set => port = value;
        }

        public TimeSpan RequestHeadersTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan KeepAliveTimeout { get; set; } = TimeSpan.FromMinutes(2);

        private void ConfigureListener(ListenOptions options)
        {
#if NETCOREAPP3_1
            options.SetProtocolVersion(ProtocolVersion);
#else
            options.SetProtocolVersion(ProtocolVersion, ProtocolVersionPolicy);
#endif
        }

        internal void ConfigureKestrel(KestrelServerOptions options)
        {
            options.AllowSynchronousIO = false;
            options.Limits.RequestHeadersTimeout = RequestHeadersTimeout;
            options.Limits.KeepAliveTimeout = KeepAliveTimeout;
            options.ListenAnyIP(Port, ConfigureListener);
        }
    }
}
