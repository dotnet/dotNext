using System;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    using Net.Http;

    internal sealed class RaftHostedClusterMemberConfiguration : HttpClusterMemberConfiguration
    {
        private const int DefaultPort = 32999;

        public int Port { get; set; } = DefaultPort;

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
