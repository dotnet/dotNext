using System;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    internal sealed class RaftHostedClusterMemberConfiguration : HttpClusterMemberConfiguration
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
                case HttpVersion.Http3:
#if NETCOREAPP3_1
                    options.Protocols = (HttpProtocols)4;
#else
                    options.Protocols = HttpProtocols.Http3;
#endif
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
