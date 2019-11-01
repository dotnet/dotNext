using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    internal sealed class RaftHostedClusterMemberConfiguration : RaftClusterMemberConfiguration
    {
        private const int DefaultPort = 32999;

        public int Port { get; set; } = DefaultPort;

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

        internal void ConfigureKestrel(KestrelServerOptions options) => options.ListenAnyIP(Port, ConfigureListener);
    }
}
