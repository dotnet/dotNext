using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using IServer = Microsoft.AspNetCore.Hosting.Server.IServer;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    [SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated by DI container")]
    internal sealed class RaftEmbeddedCluster : RaftHttpCluster
    {
        internal readonly PathString ProtocolPath;
        private readonly IServer server;

        public RaftEmbeddedCluster(IServiceProvider services)
            : base(services, out var members)
        {
            var config = services.GetRequiredService<IOptions<RaftEmbeddedClusterMemberConfiguration>>().Value;
            ProtocolPath = config.ResourcePath;
            foreach (var memberUri in config.Members)
                members.Add(CreateMember(memberUri));
            server = services.GetRequiredService<IServer>();
            config.SetupHostAddressHint(server.Features);
        }

        private protected override RaftClusterMember CreateMember(Uri address)
        {
            var member = new RaftClusterMember(this, address, new Uri(ProtocolPath.Value.IfNullOrEmpty(RaftEmbeddedClusterMemberConfiguration.DefaultResourcePath), UriKind.Relative));
            ConfigureMember(member);
            return member;
        }

        private protected override Task<ICollection<EndPoint>> GetHostingAddressesAsync()
            => server.GetHostingAddressesAsync();
    }
}
