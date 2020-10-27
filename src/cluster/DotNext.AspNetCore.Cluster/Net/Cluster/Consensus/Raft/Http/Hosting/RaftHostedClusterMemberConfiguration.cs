using System;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    internal sealed class RaftHostedClusterMemberConfiguration : RaftClusterMemberConfiguration
    {
        private const int DefaultPort = 32999;

        public int Port { get; set; } = DefaultPort;

        public TimeSpan RequestHeadersTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public TimeSpan KeepAliveTimeout { get; set; } = TimeSpan.FromMinutes(2);

        private void ConfigureListener(ListenOptions options)
        {
            switch (ProtocolVersion)
            {
                case HttpVersion.Http1:
                    options.Protocols = HttpProtocols.Http1;
                    break;
                case HttpVersion.Http2:
                    options.Protocols = HttpProtocols.Http2;
                    break;
            }
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
